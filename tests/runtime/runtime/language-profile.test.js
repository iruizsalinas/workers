import { createExecutionContext } from "cloudflare:test";
import { expect, it } from "vitest";
import worker from "../fixtures/LanguageProfile/dist/worker.js";

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
