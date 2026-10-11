import { cloudflareTest } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [cloudflareTest({ wrangler: { configPath: "./tests/runtime/wrangler.context.jsonc" } })],
  test: { include: ["tests/runtime/scenarios/context.test.js"] },
});
