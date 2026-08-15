import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { env } from "../support.js";
import kvBinding from "../../../examples/KvBinding/dist/worker.js";
import r2Binding from "../../../examples/R2Binding/dist/worker.js";

describe("native storage bindings", () => {
  const invokeNative = async (path) => {
    const nativeBindings = await import("../fixtures/NativeBindings/dist/worker.js");
    return nativeBindings.default.fetch(
      new Request(`https://worker.test${path}`),
      env,
      createExecutionContext(),
    );
  };

  it("executes prepared statements against the local D1 simulator", async () => {
    const nativeBindings = await import("../fixtures/NativeBindings/dist/worker.js");
    const response = await nativeBindings.default.fetch(
      new Request("https://worker.test/d1"),
      env,
      createExecutionContext(),
    );

    await expect(response.json()).resolves.toEqual({ name: "Ada" });
  });

  it("round-trips KV metadata, listing, and deletion", async () => {
    const response = await invokeNative("/kv-lifecycle");
    const result = await response.json();

    expect(result).toMatchObject({
      value: "stored",
      listComplete: true,
      deleted: null,
    });
    expect(result.listed).toMatch(/^[-0-9a-f]{36}$/i);
  });

  it("round-trips R2 metadata, listing, and deletion", async () => {
    const response = await invokeNative("/r2-lifecycle");
    const result = await response.json();

    expect(result.key).toBe(result.listed);
    expect(result).toMatchObject({
      size: 8,
      contentType: "text/custom",
      deleted: null,
    });
  });

  it("executes D1 batches, raw rows, and sessions", async () => {
    const response = await invokeNative("/d1-advanced");

    await expect(response.json()).resolves.toEqual({
      firstSuccess: true,
      secondSuccess: true,
      firstValue: 1,
      secondValue: 2,
      hasBookmark: true,
    });
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
