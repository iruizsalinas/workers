import { createExecutionContext } from "cloudflare:test";
import { expect, it } from "vitest";
import worker from "../fixtures/LanguageProfile/dist/worker.js";

it("captures explicit params arrays once and spreads every value into binding calls", async () => {
  const calls = [];
  const statement = { bind(...values) { calls.push(values); return this; } };
  const env = { DB: { prepare() { return statement; } } };
  const response = await worker.fetch(new Request("https://worker.test/binding-params"), env, createExecutionContext());
  await expect(response.json()).resolves.toEqual({ count: 1 });
  expect(calls).toEqual([[1, 2]]);
});

it("throws typed null-reference errors after arguments without misclassifying platform errors", async () => {
  const response = await worker.fetch(new Request("https://worker.test/null-dereferences"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    read: true, call: true, add: true, invoke: true, nullArguments: 3, platformIsNull: false,
  });
});

it("preserves predicate mutation rules, set enumeration, serialization, and record equality after JSON reads", async () => {
  const response = await worker.fetch(new Request("https://worker.test/predicates"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    found: 2, every: false, invalidated: true, predicateNull: true, set: [1, 2, 3], recordCount: 1, contains: true,
  });
});

it("preserves foreach boxing, casts, and single evaluation during formatting", async () => {
  const response = await worker.fetch(new Request("https://worker.test/enumeration"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    boxed: "(1, 2)", text: ["Ready", "[key, 3]"], sum: 5, invalidCast: true,
    tupleText: "(1, 2)", formattingCalls: 1,
  });
});

it("copies tuple values across collection boundaries and wraps comparer failures", async () => {
  const response = await worker.fetch(new Request("https://worker.test/collections"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    list: 1, expression: 1, copied: 3, slice: 3, added: 3, projected: [8], wrapped: true, inner: true,
  });
});

it("preserves captures, patterns, compound assignments, queries, initializer storage, types, and exception catches", async () => {
  const response = await worker.fetch(new Request("https://worker.test/correctness"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    captures: [3, 3, 3], pattern: "match", text: "cached", flags: 10, query: [4, 6],
    invalid: true, format: true, missing: true, type: "PatternValue",
  });
});

it("combines composite keys, user indexers, events, and static abstract interface factories", async () => {
  const response = await worker.fetch(new Request("https://worker.test/composite"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({ value: 7, total: 10, count: 1, contains: true, factory: 10 });
});

it("combines local functions, deconstruction, params, interpolation, ranges, and primary constructors", async () => {
  const response = await worker.fetch(new Request("https://worker.test/features"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    greeting: "Hello, Ada",
    low: 1,
    high: 7,
    middle: [3],
    sum: 8,
    calls: 1,
    text: "Success { Kind = success, Value = 8 }",
    nullableText: "Ready",
    invalidRange: true,
  });
});

it("preserves default interface dispatch, inherited JSON, copy arguments, and null record equality", async () => {
  const response = await worker.fetch(new Request("https://worker.test/semantics"), {}, createExecutionContext());
  await expect(response.json()).resolves.toEqual({
    identity: 7,
    helper: 3,
    label: "inherited",
    copied: 13,
    bothNull: true,
  });
});

it("combines generic JSON, extensions, interfaces, inheritance, out helpers, and constructor chaining", async () => {
  const response = await worker.fetch(new Request("https://worker.test/", {
    method: "POST",
    headers: { "content-type": "application/json" },
    body: JSON.stringify({ items: [{ name: " Ada " }] }),
  }), {}, createExecutionContext());
  expect(response.status).toBe(200);
  await expect(response.json()).resolves.toEqual({ kind: "success", user: { name: "Hello, Ada" } });
});
