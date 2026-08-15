import { createExecutionContext, env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/FileGateway/dist/worker.js";

const invoke = (path, init) => worker.fetch(
  new Request(`https://worker.test${path}`, init),
  env,
  createExecutionContext(),
);

const sha256 = async (text) => {
  const digest = await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text));
  return Array.from(new Uint8Array(digest), byte => byte.toString(16).padStart(2, "0")).join("");
};

describe("R2 file gateway scenario", () => {
  it("streams an upload through DigestStream and R2, then downloads its metadata", async () => {
    const key = `gateway/${crypto.randomUUID()}.txt`;
    const payload = "streamed payload";
    const uploaded = await invoke(`/files/${encodeURIComponent(key)}`, {
      method: "PUT",
      headers: { "content-type": "text/custom", "x-user": "Ada" },
      body: payload,
    });

    expect(uploaded.status).toBe(200);
    await expect(uploaded.json()).resolves.toMatchObject({
      key,
      size: payload.length,
      sha256: await sha256(payload),
    });

    const downloaded = await invoke(`/files/${encodeURIComponent(key)}`);
    expect(await downloaded.text()).toBe(payload);
    expect(downloaded.headers.get("content-type")).toBe("text/custom");
    expect(downloaded.headers.get("x-uploaded-by")).toBe("Ada");
    expect(downloaded.headers.get("x-r2-key")).toBe(key);

    const head = await invoke(`/files/${encodeURIComponent(key)}`, { method: "HEAD" });
    expect(head.status).toBe(200);
    expect(head.headers.get("content-length")).toBe(String(payload.length));

    const listed = await (await invoke(`/files?prefix=${encodeURIComponent("gateway/")}`)).json();
    expect(listed.objects.some(item => item.key === key)).toBe(true);

    expect((await invoke(`/files/${encodeURIComponent(key)}`, { method: "DELETE" })).status).toBe(204);
    expect((await invoke(`/files/${encodeURIComponent(key)}`)).status).toBe(404);
  });

  it("rejects missing bodies and unsupported routes and methods", async () => {
    expect((await invoke("/files/missing", { method: "PUT" })).status).toBe(400);
    expect((await invoke("/elsewhere")).status).toBe(404);
    const response = await invoke("/files/key", { method: "PATCH" });
    expect(response.status).toBe(405);
    expect(response.headers.get("allow")).toBe("GET, HEAD, PUT, DELETE");
  });

  it("follows native R2 cursors across multiple list pages", async () => {
    const prefix = `pages-${crypto.randomUUID()}/`;
    const keys = Array.from({ length: 105 }, (_, index) => `${prefix}${index}`);
    await Promise.all(keys.map(key => env.FILES.put(key, "value")));

    const listed = await (await invoke(`/files?prefix=${encodeURIComponent(prefix)}`)).json();
    expect(listed.count).toBe(105);
    expect(listed.objects.map(item => item.key).sort()).toEqual(keys.sort());

    await env.FILES.delete(keys);
  });
});
