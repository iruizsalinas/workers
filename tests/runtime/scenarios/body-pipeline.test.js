import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/BodyPipeline/dist/worker.js";

const invoke = (path, init) => worker.fetch(new Request(`https://worker.test${path}`, init), {}, createExecutionContext());

describe("request body pipeline", () => {
  it("fingerprints empty and populated uploads without evaluating body factories twice", async () => {
    const digest = async text => Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text))),
      byte => byte.toString(16).padStart(2, "0")).join("").toUpperCase();
    const emptyDigest = await digest("");
    const generatedDigest = await digest("generated");
    for (const body of [undefined, "", "héllo"]) {
      const response = await invoke("/fingerprint", { method: "POST", body });
      await expect(response.json()).resolves.toEqual({
        sha256: await digest(body ?? ""), emptySha256: emptyDigest, generatedSha256: generatedDigest,
        byteSha256: generatedDigest, factoryCalls: 1, byteFactoryCalls: 1,
      });
    }
  });

  it("cancels an upload after reading a bounded preview", async () => {
    let cancelled = false;
    const body = new ReadableStream({
      start(controller) { controller.enqueue(new TextEncoder().encode("preview")); },
      cancel() { cancelled = true; },
    });
    const response = await invoke("/preview", { method: "POST", body });
    await expect(response.json()).resolves.toEqual({ done: false, preview: "preview" });
    expect(cancelled).toBe(true);
  });

  it("consumes the remaining upload bytes after inspecting the first chunk", async () => {
    const body = new ReadableStream({
      start(controller) {
        controller.enqueue(new TextEncoder().encode("id,name\n"));
        controller.enqueue(new TextEncoder().encode("1,one\n"));
        controller.enqueue(new TextEncoder().encode("2,two\n"));
        controller.close();
      },
    });
    const response = await invoke("/remainder", { method: "POST", body });
    await expect(response.json()).resolves.toEqual({ first: "id,name\n", remainder: "1,one\n2,two\n" });
  });

  it("captures signed upload data when verification starts", async () => {
    const body = "signed payload";
    const key = await crypto.subtle.importKey("raw", new TextEncoder().encode("body-signing-secret"),
      { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
    const signature = Array.from(new Uint8Array(await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(body))),
      byte => byte.toString(16).padStart(2, "0")).join("");
    const response = await invoke("/verify", { method: "POST", body, headers: { "x-signature": signature } });
    await expect(response.json()).resolves.toEqual({ valid: true });
  });

  it("reads multipart fields and file metadata with a bounded preview", async () => {
    const form = new FormData();
    form.append("title", "example");
    form.append("upload", new File([new Uint8Array([0, 1, 2, 250])], "sample.bin", {
      type: "application/octet-stream", lastModified: 1234,
    }));
    const response = await invoke("/form", { method: "POST", body: form });
    const result = await response.json();
    expect(result).toMatchObject({
      fields: [{ name: "title", value: "example" }],
      files: [{ field: "upload", name: "sample.bin", size: 4, type: "application/octet-stream", firstBytes: "000102FA" }],
    });
    expect(result.files[0].lastModified).toBeGreaterThan(0);
  });

  it("clones a request and independently consumes both bodies", async () => {
    const response = await invoke("/clone", { method: "POST", body: "héllo", headers: { "x-test": "yes" } });
    const result = await response.json();
    expect(result).toMatchObject({ text: "héllo", byteLength: 6, bodyUsed: true, cloneBodyUsed: true });
    expect(result.headers).toContain("x-test:yes");
  });

  it("decompresses request streams and emits compressed async streams", async () => {
    const compressed = new Blob(["compressed input"]).stream().pipeThrough(new CompressionStream("gzip"));
    const decompressed = await invoke("/decompress", { method: "POST", body: compressed });
    await expect(decompressed.json()).resolves.toEqual({ decompressed: "compressed input" });

    const response = await invoke("/stream?count=3", { headers: { "accept-encoding": "gzip" } });
    expect(response.headers.get("content-encoding")).toBe("gzip");
    const text = await new Response(response.body.pipeThrough(new DecompressionStream("gzip"))).text();
    const lines = text.trim().split("\n").map(JSON.parse);
    expect(lines).toHaveLength(3);
    expect(lines.map(line => line.index)).toEqual([0, 1, 2]);
  });
});
