import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/Crypto/dist/worker.js";

const payload = new TextEncoder().encode("héllo crypto");
const hex = bytes => Array.from(new Uint8Array(bytes), byte => byte.toString(16).padStart(2, "0")).join("");
const invoke = (path, algorithm, body = payload) => worker.fetch(new Request(
  `https://worker.test${path}?algorithm=${encodeURIComponent(algorithm)}`,
  { method: "POST", body },
), {}, createExecutionContext());
async function json(path, algorithm, body) {
  const response = await invoke(path, algorithm, body);
  const text = await response.text();
  expect(response.status, text).toBe(200);
  return JSON.parse(text);
}

describe("native Web Crypto bindings", () => {
  it.each(["SHA-1", "SHA-256", "SHA-384", "SHA-512", "MD5"])("hashes binary and UTF-8 text with %s", async algorithm => {
    const result = await json("/digest", algorithm);
    expect(result.bytes).toBe(hex(await crypto.subtle.digest(algorithm, payload)));
    expect(result.subtle).toBe(result.bytes);
    expect(result.text).toBe(hex(await crypto.subtle.digest(algorithm, new TextEncoder().encode("héllo"))));
  });

  it.each(["AES-GCM", "AES-CBC", "AES-CTR", "RSA-OAEP"])("encrypts, decrypts, and imports JWK keys with %s", async algorithm => {
    const result = await json("/encrypt", algorithm);
    expect(result.decrypted).toBe(hex(payload));
    expect(result.encrypted).not.toBe(result.decrypted);
    expect(result.imported).toBe(true);
    expect(result.usages).toEqual(["decrypt"]);
    expect(result.name).toBe(algorithm);
  });

  it.each(["HMAC", "RSASSA-PKCS1-v1_5", "RSA-PSS", "ECDSA", "Ed25519", "NODE-ED25519", "ML-DSA-44", "ML-DSA-65", "ML-DSA-87"])(
    "signs, verifies, and round-trips JWK and binary keys with %s", async algorithm => {
      const result = await json("/sign", algorithm);
      expect(result).toMatchObject({ valid: true, invalid: false, importedValid: true, binaryValid: true, extractable: true });
      expect(result.jwk.key_ops).toContain("verify");
      const key = await crypto.subtle.importKey("jwk", result.jwk,
        { name: algorithm, hash: "SHA-256", namedCurve: algorithm === "NODE-ED25519" ? "NODE-ED25519" : "P-256" }, true, ["verify"]);
      const signature = new Uint8Array(result.signature.match(/../g).map(byte => parseInt(byte, 16)));
      expect(await crypto.subtle.verify({ name: algorithm, hash: "SHA-256", saltLength: 32 }, key, signature, payload)).toBe(true);
      if (algorithm.startsWith("RSA")) expect(result.publicExponent).toBe("010001");
    },
  );

  it.each(["HKDF", "PBKDF2", "ECDH", "X25519"])("derives bits and reusable keys with %s", async algorithm => {
    const result = await json("/derive", algorithm);
    expect(result).toMatchObject({ same: true, peers: true, length: 256 });
    if (algorithm === "HKDF" || algorithm === "PBKDF2") {
      const key = await crypto.subtle.importKey("raw", new TextEncoder().encode("password"), algorithm, false, ["deriveBits"]);
      const expected = await crypto.subtle.deriveBits({ name: algorithm, hash: "SHA-256", salt: new TextEncoder().encode("salt"), info: new TextEncoder().encode("info"), iterations: 2 }, key, 256);
      expect(result.bits).toBe(hex(expected));
    }
  });

  it("preserves ML-DSA signature context bytes", async () => {
    const result = await json("/sign-context", "ML-DSA-44");
    expect(result).toMatchObject({ valid: true, invalid: false, importedValid: true, binaryValid: true });
    const key = await crypto.subtle.importKey("jwk", result.jwk, "ML-DSA-44", false, ["verify"]);
    const signature = new Uint8Array(result.signature.match(/../g).map(byte => parseInt(byte, 16)));
    expect(await crypto.subtle.verify({ name: "ML-DSA-44", context: new Uint8Array([1, 2, 3]) }, key, signature, payload)).toBe(true);
    expect(await crypto.subtle.verify("ML-DSA-44", key, signature, payload)).toBe(false);
  });

  it.each(["AES-GCM", "AES-CBC", "AES-CTR"])("wraps and unwraps JWK serialization with %s", async algorithm => {
    expect(await json("/wrap-jwk", algorithm)).toEqual({ same: true, extractable: true, usages: ["encrypt", "decrypt"] });
  });

  it("preserves native AES-KW alignment requirements for JWK wrapping", async () => {
    const response = await invoke("/wrap-jwk", "AES-KW");
    expect(response.status).toBe(400);
    expect(await response.text()).toContain("multiple of 64 bits");
  });

  it.each(["AES-KW", "AES-GCM", "AES-CBC", "AES-CTR", "RSA-OAEP"])("wraps and unwraps keys with %s", async algorithm => {
    expect(await json("/wrap", algorithm)).toEqual({ same: true, extractable: true, usages: ["encrypt", "decrypt"] });
  });

  it.each(["ML-KEM-768", "ML-KEM-1024"])("encapsulates bits and keys with %s", async algorithm => {
    const result = await json("/kem", algorithm);
    expect(result).toMatchObject({ sameBits: true, sameKey: true, publicType: "public", supported: true, supportedKey: true, unsupported: false, supportedDigest: true });
    expect(result.jwk.kty).toBe("AKP");
    expect(result.jwk.pub).toBeTypeOf("string");
  });

  it.each(["SHA-256", "MD5", "CRC32", "CRC32C", "CRC64NVME"])("streams %s with byte counts and disposal", async algorithm => {
    const result = await json("/stream", algorithm);
    const stream = new crypto.DigestStream(algorithm);
    const writer = stream.getWriter();
    await writer.write(payload);
    await writer.close();
    expect(result).toEqual({ hex: hex(await stream.digest), bytesWritten: payload.length, same: true });
    stream[Symbol.dispose]();
  });

  it.each([0, 16, 65536])("fills a caller-owned buffer of %i bytes", async count => {
    const response = await worker.fetch(new Request(`https://worker.test/random?count=${count}`), {}, createExecutionContext());
    expect(await response.json()).toEqual({ count, same: true, sameReference: true });
  });

  it("preserves the native random quota", async () => {
    const response = await worker.fetch(new Request("https://worker.test/random?count=65537"), {}, createExecutionContext());
    expect(response.status).toBe(400);
    expect(await response.text()).toContain("65537");
  });

  it.each(["usage", "export", "timing", "family"])("rejects invalid %s operations", async scenario => {
    const response = await worker.fetch(new Request(`https://worker.test/errors?case=${scenario}`), {}, createExecutionContext());
    expect(response.status).toBe(400);
    expect(await response.text()).not.toBe("");
  });

  it.each([
    ["RSASSA-PKCS1-v1_5", "pkcs8"], ["RSA-PSS", "pkcs8"], ["ECDSA", "pkcs8"],
    ["Ed25519", "pkcs8"], ["X25519", "pkcs8"], ["ECDH", "spki"],
    ["Ed25519", "raw-public"], ["X25519", "raw-public"],
    ["ML-DSA-44", "raw-public"], ["ML-DSA-44", "raw-seed"],
  ])("round-trips %s keys in %s format", async (algorithm, format) => {
    const response = await worker.fetch(new Request(`https://worker.test/format?algorithm=${algorithm}&format=${format}`), {}, createExecutionContext());
    const text = await response.text();
    expect(response.status, text).toBe(200);
    expect(JSON.parse(text)).toMatchObject({ same: true, supported: true });
  });

  it.each([["Ed25519", "raw-private"], ["Ed25519", "raw-seed"], ["X25519", "raw-private"], ["ML-DSA-44", "raw-private"]])(
    "preserves native rejection of %s in %s format", async (algorithm, format) => {
      const response = await worker.fetch(new Request(`https://worker.test/format?algorithm=${algorithm}&format=${format}`), {}, createExecutionContext());
      expect(response.status).toBe(400);
      expect(await response.text()).toContain(format);
    },
  );

  it("handles split surrogate pairs and lone surrogates in digest streams", async () => {
    const result = await json("/stream-text", "SHA-256");
    const bytes = new TextEncoder().encode("😀\ufffd");
    expect(result).toEqual({ hex: hex(await crypto.subtle.digest("SHA-256", bytes)), count: bytes.length });
  });

  it.each(["abort", "writer", "dispose"])("rejects the pending digest on stream %s", async scenario => {
    const response = await worker.fetch(new Request(`https://worker.test/stream-error?case=${scenario}`), {}, createExecutionContext());
    expect(await response.json()).toEqual({ rejected: true });
  });
});
