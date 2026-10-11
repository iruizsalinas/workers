import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";
import worker from "../fixtures/BoundedBodies/dist/worker.js";
import requestPolicy from "../../../examples/RequestPolicy/dist/worker.js";

const encode = value => new TextEncoder().encode(value);
const request = (path, body, headers = {}) => new Request(`https://worker.test${path}`, {
  method: "POST", body, headers: { origin: "https://app.example", ...headers },
});
const invoke = req => worker.fetch(req, {}, createExecutionContext());
const call = (path, body, headers) => invoke(request(path, body, headers));
function chunked(chunks, cancel) {
  let index = 0;
  return new ReadableStream({
    pull(controller) {
      if (index === chunks.length) controller.close();
      else controller.enqueue(chunks[index++]);
    },
    cancel,
  }, { highWaterMark: 0 });
}
async function expectOverflow(response) {
  expect(response.status).toBe(413);
  await expectEmptyError(response);
  expect(response.headers.get("x-body-policy")).toBe("bounded");
  expect(response.headers.get("access-control-allow-origin")).toBe("https://app.example");
}
async function expectEmptyError(response) {
  expect(response.body).toBeNull();
  expect(response.headers.get("content-type")).toBeNull();
  expect(await response.text()).toBe("");
}

describe("bounded body reads in workerd", () => {
  it.each(["json", "text", "bytes"])("accepts exactly the limit for %s", async kind => {
    const body = '{"text":"hello"}';
    const response = await call(`/${kind}/${encode(body).length}`, body);
    expect(response.status).toBe(200);
    if (kind === "json") expect(await response.json()).toEqual({ text: "hello" });
    else expect(await response.text()).toBe(body);
  });

  it.each(["json", "text", "bytes"])("rejects one byte over the limit for %s", async kind => {
    const body = '{"text":"hello"}';
    await expectOverflow(await call(`/${kind}/${encode(body).length - 1}`, body));
  });

  it.each(["json", "text", "bytes"])("counts streamed chunks without Content-Length for %s", async kind => {
    const bytes = encode('{"text":"hello"}');
    const body = () => chunked([bytes.subarray(0, 4), bytes.subarray(4, 9), bytes.subarray(9)]);
    const req = request(`/${kind}/${bytes.length}`, body());
    expect(req.headers.has("content-length")).toBe(false);
    const response = await invoke(req);
    expect(response.status).toBe(200);
    if (kind === "json") expect(await response.json()).toEqual({ text: "hello" });
    else expect(await response.text()).toBe('{"text":"hello"}');
    await expectOverflow(await call(`/${kind}/${bytes.length - 1}`, body()));
  });

  it.each(["json", "text", "bytes"])("counts UTF-8 bytes rather than characters for %s", async kind => {
    const body = '{"text":"é😀"}';
    expect(encode(body).length).toBeGreaterThan(body.length);
    await expectOverflow(await call(`/${kind}/${body.length}`, body));
    const bytes = encode(body);
    // Split inside both multi-byte characters.
    const stream = chunked(Array.from(bytes, byte => new Uint8Array([byte])));
    const response = await call(`/${kind}/${bytes.length}`, stream);
    expect(response.status).toBe(200);
    if (kind === "json") expect(await response.json()).toEqual({ text: "é😀" });
    else expect(await response.text()).toBe(body);
  });

  it("keeps malformed JSON at 400 only when the whole body fits", async () => {
    const response = await call("/json/4", "{bad");
    expect(response.status).toBe(400);
    await expectEmptyError(response);
    await expectOverflow(await call("/json/3", "{bad"));
  });

  it.each(["text", "bytes"])("accepts absent and empty bodies for %s", async kind => {
    for (const body of [undefined, "", chunked([])]) {
      const response = await call(`/${kind}/1`, body);
      expect(response.status).toBe(200);
      expect(await response.text()).toBe("");
    }
  });

  it("handles empty JSON and JSON null consistently", async () => {
    for (const body of [undefined, ""]) {
      const response = await call("/json/4", body);
      expect(response.status).toBe(400);
      await expectEmptyError(response);
    }
    expect(await (await call("/json/4", "null")).json()).toBeNull();
  });

  it.each(["json", "text", "bytes"])("rejects invalid limits without consuming the %s body", async kind => {
    for (const limit of [0, -1, -2147483648]) {
      const response = await call(`/invalid/${kind}/${limit}`, '{"text":"still readable"}');
      expect(await response.json()).toEqual({
        error: "maxBytes must be a positive 32-bit integer.", used: false, text: '{"text":"still readable"}',
      });
    }
  });

  it.each(["json", "text", "bytes"])("does not permit a second %s read", async kind => {
    const req = request(`/twice/${kind}/64`, '{"text":"hello"}');
    const response = await invoke(req);
    expect(response.status).toBe(500);
    await expectEmptyError(response);
    expect(req.bodyUsed).toBe(true);
    expect(req.body.locked).toBe(false);
  });

  it("does not allocate the configured maximum for a small input", async () => {
    const response = await call("/text/2147483647", "small");
    expect(response.status).toBe(200);
    expect(await response.text()).toBe("small");
  });

  it("grows the buffer across chunks and ignores empty chunks", async () => {
    const bytes = new Uint8Array(10000).fill(97);
    const response = await call("/bytes/10000", chunked([
      new Uint8Array(0), bytes.subarray(0, 4000), bytes.subarray(4000, 8000), bytes.subarray(8000),
    ]));
    expect(response.status).toBe(200);
    expect(new Uint8Array(await response.arrayBuffer())).toEqual(bytes);
  });

  it("preserves UTF-8 BOM and replacement-character behavior of native text reads", async () => {
    const bytes = new Uint8Array([0xef, 0xbb, 0xbf, 0x61, 0xff]);
    const native = await request("/", bytes).text();
    expect(await (await call("/text/5", chunked(Array.from(bytes, byte => new Uint8Array([byte]))))).text()).toBe(native);
    await expectOverflow(await call("/text/4", bytes));
  });
});

