import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import compilerSemantics from "../fixtures/CompilerSemantics/dist/worker.js";
import clrSemantics from "../generated/differential.json";

const invoke = (path, init = {}) => compilerSemantics.fetch(
  new Request(`https://worker.test${path}`, typeof init === "string" ? { method: init } : init),
  {},
  createExecutionContext(),
);

describe("compiler value semantics", () => {
  it("matches the CLR for the shared core-semantics corpus", async () => {
    const response = await invoke("/differential");

    await expect(response.json()).resolves.toEqual(clrSemantics);
  });

  it("preserves collection initializer elements", async () => {
    const response = await invoke("/collections");

    await expect(response.json()).resolves.toEqual({
      count: 3,
      total: 12,
      updated: 5,
      readRejected: true,
      writeRejected: true,
      specialKey: 7,
      dictionaryCount: 2,
      missingKeyRejected: true,
    });
  });

  it("binds reordered record arguments by name", async () => {
    const response = await invoke("/records");

    await expect(response.json()).resolves.toEqual({ label: "priority", count: 3 });
  });

  it("preserves null-coalescing precedence", async () => {
    const response = await invoke("/coalesce", "PUT");

    await expect(response.json()).resolves.toEqual({ accepted: true });
  });

  it("binds reordered native constructor arguments by name", async () => {
    const response = await invoke("/constructors");

    await expect(response.json()).resolves.toEqual({
      method: "POST",
      rewritten: "/reordered",
      resolved: "https://worker.test/root/child",
    });
  });

  it("matches strict .NET parsing and escaping semantics", async () => {
    const response = await invoke("/conversions");

    await expect(response.json()).resolves.toEqual({
      parsed: 42,
      hex: "00FF",
      escaped: "%21%2A%27%28%29",
      invalidIntegerRejected: true,
      invalidHexRejected: true,
    });
  });

  it("inlines user enum members and qualified constants", async () => {
    const response = await invoke("/constants");

    await expect(response.json()).resolves.toEqual({ state: 1, ready: true, limit: 25 });
  });

  it("uses the same round-trip timestamp format for both C# forms", async () => {
    const response = await invoke("/timestamps");
    const result = await response.json();

    expect(result.interpolated).toBe(result.explicitFormat);
    expect(result.interpolated).toMatch(/\.\d{7}\+00:00$/);
  });

  it("rejects fixed-duration arithmetic outside the CLR date range", async () => {
    const response = await invoke("/date-range");

    await expect(response.json()).resolves.toEqual({ rejected: true });
  });

  it("enforces First and Single sequence cardinality", async () => {
    const response = await invoke("/linq-errors");

    await expect(response.json()).resolves.toEqual({
      emptyRejected: true,
      multipleRejected: true,
      nullSourceRejected: true,
      indexRejected: true,
      emptyAggregateRejected: true,
      emptyAverageRejected: true,
      duplicateKeyRejected: true,
      invalidChunkRejected: true,
      sumOverflowRejected: true,
    });
  });

  it("enforces TimeSpan range and MinValue arithmetic", async () => {
    const response = await invoke("/timespan-errors");

    await expect(response.json()).resolves.toEqual({
      factoryRejected: true,
      additionRejected: true,
      negationRejected: true,
      durationRejected: true,
    });
  });

  it("matches CLR validation for delay and string operations", async () => {
    const response = await invoke("/bcl-errors");

    await expect(response.json()).resolves.toEqual({
      delayRejected: true,
      substringRejected: true,
      nullSearchRejected: true,
      emptyReplacementRejected: true,
      removeRejected: true,
      insertRejected: true,
      paddingRejected: true,
      characterRangeRejected: true,
      searchRangeRejected: true,
      integerParseRejected: true,
      unsignedParseRejected: true,
      booleanParseRejected: true,
      absoluteRejected: true,
      clampRejected: true,
      roundRejected: true,
      signRejected: true,
      guidParseRejected: true,
      nullInstanceEqualsRejected: true,
      equalsArgumentEvaluated: true,
      staticNullEquals: true,
      nonNullInstanceEqualsNull: false,
      clrTrim: "value",
      byteOrderMarkPreserved: "\uFEFFvalue\uFEFF",
    });
  });

  it.each([
    ["string", "hello", "hello"],
    ["boolean", true, "True"],
    ["null", null, ""],
    ["object", { answer: 42 }, '{"answer":42}'],
  ])("formats a JSON %s like JsonElement.ToString", async (_kind, input, expected) => {
    const response = await invoke("/json-element-text", {
      method: "POST",
      body: JSON.stringify(input),
    });

    await expect(response.text()).resolves.toBe(expected);
  });

  it("supports native JSON serialization and JsonElement inspection", async () => {
    const response = await invoke("/json-api", {
      method: "POST",
      body: JSON.stringify({ text: "hello", flag: true, values: [2, 3] }),
    });

    await expect(response.json()).resolves.toEqual({
      isObject: true,
      text: "hello",
      flag: true,
      first: 2,
      approximate: 3,
      length: 2,
      sum: 5,
      name: "Ada",
      encoded: '{"display-name":"Ada"}',
      roundTripOk: true,
    });
  });

  it("validates JsonElement kinds, properties, and indexes", async () => {
    const response = await invoke("/json-errors", {
      method: "POST",
      body: JSON.stringify({ values: [1] }),
    });

    await expect(response.json()).resolves.toEqual({
      missingPropertyRejected: true,
      wrongKindRejected: true,
      indexRejected: true,
    });
  });

  it("maps cancellation tokens to abort signals", async () => {
    const response = await invoke("/cancellation", { method: "POST", body: "unused" });

    await expect(response.json()).resolves.toEqual({
      delayCanceled: true,
      cancellationDisabled: true,
      throwRejected: true,
      bodyRejected: true,
      fetchRejected: true,
      canBeCanceled: true,
      isCancellationRequested: true,
      noneCanBeCanceled: false,
      defaultsEqual: true,
    });
  });

  it("executes synchronous C# iterators as JavaScript generators", async () => {
    const response = await invoke("/sync-iterator");

    await expect(response.json()).resolves.toEqual({ total: 3 });
  });

  it("preserves receiver-first evaluation for named Response instance calls", async () => {
    const response = await invoke("/response-order");

    await expect(response.json()).resolves.toEqual({
      events: ["receiver", "value", "name"],
      header: "set",
    });
  });

  it("runs user classes, records, initializers, methods, and computed properties", async () => {
    const response = await invoke("/user-types");

    await expect(response.json()).resolves.toEqual({
      value: 5,
      doubled: 10,
      label: "items",
      fullName: "Ada Lovelace",
      greeting: "Hello, Ada Lovelace",
      age: 36,
      counter: { label: "items", doubled: 10 },
      person: {
        first: "Ada",
        last: "Lovelace",
        age: 36,
        fullName: "Ada Lovelace",
      },
    });
  });

  it("keeps prototype-sensitive C# members as ordinary own data", async () => {
    const response = await invoke("/prototype-safety");
    const body = await response.json();

    expect(Object.hasOwn(body, "__proto__")).toBe(true);
    expect(body["__proto__"]).toBe("anonymous");
    expect(Object.hasOwn(body.model, "__proto__")).toBe(true);
    expect(body.model["__proto__"]).toBe("class");
    expect(Object.getPrototypeOf(body)).toBe(Object.prototype);
    expect(Object.getPrototypeOf(body.model)).toBe(Object.prototype);
  });

  it("runs the focused StringBuilder profile", async () => {
    const response = await invoke("/string-builder");

    await expect(response.json()).resolves.toEqual({
      text: "[egin:😀True42\nend\n!!a,,bx-y",
      length: 28,
      sameAfterClear: true,
      reset: "reset\u0000\u0000",
      resetLength: 7,
    });
  });
});
