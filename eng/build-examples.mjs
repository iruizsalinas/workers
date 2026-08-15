import { readdirSync } from "node:fs";
import { join, resolve } from "node:path";
import { spawnSync } from "node:child_process";

const repositoryRoot = resolve(import.meta.dirname, "..");
const examplesRoot = join(repositoryRoot, "examples");
const fixtureRoot = join(repositoryRoot, "tests", "runtime", "fixtures");
const roots = [examplesRoot, fixtureRoot];
const projects = roots.flatMap((root) =>
  readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isDirectory())
    .flatMap((entry) => {
      const directory = join(root, entry.name);
      return readdirSync(directory)
        .filter((name) => name.endsWith(".csproj"))
        .map((name) => join(directory, name));
    }),
  )
  .sort();

if (projects.length === 0) {
  throw new Error(`No example projects were found under ${examplesRoot}.`);
}

for (const project of projects) {
  console.log(`Building ${project.slice(repositoryRoot.length + 1)}...`);
  const result = spawnSync(
    "dotnet",
    ["build", project, "-c", "Release", "--nologo", "-p:NuGetAudit=false"],
    { cwd: repositoryRoot, stdio: "inherit" },
  );

  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}
