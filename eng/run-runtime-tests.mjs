import { resolve } from "node:path";
import { spawnSync } from "node:child_process";

const repositoryRoot = resolve(import.meta.dirname, "..");
const vitest = resolve(repositoryRoot, "node_modules", "vitest", "vitest.mjs");
const result = spawnSync(
  process.execPath,
  [vitest, "run", "--config", "tests/runtime/vitest.config.js"],
  {
    cwd: repositoryRoot,
    stdio: "inherit",
    env: {
      ...process.env,
      // Keep Wrangler logs inside the test output instead of the host filesystem.
      WRANGLER_WRITE_LOGS: "false",
    },
  },
);

if (result.error) {
  throw result.error;
}
process.exit(result.status ?? 1);