describe("bounded read security and stream cleanup", () => {
  it.each(["1", "999999", "garbage"])("enforces actual bytes regardless of Content-Length: %s", async contentLength => {
    // Direct workerd Request construction permits headers that its HTTP ingress may reject.
    await expectOverflow(await call("/text/4", chunked([encode("12345")]), { "content-length": contentLength }));
    const response = await call("/text/4", chunked([encode("1234")]), { "content-length": contentLength });
    expect(response.status).toBe(200);
    expect(await response.text()).toBe("1234");
  });

  it.each(["json", "text", "bytes"])("cancels on overflow, stops reading, and releases the lock for %s", async kind => {
    const cancel = vi.fn();
    let pulls = 0;
    const body = new ReadableStream({
      pull(controller) { pulls++; controller.enqueue(new Uint8Array(4)); },
      cancel,
    }, { highWaterMark: 0 });
    const req = request(`/${kind}/4`, body);
    await expectOverflow(await invoke(req));
    expect(pulls).toBe(2);
    expect(cancel).toHaveBeenCalledOnce();
    expect(cancel.mock.calls[0][0].status).toBe(413);
    expect(req.bodyUsed).toBe(true);
    expect(body.locked).toBe(false);
  });

  it.each(["reject", "pending"])("does not mask or delay 413 when cancellation is %s", async behavior => {
    const cancel = vi.fn(() => behavior === "reject"
      ? Promise.reject(new Error("secret cancellation failure")) : new Promise(() => {}));
    const body = chunked([encode("12345")], cancel);
    await expectOverflow(await call("/text/4", body));
    expect(cancel).toHaveBeenCalledOnce();
    expect(body.locked).toBe(false);
  });

  it("releases the lock on success", async () => {
    const body = chunked([encode("1234")]);
    const response = await call("/text/4", body);
    expect(await response.text()).toBe("1234");
    expect(body.locked).toBe(false);
  });

  it("preserves body-read failures as safe 500 errors", async () => {
    const body = new ReadableStream({ pull(controller) { controller.error(new Error("secret stream failure")); } });
    const response = await call("/json/4", body);
    expect(response.status).toBe(500);
    await expectEmptyError(response);
    expect(body.locked).toBe(false);
  });

  it("rejects non-byte chunks without retaining the reader lock", async () => {
    const cancel = vi.fn();
    const body = chunked(["not bytes"], cancel);
    const response = await call("/text/4", body);
    expect(response.status).toBe(500);
    expect(cancel).toHaveBeenCalledOnce();
    expect(body.locked).toBe(false);
  });

  it("does not take ownership of an already locked stream", async () => {
    const body = chunked([encode("1234")]);
    const reader = body.getReader();
    try {
      const response = await call("/text/4", body);
      expect(response.status).toBe(500);
      expect(body.locked).toBe(true);
    } finally { reader.releaseLock(); }
  });
});

describe("request-policy example with bounded JSON", () => {
  const reading = '{"deviceId":"sensor-17","value":21.5,"tags":["lab"]}';
  const forward = (body, headers = {}) => requestPolicy.fetch(request("/v1/readings", body, {
    "content-type": "application/json", ...headers,
  }), { ORIGIN: "https://telemetry.example" }, createExecutionContext());

  it("forwards streamed JSON without Content-Length", async () => {
    const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({ stored: true }));
    const response = await forward(chunked([encode(reading.slice(0, 10)), encode(reading.slice(10))]));
    expect(response.status).toBe(200);
    expect(fetch).toHaveBeenCalledOnce();
    expect(fetch.mock.calls[0][1].headers.get("content-length")).toBeNull();
  });

  it("accepts an exact 4096-byte JSON body and removes the stale length when forwarding", async () => {
    const fetch = vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({ stored: true }));
    const body = reading.padEnd(4096, " ");
    const response = await forward(body, { "content-length": "4096" });
    expect(response.status).toBe(200);
    expect(fetch.mock.calls[0][1].headers.get("content-length")).toBeNull();
  });

  it.each([undefined, "1"])("rejects an oversized streamed JSON body before forwarding (length %s)", async length => {
    const fetch = vi.spyOn(globalThis, "fetch");
    const response = await forward(chunked([encode(reading), encode(" ".repeat(4097 - reading.length))]),
      length === undefined ? {} : { "content-length": length });
    expect(response.status).toBe(413);
    expect(await response.json()).toEqual({ error: "Request body too large", requestId: expect.any(String) });
    expect(fetch).not.toHaveBeenCalled();
  });

  it.each(["application/json-evil", "text/plain", ""])("rejects unsupported content types: %s", async contentType => {
    const fetch = vi.spyOn(globalThis, "fetch");
    const response = await forward(reading, { "content-type": contentType });
    expect(response.status).toBe(415);
    expect(fetch).not.toHaveBeenCalled();
  });

  it("allows media-type casing and charset parameters", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(Response.json({ stored: true }));
    const response = await forward(reading, { "content-type": "Application/JSON; charset=utf-8" });
    expect(response.status).toBe(200);
  });
});
