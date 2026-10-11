import { createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
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

  it("reconstructs writable headers and preserves native body ownership", async () => {
    const upstream = Response.redirect("https://example.test/", 307);
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(upstream);
    try {
      const response = await invoke("/response-ownership");
      expect(await response.json()).toEqual({
        immutable: true,
        cloneImmutable: true,
        status: 307,
        location: "https://example.test/",
        copied: "copied",
        original: null,
        text: "shared",
        bodyUsed: true,
        independentText: "shared",
        snapshot: "initial",
        contentType: "application/json",
        cookies: ["first=1", "second=2"],
      });
    } finally {
      fetchMock.mockRestore();
    }
  });

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

  it("keeps FormData, streams, and URLSearchParams as native body values", async () => {
    const form = new FormData();
    form.set("value", "native");
    const formResult = await (await invoke("/body-form", { method: "POST", body: form })).json();
    expect(formResult).toEqual({ usedBefore: false, usedAfter: true, value: "native" });

    expect(await (await invoke("/body-stream", { method: "POST", body: "streamed" })).text())
      .toBe("streamed");

    const query = await invoke("/body-query?first=one&second=two");
    expect(new TextDecoder().decode(await query.arrayBuffer())).toBe("first=one&second=two");
    expect(query.headers.get("content-type")).toContain("application/x-www-form-urlencoded");
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

  it("waits with the native scheduler", async () => {
    expect(await (await invoke("/timer")).text()).toBe("delayed");
  });

  it("exposes the incoming request signal", async () => {
    expect(await (await invoke("/request-cancellation")).json()).toEqual({ aborted: false, threw: false });

    const controller = new AbortController();
    controller.abort();
    expect(await (await invoke("/request-cancellation", { signal: controller.signal })).json())
      .toEqual({ aborted: true, threw: true });
  });

  it("creates and uses a native WebSocketPair", async () => {
    expect(await (await invoke("/websocket")).text()).toBe("websocket-sent");
  });

  it("receives WebSocket messages through a message listener", async () => {
    const response = await invoke("/websocket-events");
    expect(response.status).toBe(101);
    const socket = response.webSocket;
    socket.accept();
    const message = new Promise((resolve) => socket.addEventListener("message", resolve, { once: true }));
    socket.send("echo from C#");
    expect((await message).data).toBe("echo from C#");
    socket.close(1000, "done");
  });

  it("passes close metadata to a close listener", async () => {
    const runtime = await import("../fixtures/RuntimeIntrinsics/dist/worker.js");
    const context = createExecutionContext();
    const key = `socket-close-${crypto.randomUUID()}`;
    const response = await runtime.default.fetch(
      new Request(`https://worker.test/websocket-close?key=${key}`), env, context,
    );
    const socket = response.webSocket;
    socket.accept();
    socket.close(1000, "completed");
    // The listener runs when the close event arrives, which can be after this test reaches here.
    await vi.waitFor(async () => expect(await env.KV.get(key, "json")).toEqual({
      code: 1000, reason: "completed", clean: true,
    }));
    await waitOnExecutionContext(context);
    await env.KV.delete(key);
  });

  it("runs a generated C# HTMLRewriter callback", async () => {
    const response = await invoke("/html");
    expect(await response.text()).toBe('<main><p data-generated="csharp">hello</p></main>');
  });

  it("invokes a reachable cross-file helper and native logging, UUID and random APIs", async () => {
    const log = vi.spyOn(console, "info").mockImplementation(() => undefined);
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    const result = await (await invoke("/helpers")).json();

    expect(result.message).toBe("helper:cross-file");
    expect(result.uuid).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
    expect(result.fraction).toBeGreaterThanOrEqual(0);
    expect(result.fraction).toBeLessThan(1);
    expect(log).toHaveBeenCalledWith("generated-console");
    expect(error).toHaveBeenCalledWith("generated-error");
  });

  it("preserves native URL, query, and independent Headers semantics", async () => {
    const response = await invoke("/http-semantics?value=one&value=two", {
      headers: { "x-original": "yes", "x-remove": "yes" },
    });

    await expect(response.json()).resolves.toEqual({
      pathAndQuery: "/http-semantics?value=one&value=two",
      values: ["one", "two"],
      originalCount: 2,
      cloneCount: 2,
      originalCloned: false,
      cloneRemoved: false,
      cookies: ["first=1", "second=2"],
    });
  });

  it("exposes a parsed URL and native request metadata", async () => {
    const response = await invoke("/url?mode=full");

    await expect(response.json()).resolves.toEqual({
      origin: "https://worker.test",
      protocol: "https:",
      host: "worker.test",
      hostname: "worker.test",
      port: "",
      username: "",
      password: "",
      path: "/url",
      query: "?mode=full",
      fragment: "",
      redirect: "follow",
      hasSignal: true,
    });
  });

  it("round-trips UTF-8/base64 and replaces a request URL", async () => {
    const response = await invoke("/text-codec");

    await expect(response.json()).resolves.toEqual({
      encoded: "aGVsbG8gKyBlZGdl",
      decoded: "hello + edge",
      escaped: "hello%20%20%20edge",
      forwarded: "/accepted?source=codec",
    });
  });

  it("opens a named native Cache and completes its full lifecycle", async () => {
    const response = await invoke("/cache-lifecycle");

    await expect(response.json()).resolves.toEqual({
      found: true,
      deleted: true,
      missing: true,
    });
  });
});
