import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import worker from "../fixtures/ApplicationProbe/dist/worker.js";
import clr from "../generated/applications.json";

const invoke = async (path, body) => {
  const context = createExecutionContext();
  const response = await worker.fetch(new Request(`https://worker.test${path}`, {
    method: body === undefined ? "GET" : "POST", body,
  }), env, context);
  await waitOnExecutionContext(context);
  return response;
};

describe("application correctness probes", () => {
  it.each(clr.jsonAudit)("JSON materialization agrees with the CLR for $json", async ({ json, result }) => {
    await expect((await invoke("/json-audit", json)).json()).resolves.toEqual(result);
  });
  it.each(clr.checkout)("checkout follows the CLR contract for $json", async ({ json, result }) => {
    const response = await invoke("/checkout", json);
    expect(response.status).toBe(result.status);
    await expect(response.json()).resolves.toEqual(result);
  });

  it("protects stock and pending reservations from invalid mutations", async () => {
    await expect((await invoke("/inventory")).json()).resolves.toEqual(clr.inventory);
  });

  it("reports deferred sales and excludes unassigned tenants from joins", async () => {
    await expect((await invoke("/reporting")).json()).resolves.toEqual(clr.reporting);
  });
});

describe("shipment submission with nested input, D1 persistence and background KV cache", () => {
  beforeEach(async () => {
    await env.DB.exec("CREATE TABLE IF NOT EXISTS probe_shipments (id TEXT PRIMARY KEY, total INTEGER, priority INTEGER, delay INTEGER)");
    await env.DB.exec("DELETE FROM probe_shipments");
  });

  it.each(clr.shipments)("preserves business decisions for $json", async ({ json, result }) => {
    const response = await invoke("/shipments", json);
    expect(response.status).toBe(result.status);
    await expect(response.json()).resolves.toEqual(result);
    const rows = await env.DB.prepare("SELECT * FROM probe_shipments").all();
    if (result.status === 202) {
      expect(rows.results).toHaveLength(1);
      expect(rows.results[0]).toMatchObject({ total: result.totalCents, priority: result.priority, delay: result.delaySeconds });
      const location = response.headers.get("location");
      expect(location).toMatch(/^\/shipments\/[a-f0-9-]{36}$/);
      await expect((await invoke(location)).json()).resolves.toEqual(result);
    } else {
      expect(rows.results).toEqual([]);
      expect(response.headers.get("location")).toBeNull();
    }
  });
});
