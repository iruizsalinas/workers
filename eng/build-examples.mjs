import { readdirSync } from "node:fs";
import { availableParallelism } from "node:os";
import { join, relative, resolve } from "node:path";
import { spawn } from "node:child_process";

const repositoryRoot = resolve(import.meta.dirname, "..");
const projectRoots = [
  join(repositoryRoot, "examples"),
  join(repositoryRoot, "tests", "runtime", "fixtures"),
];
const projects = projectRoots.flatMap(findProjects)
  .sort((left, right) => left.localeCompare(right));

if (projects.length === 0) {
  throw new Error("No runtime projects were found.");
}

const concurrency = readConcurrency();
const started = performance.now();

console.log("Preparing shared build dependencies...");
await runDotnet([
  "build", join(repositoryRoot, "src", "Workers.csproj"),
  "-c", "Release", "--nologo", "-p:NuGetAudit=false",
]);

console.log(`Restoring ${projects.length} runtime projects (${concurrency} at a time)...`);
await runParallel(projects, (project) => runDotnet([
  "restore", project, "--no-dependencies", "--nologo", "-p:NuGetAudit=false",
]));

console.log(`Building ${projects.length} runtime projects (${concurrency} at a time)...`);
let completed = 0;
await runParallel(projects, async (project) => {
  await runDotnet([
    "build", project, "-c", "Release", "--no-restore", "--no-dependencies",
    "--nologo", "-p:NuGetAudit=false",
  ]);
  completed++;
  console.log(`[${completed}/${projects.length}] ${relative(repositoryRoot, project)}`);
});

console.log(`Built ${projects.length} runtime projects in ${formatDuration(performance.now() - started)}.`);

function findProjects(root) {
  return readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((entry) => {
      const directory = join(root, entry.name);
      return readdirSync(directory)
        .filter((name) => name.endsWith(".csproj"))
        .map((name) => join(directory, name));
    });
}

function readConcurrency() {
  const configured = Number.parseInt(process.env.WORKERS_BUILD_CONCURRENCY ?? "", 10);
  if (Number.isInteger(configured) && configured > 0) {
    return Math.min(configured, projects.length);
  }
  return Math.min(Math.max(availableParallelism() - 1, 2), 8, projects.length);
}

async function runParallel(items, action) {
  let next = 0;
  await Promise.all(Array.from({ length: Math.min(concurrency, items.length) }, async () => {
    while (next < items.length) {
      const item = items[next++];
      await action(item);
    }
  }));
}

function runDotnet(arguments_) {
  return new Promise((resolvePromise, reject) => {
    const child = spawn("dotnet", arguments_, {
      cwd: repositoryRoot,
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
    });
    let output = "";
    child.stdout.on("data", (data) => output += data);
    child.stderr.on("data", (data) => output += data);
    child.on("error", reject);
    child.on("close", (code) => {
      if (code === 0) {
        resolvePromise();
        return;
      }
      reject(new Error(
        `${["dotnet", ...arguments_].join(" ")} failed with exit code ${code}.\n${output.trim()}`,
      ));
    });
  });
}

function formatDuration(milliseconds) {
  return `${(milliseconds / 1000).toFixed(1)}s`;
}
