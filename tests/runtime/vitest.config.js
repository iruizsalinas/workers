import { cloudflareTest } from "@cloudflare/vitest-pool-workers";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [
    cloudflareTest({
      wrangler: {
        configPath: "./tests/runtime/wrangler.jsonc",
      },
    }),
  ],
  test: {
    include: ["tests/runtime/**/*.test.js"],
  },
});
