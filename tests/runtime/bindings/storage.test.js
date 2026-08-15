import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { env } from "../support.js";
import kvBinding from "../../../examples/KvBinding/dist/worker.js";
import r2Binding from "../../../examples/R2Binding/dist/worker.js";

describe("native storage bindings", () => {
  it("executes prepared statements against the local D1 simulator", async () => {
    const nativeBindings = await import("../fixtures/NativeBindings/dist/worker.js");
    const response = await nativeBindings.default.fetch(
      new Request("https://worker.test/d1"),
      env,
      createExecutionContext(),
    );

    await expect(response.json()).resolves.toEqual({ name: "Ada" });
  });
  it("reads text from the local KV simulator and handles misses", async () => {
    const key = `key-${crypto.randomUUID()}`;
    await env.KV.put(key, "stored value");

    const found = await kvBinding.fetch(
      new Request(`https://worker.test/?key=${key}`),
      env,
      createExecutionContext(),
    );
    const missing = await kvBinding.fetch(
      new Request(`https://worker.test/?key=missing-${key}`),
      env,
      createExecutionContext(),
    );

    expect(await found.text()).toBe("stored value");
    expect(missing.status).toBe(404);
  });

  it("streams objects from the local R2 simulator and handles misses", async () => {
    const key = `object-${crypto.randomUUID()}.txt`;
    await env.BUCKET.put(key, "r2 body");

    const found = await r2Binding.fetch(
      new Request(`https://worker.test/?key=${key}`),
      env,
      createExecutionContext(),
    );
    const missing = await r2Binding.fetch(
      new Request(`https://worker.test/?key=missing-${key}`),
      env,
      createExecutionContext(),
    );

    expect(await found.text()).toBe("r2 body");
    expect(missing.status).toBe(404);
  });
});
