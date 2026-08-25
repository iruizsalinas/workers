import { createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import { env } from "../support.js";
import cacheApi from "../../../examples/CacheApi/dist/worker.js";
import corsHeaders from "../../../examples/CorsHeaders/dist/worker.js";
import helloWorld from "../../../examples/HelloWorld/dist/worker.js";
import jsonApi from "../../../examples/JsonApi/dist/worker.js";
import proxyFetch from "../../../examples/ProxyFetch/dist/worker.js";
import redirects from "../../../examples/Redirects/dist/worker.js";

describe("HTTP workers", () => {
  it("reads the native request URL and returns JSON", async () => {
    const response = await jsonApi.fetch(
      new Request("https://worker.test/items/42?ignored=true"),
      env,
      createExecutionContext(),
    );

    expect(response.headers.get("content-type")).toContain("application/json");
    await expect(response.json()).resolves.toEqual({ ok: true, path: "/items/42" });
  });

  it("routes root requests and returns a 404 for unknown paths", async () => {
    const root = await helloWorld.fetch(
      new Request("https://worker.test/"),
      env,
      createExecutionContext(),
    );
    const missing = await helloWorld.fetch(
      new Request("https://worker.test/missing"),
      env,
      createExecutionContext(),
    );

    expect(await root.text()).toBe("Hello from C# on Cloudflare Workers.");
    expect(missing.status).toBe(404);
  });

  it("emits CORS headers for preflight and normal responses", async () => {
    const preflight = await corsHeaders.fetch(
      new Request("https://worker.test/", { method: "OPTIONS" }),
      env,
      createExecutionContext(),
    );
    const response = await corsHeaders.fetch(
      new Request("https://worker.test/"),
      env,
      createExecutionContext(),
    );

    expect(preflight.status).toBe(204);
    expect(preflight.headers.get("access-control-allow-methods")).toBe("GET, POST, OPTIONS");
    expect(response.headers.get("access-control-allow-origin")).toBe("*");
    expect(response.headers.get("x-example")).toBe("cors");
  });

  it("creates native redirects from query parameters", async () => {
    const response = await redirects.fetch(
      new Request("https://worker.test/?to=https%3A%2F%2Fexample.net%2Ftarget"),
      env,
      createExecutionContext(),
    );

    expect(response.status).toBe(302);
    expect(response.headers.get("location")).toBe("https://example.net/target");
  });

  it("uses the local Cache simulator and waitUntil", async () => {
    const request = new Request(`https://worker.test/cache-${crypto.randomUUID()}`);
    const firstContext = createExecutionContext();
    const first = await cacheApi.fetch(request, env, firstContext);
    await waitOnExecutionContext(firstContext);

    const second = await cacheApi.fetch(request, env, createExecutionContext());

    expect(first.headers.get("x-cache")).toBe("miss");
    expect(second.headers.get("x-cache")).toBe("hit");
    expect(await second.text()).toBe(await first.text());
  });

  it("uses an explicit outbound fetch mock", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response("upstream"),
    );

    const response = await proxyFetch.fetch(
      new Request("https://worker.test/"),
      env,
      createExecutionContext(),
    );

    expect(fetchMock).toHaveBeenCalledWith("https://example.com");
    expect(response.headers.get("x-proxied-by")).toBe("Workers");
    expect(await response.text()).toBe("upstream");
  });
});
