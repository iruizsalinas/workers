import { createExecutionContext, env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/RuntimeProfile/dist/worker.js";

const invoke = (path, body) => worker.fetch(new Request(`https://worker.test${path}`, {
  method: body === undefined ? "GET" : "POST", body,
}), env, createExecutionContext());

describe("Workers runtime profile", () => {
  it("reads ordinary JavaScript JSON values, including null and missing properties", async () => {
    const input = { name: "Ada", number: 2.5, enabled: true, empty: null, items: [1, 2] };
    expect(await (await invoke("/json", JSON.stringify(input))).json()).toEqual({
      name: "Ada", number: 2.5, enabled: true, explicitNull: true, missing: true,
      hasMissing: false, count: 2, serialized: JSON.stringify(input),
    });
  });

  it("uses one JSON contract across HTTP, KV, and standalone conversion", async () => {
    const user = { user_name: "Ada", count: 3 };
    expect(await (await invoke("/typed-json", JSON.stringify(user))).json()).toEqual({
      name: "Ada:3", serialized: JSON.stringify(user), user,
    });
    expect(await env.KV.get("native-user", "json")).toEqual(user);
  });

  it("keeps JavaScript date units, zero-based months, and invalid-date behavior", async () => {
    const date = new Date(Date.UTC(2024, 1, 29, 23, 58, 57, 123));
    const original = date.toISOString();
    date.setUTCMonth(2);
    expect(await (await invoke("/date")).json()).toEqual({
      original, date: date.toISOString(), year: date.getUTCFullYear(), month: date.getUTCMonth(),
      day: date.getUTCDate(), timestamp: date.getTime(), parsed: Date.parse(original), invalid: null,
      epoch: new Date(0).toISOString(),
    });
  });

  it("hydrates nested dates without coercing JSON scalar values or folding property names", async () => {
    const input = {
      enabled: "true", count: "2", at: "2024-02-29T00:00:00Z",
      children: [{ Enabled: true, count: 3, at: "2024-03-01T00:00:00Z", children: [] }],
    };
    expect(await (await invoke("/typed-values", JSON.stringify(input))).json()).toEqual({
      enabled: "true", count: "2", at: "2024-02-29T00:00:00.000Z",
      children: [{ enabled: false, count: 3, at: "2024-03-01T00:00:00.000Z", children: [] }],
    });
  });

  it("uses native regex captures, flags, replacement syntax, and lastIndex", async () => {
    expect(await (await invoke("/regex")).json()).toEqual({
      first: "one", group: "one", index: 0, nextIndex: 3, second: "two",
      source: "(?<word>[a-z]+)", flags: "g", replaced: "one! two!",
      split: ["a", "b", "c"], unicode: true, noMatch: true,
    });
  });

  it("preserves abort reasons and rejects an aborted scheduler wait", async () => {
    expect(await (await invoke("/abort")).json()).toEqual({
      aborted: true, reason: "stopped", threw: true, waitRejected: true, combined: true,
    });
  });

  it("uses native text, URL, and UUID operations", async () => {
    const result = await (await invoke("/text")).json();
    expect(result).toMatchObject({
      decoded: "hello 😀", fromBase64: "hello 😀", fromHex: "hello 😀",
      hex: "68656c6c6f20f09f9880", encoded: encodeURIComponent("a b!*"), decodedUrl: "a b",
    });
    expect(result.uuid).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/);
  });

  it("retains ordinary C# computation, collections, LINQ, and async composition", async () => {
    expect(await (await invoke("/foundation")).json()).toEqual({
      sorted: [1, 2, 3], sum: 8, specialKey: 7, tasks: [9, 16], queued: 10,
    });
  });

  it("uses JavaScript number parsing, formatting, and rounding", async () => {
    const result = await (await invoke("/numbers")).json();
    expect(result).toMatchObject({
      rounded: Math.round(2.5), negative: Math.round(-1.5), fixedText: (2.675).toFixed(2),
      precision: (12.345).toPrecision(4), radix: (255).toString(16),
      partialInt: 12, partialFloat: 1.25, nan: true, nullNumber: 0, safe: true, invalidSign: null,
    });
    expect(result.random).toBeGreaterThanOrEqual(0);
    expect(result.random).toBeLessThan(1);
  });
});
