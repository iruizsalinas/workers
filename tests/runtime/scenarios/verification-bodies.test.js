import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/BodyPipeline/dist/worker.js";

const invoke = (path, body) => worker.fetch(new Request(`https://worker.test${path}`, {
  method: "POST", body,
}), {}, createExecutionContext());

describe("independent upload and signature verification", () => {
  it.each([new Uint8Array(), new Uint8Array([0, 128, 255, 1])])("hashes arbitrary binary bodies like native Web Crypto", async bytes => {
    const expected = Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes)),
      byte => byte.toString(16).padStart(2, "0")).join("").toUpperCase();
    const result = await (await invoke("/fingerprint", bytes)).json();
    expect(result.sha256).toBe(expected);
    expect(result.factoryCalls).toBe(1);
    expect(result.byteFactoryCalls).toBe(1);
  });

  it("consumes an upload with no chunks", async () => {
    const response = await invoke("/remainder", new ReadableStream({ start(controller) { controller.close(); } }));
    expect(await response.json()).toEqual({ first: "", remainder: "" });
  });

  it("consumes empty chunks followed by remaining bytes", async () => {
    const body = new ReadableStream({ start(controller) {
      for (const bytes of [new Uint8Array(), new Uint8Array([65]), new Uint8Array(), new Uint8Array([66])]) controller.enqueue(bytes);
      controller.close();
    } });
    expect(await (await invoke("/remainder", body)).json()).toEqual({ first: "", remainder: "AB" });
  });

  it("copies consumed bytes when an upstream producer reuses its buffer", async () => {
    const stream = () => {
      const buffer = new Uint8Array(1);
      let next = 65;
      return new ReadableStream({ pull(controller) {
        if (next === 68) { controller.close(); return; }
        buffer[0] = next++;
        controller.enqueue(buffer);
      } }, { highWaterMark: 0 });
    };
    const expected = await new Response(stream()).text();
    const result = await (await invoke("/remainder", stream())).json();
    expect(result.remainder).toBe(expected.slice(1));
  });

  it.each(["payload", "", "héllo"])("rejects invalid signatures without crashing for %s", async body => {
    const response = await worker.fetch(new Request("https://worker.test/verify", {
      method: "POST", body, headers: { "x-signature": "00".repeat(32) },
    }), {}, createExecutionContext());
    expect(await response.json()).toEqual({ valid: false });
  });
});
