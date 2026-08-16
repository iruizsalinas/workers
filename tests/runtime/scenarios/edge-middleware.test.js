import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";

const metadata = { id: "version-2", tag: "production", timestamp: new Date("2026-08-16T00:00:00Z") };

async function loadWorker(success = true) {
  const calls = { points: [], assets: [] };
  const environment = {
    RATE: { limit: vi.fn(async () => ({ success })) },
    VERSION: metadata,
    METRICS: { writeDataPoint: vi.fn(point => calls.points.push(point)) },
    ASSETS: { fetch: vi.fn(async request => {
      calls.assets.push(request.url);
      return new Response("asset", { status: 200, headers: { "content-type": "text/plain" } });
    }) },
  };
  const module = await import("../fixtures/EdgeMiddleware/dist/worker.js");
  return { calls, environment, invoke: (path, init) => module.default.fetch(
    new Request(`https://worker.test${path}`, init), environment, createExecutionContext()) };
}

describe("edge middleware", () => {
  it("composes rate limiting, version metadata, and analytics", async () => {
    const { calls, environment, invoke } = await loadWorker();
    const response = await invoke("/api/version", { headers: { "x-user-id": "ada" } });
    await expect(response.json()).resolves.toMatchObject({ id: "version-2", tag: "production" });
    expect(environment.RATE.limit).toHaveBeenCalledWith({ key: "user:ada:/api/version" });
    expect(calls.points[0].indexes).toEqual(["version-2"]);
    expect(calls.points[0].blobs[0]).toBe("version");
  });

  it("serves secured assets and records rejected requests", async () => {
    const allowed = await loadWorker();
    const asset = await allowed.invoke("/docs");
    expect(await asset.text()).toBe("asset");
    expect(asset.headers.get("x-content-type-options")).toBe("nosniff");
    expect(asset.headers.get("x-worker-version")).toBe("version-2");

    const denied = await loadWorker(false);
    const rejected = await denied.invoke("/docs");
    expect(rejected.status).toBe(429);
    expect(rejected.headers.get("retry-after")).toBe("60");
  });
});
