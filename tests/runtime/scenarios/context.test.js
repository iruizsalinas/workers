import { SELF } from "cloudflare:test";
import { describe, expect, it } from "vitest";

// Requests go through SELF so handlers receive the runtime's own ExecutionContext.
const invoke = (path) => SELF.fetch(`https://worker.test${path}`);

describe("execution context", () => {
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
