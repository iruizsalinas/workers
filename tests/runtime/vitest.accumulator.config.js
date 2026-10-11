import { cloudflareTest } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [cloudflareTest({ wrangler: { configPath: "./tests/runtime/wrangler.accumulator.jsonc" } })],
  test: { include: ["tests/runtime/scenarios/accumulator.test.js"] },
});
