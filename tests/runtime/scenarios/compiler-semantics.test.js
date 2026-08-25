import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import compilerSemantics from "../fixtures/CompilerSemantics/dist/worker.js";

const invoke = (path, method = "GET") => compilerSemantics.fetch(
  new Request(`https://worker.test${path}`, { method }),
  {},
  createExecutionContext(),
);

describe("compiler value semantics", () => {
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
});
