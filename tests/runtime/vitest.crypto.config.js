import { cloudflareTest } from "@cloudflare/vitest-plugin";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [cloudflareTest({ wrangler: { configPath: "./tests/runtime/wrangler.crypto.jsonc" } })],
  test: { include: ["tests/runtime/runtime/crypto.test.js"] },
});
