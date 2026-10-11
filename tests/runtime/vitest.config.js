import { cloudflareTest } from "@cloudflare/vitest-plugin";
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
    exclude: [
      "tests/runtime/scenarios/chat.test.js",
      "tests/runtime/scenarios/accumulator.test.js",
      "tests/runtime/scenarios/services.test.js",
      "tests/runtime/scenarios/context.test.js",
      "tests/runtime/scenarios/databases.test.js",
      "tests/runtime/runtime/crypto.test.js",
    ],
  },
});
