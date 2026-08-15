import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import { env } from "../support.js";

describe("runtime intrinsics", () => {
  const invoke = async (path, init) => {
    const runtime = await import("../fixtures/RuntimeIntrinsics/dist/worker.js");
    return runtime.default.fetch(
      new Request(`https://worker.test${path}`, init),
      env,
      createExecutionContext(),
    );
  };

  it("reads native request bodies, methods, paths, and headers", async () => {
    const response = await invoke("/request", {
      method: "POST",
      headers: { "x-test": "present" },
      body: "request-body",
    });

    await expect(response.json()).resolves.toEqual({
      method: "POST",
      path: "/request",
      text: "request-body",
      header: "present",
    });
  });

  it("creates, clones, and reads native responses", async () => {
    const response = await invoke("/response");
    await expect(response.json()).resolves.toEqual({
      status: 201,
      header: "yes",
      text: "response-body",
    });
  });

  it("converts a C# Body directly into a native response", async () => {
    const response = await invoke("/body");
    expect(await response.text()).toBe("body-value");
    expect(response.headers.get("content-type")).toContain("text/plain");
  });

  it("reads an entire native request ReadableStream", async () => {
    const response = await invoke("/stream", { method: "POST", body: "stream-body" });
    await expect(response.json()).resolves.toEqual({ length: 11 });
  });

  it("uses native Web Crypto for UUIDs, random bytes, and SHA-256", async () => {
    const response = await invoke("/crypto");
    const result = await response.json();

    expect(result.uuid).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    expect(result).toMatchObject({
      randomLength: 16,
      digestLength: 32,
      equal: true,
      streamEqual: true,
    });
  });

  it("uses native timers and AbortController", async () => {
    expect(await (await invoke("/timer")).text()).toBe("delayed");
    expect(await (await invoke("/abort")).text()).toBe("aborted");
  });

  it("creates and uses a native WebSocketPair", async () => {
    expect(await (await invoke("/websocket")).text()).toBe("websocket-sent");
  });

  it("runs a generated C# HTMLRewriter callback", async () => {
    const response = await invoke("/html");
    expect(await response.text()).toBe('<main><p data-generated="csharp">hello</p></main>');
  });

  it("invokes a reachable cross-file helper and native Console, Guid, and Random shapes", async () => {
    const log = vi.spyOn(console, "log").mockImplementation(() => undefined);
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    const result = await (await invoke("/helpers")).json();

    expect(result.message).toBe("helper:cross-file");
    expect(result.guid).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    expect(Number.isInteger(result.next)).toBe(true);
    expect(result.next).toBeGreaterThanOrEqual(0);
    expect(result.bounded).toBeGreaterThanOrEqual(0);
    expect(result.bounded).toBeLessThan(10);
    expect(result.ranged).toBeGreaterThanOrEqual(5);
    expect(result.ranged).toBeLessThan(10);
    expect(result.fraction).toBeGreaterThanOrEqual(0);
    expect(result.fraction).toBeLessThan(1);
    expect(log).toHaveBeenCalledWith("generated-console");
    expect(error).toHaveBeenCalledWith("generated-error");
  });
});
