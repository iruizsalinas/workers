import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import worker from "../fixtures/HtmlProxy/dist/worker.js";

const invoke = async (url, init) => {
  const context = createExecutionContext();
  const response = await worker.fetch(new Request(url, init), env, context);
  await waitOnExecutionContext(context);
  return response;
};

describe("cached HTML proxy scenario", () => {
  it("runs stateful sync and async HTML handlers and caches the transformed response", async () => {
    const url = `https://worker.test/page-${crypto.randomUUID()}?view=full`;
    await env.CONTENT.put("banner", "Edge notice");
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response(
      '<html><body><a href="/next">Next</a></body></html>',
      { headers: { "content-type": "text/html; charset=utf-8" } },
    ));

    const first = await invoke(url);
    expect(first.headers.get("x-edge-cache")).toBe("MISS");
    expect(await first.text()).toContain('<aside class="edge-banner">Edge notice</aside>');
    expect(await (await invoke(url)).text()).toContain('href="https://origin.test/next"');
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("rejects non-GET requests and passes non-HTML responses through", async () => {
    const rejected = await invoke("https://worker.test/path", { method: "POST" });
    expect(rejected.status).toBe(405);
    expect(rejected.headers.get("allow")).toBe("GET");

    vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("plain", {
      headers: { "content-type": "text/plain" },
    }));
    expect(await (await invoke(`https://worker.test/plain-${crypto.randomUUID()}`)).text()).toBe("plain");
  });
});
