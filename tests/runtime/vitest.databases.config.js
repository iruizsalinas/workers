import { spawnSync } from "node:child_process";
import { resolve } from "node:path";
import { cloudflareTest } from "@cloudflare/vitest-pool-workers";
import { defineConfig } from "vitest/config";

// Runs against real servers; set any of these to enable the matching tests.
const urls = {
  POSTGRES_URL: process.env.WORKERS_POSTGRES_URL,
  MYSQL_URL: process.env.WORKERS_MYSQL_URL,
  MONGODB_URL: process.env.WORKERS_MONGODB_URL,
};
const bindings = Object.fromEntries(Object.entries(urls).filter(([, value]) => value));
const hyperdrives = Object.fromEntries([["POSTGRES", urls.POSTGRES_URL], ["MYSQL", urls.MYSQL_URL]].filter(([, value]) => value));

// The drivers are CommonJS packages built on Node.js modules, so the test loads the Worker as
// Wrangler bundles it for deployment.
const runtime = import.meta.dirname;
const bundled = spawnSync(process.execPath, [
  resolve(runtime, "../../node_modules/wrangler/bin/wrangler.js"), "deploy", "--dry-run",
  "--config", resolve(runtime, "wrangler.databases.jsonc"),
  "--outdir", resolve(runtime, ".wrangler/databases"),
  resolve(runtime, "fixtures/Databases/dist/worker.js"),
], { stdio: "inherit", env: { ...process.env, WRANGLER_WRITE_LOGS: "false" } });
if (bundled.status !== 0) throw new Error("Bundling the database fixture failed.");

export default defineConfig({
  plugins: [cloudflareTest({ wrangler: { configPath: "./tests/runtime/wrangler.databases.jsonc" }, miniflare: { bindings, hyperdrives } })],
  test: { include: ["tests/runtime/scenarios/databases.test.js"] },
});
