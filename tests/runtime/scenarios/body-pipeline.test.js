import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/BodyPipeline/dist/worker.js";

const invoke = (path, init) => worker.fetch(new Request(`https://worker.test${path}`, init), {}, createExecutionContext());

describe("request body pipeline", () => {
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
      files: [{ field: "upload", name: "sample.bin", size: 4, type: "application/octet-stream", firstBytes: "000102fa" }],
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
