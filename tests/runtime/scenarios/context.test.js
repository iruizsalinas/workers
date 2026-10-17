import { SELF } from "cloudflare:test";
import { describe, expect, it } from "vitest";

// Requests go through SELF so handlers receive the runtime's own ExecutionContext.
const invoke = (path) => SELF.fetch(`https://worker.test${path}`);

describe("execution context", () => {
  it("transports cyclic props containing compiler-managed collections", async () => {
    const response = await invoke("/graph-props");
    expect(response.status).toBe(200);
    expect(await response.json()).toBe(true);
  });

  it("preserves native props identity across repeated typed reads", async () => {
    const response = await invoke("/typed-props-identity");
    expect(response.status).toBe(200);
    expect(await response.json()).toBe(1);
  });

  it("sends compiler-managed dictionaries through native queue serialization", async () => {
    const response = await invoke("/queue-collections");
    expect(response.status).toBe(200);
    const result = await response.json();
    expect(typeof result.single).toBe("number");
    expect(typeof result.batch).toBe("number");
  });

  it("materializes typed props before calling methods on the C# class", async () => {
    const response = await invoke("/typed-props");
    expect(response.status).toBe(200);
    expect(await response.text()).toBe("acme:ada");
  });

  it("reaches entrypoints and Durable Objects through ctx.exports", async () => {
    const response = await invoke("/exports");
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ plain: "none:ada", scoped: "acme:ada", first: 1, peer: 1 });
  });

  it("runs callbacks inside spans and returns their results", async () => {
    const response = await invoke("/tracing");
    expect(response.status).toBe(200);
    const result = await response.json();
    expect(result.value).toBe(42);
    expect(typeof result.traced).toBe("boolean");
    expect(typeof result.sync).toBe("boolean");
    expect(typeof result.active).toBe("boolean");
  });

  it("reports the queue backlog after sends", async () => {
    const response = await invoke("/queue");
    expect(response.status).toBe(200);
    const result = await response.json();
    expect(typeof result.sendBacklog).toBe("number");
    expect(typeof result.batchBacklog).toBe("number");
  });

  it("exposes no Access context outside an Access application", async () => {
    const result = await (await invoke("/context")).json();
    expect(result.access).toBe(true);
    expect(typeof result.cache).toBe("boolean");
  });

  it("fails the invocation when ctx.abort is called", async () => {
    await expect(invoke("/abort").then((response) => response.status)).rejects.toThrow(/stopped by test/);
  });
});
