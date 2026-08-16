import { env, runDurableObjectAlarm } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../fixtures/BatchAccumulator/dist/worker.js";

const invoke = (path, init) => worker.fetch(new Request(`https://worker.test${path}?bucket=suite`, init), env, {});

describe("SQLite Durable Object accumulator", () => {
  it("commits synchronous SQL/KV transactions and processes an alarm", async () => {
    expect((await invoke("/add", { method: "POST", body: JSON.stringify({ amount: 7 }) })).status).toBe(202);
    expect((await invoke("/add", { method: "POST", body: JSON.stringify({ amount: -2 }) })).status).toBe(202);

    const before = await (await invoke("/state")).json();
    expect(before).toMatchObject({ total: 0, batches: 0, metadataCount: 2 });
    expect(before.pending.map(row => row.amount)).toEqual([7, -2]);
    expect(before.databaseSize).toBeGreaterThan(0);

    const stub = env.ACCUMULATORS.getByName("suite");
    expect(await runDurableObjectAlarm(stub)).toBe(true);
    const after = await (await invoke("/state")).json();
    expect(after).toMatchObject({ total: 5, batches: 1, pending: [], metadataCount: 0 });
  });

  it("validates input and resets state", async () => {
    expect((await invoke("/add", { method: "POST", body: JSON.stringify({ amount: 0 }) })).status).toBe(400);
    expect((await invoke("/reset", { method: "DELETE" })).status).toBe(204);
    await expect((await invoke("/state")).json()).resolves.toMatchObject({ total: 0, batches: 0, pending: [] });
  });
});
