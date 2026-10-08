import { resolve } from "node:path";
import { spawnSync } from "node:child_process";

const repositoryRoot = resolve(import.meta.dirname, "..");
const vitest = resolve(repositoryRoot, "node_modules", "vitest", "vitest.mjs");
for (const config of [
  "tests/runtime/vitest.config.js",
  "tests/runtime/vitest.chat.config.js",
  "tests/runtime/vitest.accumulator.config.js",
  "tests/runtime/vitest.services.config.js",
  // Database drivers need real servers, provided through WORKERS_POSTGRES_URL, WORKERS_MYSQL_URL
  // and WORKERS_MONGODB_URL.
  ...(process.env.WORKERS_POSTGRES_URL || process.env.WORKERS_MYSQL_URL || process.env.WORKERS_MONGODB_URL
    ? ["tests/runtime/vitest.databases.config.js"]
    : []),
]) {
  const result = spawnSync(process.execPath, [vitest, "run", "--reporter", "minimal", "--config", config], {
    cwd: repositoryRoot,
    stdio: "inherit",
    env: { ...process.env, WRANGLER_WRITE_LOGS: "false" },
  });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
}
