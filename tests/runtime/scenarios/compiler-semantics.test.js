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

    await expect(response.json()).resolves.toEqual({ count: 3, total: 12 });
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
});
