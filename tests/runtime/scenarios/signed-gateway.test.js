import { createExecutionContext, waitOnExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";
import gateway from "../fixtures/SignedGateway/dist/worker.js";

const secret = "local-signing-secret";

async function signature(method, url, body = "") {
  const canonical = new URL(url);
  for (const name of Array.from(canonical.searchParams.keys())) {
    if (name.startsWith("utm_") || name === "fbclid" || name === "gclid") canonical.searchParams.delete(name);
  }
  if (!canonical.searchParams.has("lang")) canonical.searchParams.set("lang", "en");
  canonical.searchParams.sort();
  const payload = new TextEncoder().encode(`${method}\n${canonical.pathname}${canonical.search}\n${body}`);
  const key = await crypto.subtle.importKey("raw", new TextEncoder().encode(secret), { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
  const bytes = new Uint8Array(await crypto.subtle.sign("HMAC", key, payload));
  return `sha256=${Array.from(bytes, byte => byte.toString(16).padStart(2, "0")).join("")}`;
}

describe("signed API gateway", () => {
  it("canonicalizes, verifies, forwards, tags, and caches a request", async () => {
    const url = `https://worker.test/data?utm_source=test&b=2&a=1&nonce=${crypto.randomUUID()}`;
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(new Response("origin", { status: 200 }));
    const headers = { "x-signature": await signature("GET", url), origin: "https://client.test" };
    const context = createExecutionContext();
    const first = await gateway.fetch(new Request(url, { headers }), { SIGNING_SECRET: secret, ORIGIN: "https://origin.test" }, context);
    await waitOnExecutionContext(context);

    expect(await first.text()).toBe("origin");
    expect(first.headers.get("x-edge-cache")).toBe("MISS");
    expect(first.headers.get("etag")).toMatch(/^"[0-9a-f]+"$/i);
    expect(first.headers.get("access-control-allow-origin")).toBe("https://client.test");
    expect(fetchMock.mock.calls[0][0]).toBe(`https://origin.test/data?a=1&b=2&lang=en&nonce=${new URL(url).searchParams.get("nonce")}`);

    const second = await gateway.fetch(new Request(url, { headers }), { SIGNING_SECRET: secret, ORIGIN: "https://origin.test" }, createExecutionContext());
    expect(second.headers.get("x-edge-cache")).toBe("HIT");
    expect(fetchMock).toHaveBeenCalledOnce();
  });

  it("rejects invalid signatures and handles CORS preflight without forwarding", async () => {
    const invalid = await gateway.fetch(new Request("https://worker.test/data", { headers: { "x-signature": "bad" } }),
      { SIGNING_SECRET: secret, ORIGIN: "https://origin.test" }, createExecutionContext());
    expect(invalid.status).toBe(401);

    const preflight = await gateway.fetch(new Request("https://worker.test/data", { method: "OPTIONS", headers: { origin: "https://client.test" } }),
      { SIGNING_SECRET: secret, ORIGIN: "https://origin.test" }, createExecutionContext());
    expect(preflight.status).toBe(204);
    expect(preflight.headers.get("vary")).toContain("Origin");
  });
});
