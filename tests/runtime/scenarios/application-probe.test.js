import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { beforeAll, beforeEach, describe, expect, it } from "vitest";
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

  it("preserves dispatch, shipment merging, invoice audits and tenant report decisions", async () => {
    await expect((await invoke("/collection-applications")).json()).resolves.toEqual(clr.collectionApplications);
  });

  it("rejects nonfinite telemetry readings when exporting JSON", async () => {
    await expect((await invoke("/telemetry-export")).json()).resolves.toEqual(clr.telemetryExports);
  });

  it("honors constructor defaults while enforcing required record members", async () => {
    await expect((await invoke("/constructor-contracts")).json()).resolves.toEqual(clr.constructorContracts);
  });

  describe("regression scenarios", () => {
    let regressions;
    beforeAll(async () => {
      regressions = await (await invoke("/regressions")).json();
    });
    it.each(Object.entries(clr.regressions))("%s agrees with the CLR", (name, expected) => {
      expect(regressions[name]).toBe(expected);
    });
  });

  describe("regular expression scenarios", () => {
    let results;
    beforeAll(async () => {
      results = await (await invoke("/regex")).json();
    });
    it.each(Object.entries(clr.regex))("%s agrees with the CLR", (name, expected) => {
      expect(results[name]).toBe(expected);
    });
  });

  describe("DateOnly and TimeOnly scenarios", () => {
    let results;
    beforeAll(async () => {
      results = await (await invoke("/date-time-only")).json();
    });
    it.each(Object.entries(clr.dateTimeOnly))("%s agrees with the CLR", (name, expected) => {
      expect(results[name]).toBe(expected);
    });
  });

  describe("JsonNode scenarios", () => {
    let results;
    beforeAll(async () => {
      results = await (await invoke("/json-nodes")).json();
    });
    it.each(Object.entries(clr.jsonNodes))("%s agrees with the CLR", (name, expected) => {
      expect(results[name]).toBe(expected);
    });
  });
});

describe("JsonObject request bodies forwarded through Response.Json and KV", () => {
  it("keeps numbers a double cannot hold and patches the payload", async () => {
    const body = '{"id":12345678901234567890,"amount":0.10000000000000000001,"count":3,"tags":["a"]}';
    const response = await invoke("/json-forward", body);
    expect(response.status).toBe(200);
    const text = await response.text();
    expect(text).toBe('{"body":{"id":12345678901234567890,"amount":0.10000000000000000001,"count":3,"tags":["a"],'
      + '"forwarded":true,"meta":{"keys":5,"hasProto":false}},'
      + '"stored":"{\\"id\\":12345678901234567890,\\"amount\\":0.10000000000000000001,\\"count\\":3,\\"tags\\":[\\"a\\"],'
      + '\\"forwarded\\":true,\\"meta\\":{\\"keys\\":5,\\"hasProto\\":false}}"}');
  });

  it("treats prototype-named keys as ordinary data", async () => {
    const response = await invoke("/json-forward", '{"__proto__":{"admin":true},"constructor":{"prototype":{"x":1}}}');
    const result = await response.json();
    expect(Object.keys(result.body)).toEqual(["__proto__", "constructor", "forwarded", "meta"]);
    expect(Object.getOwnPropertyDescriptor(result.body, "__proto__").value).toEqual({ admin: true });
    expect(result.body.meta).toEqual({ keys: 3, hasProto: true });
    expect({}.admin).toBeUndefined();
  });

  it.each(["[1,2]", "5", "{\"a\":", "", "{\"a\":" + "[".repeat(70) + "]".repeat(70) + "}"])("rejects %s", async (body) => {
    const response = await invoke("/json-forward", body);
    expect(response.status).toBe(400);
  });
});

describe("DateOnly and TimeOnly request bodies and KV values", () => {
  it("reads and writes the System.Text.Json forms", async () => {
    const response = await invoke("/booking", '{"name":"standup","day":"2026-10-08","start":"9:30","until":null,"end":"10:15:30.5"}');
    expect(response.status).toBe(200);
    await expect(response.json()).resolves.toEqual({
      booking: { name: "standup", day: "2026-10-08", start: "09:30:00", until: null, end: "10:15:30.5000000" },
      weekday: 4,
      nextWeek: "2026-10-15",
      minutes: 45 + 30.5 / 60,
    });
  });

  it.each([
    '{"name":"x","day":"2026-02-30","start":"09:00"}',
    '{"name":"x","day":"2026-10-08T00:00:00","start":"09:00"}',
    '{"name":"x","day":"2026-10-08","start":"24:00"}',
    '{"name":"x","day":"2026-10-08","start":"1.09:00"}',
  ])("rejects %s", async (body) => {
    expect((await invoke("/booking", body)).status).toBe(400);
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

describe("tenant provisioning validates contracts before persisting resources", () => {
  beforeEach(async () => {
    await env.DB.exec("CREATE TABLE IF NOT EXISTS probe_tenants (id TEXT PRIMARY KEY, enabled INTEGER)");
    await env.DB.exec("DELETE FROM probe_tenants");
  });

  it.each(clr.provisioning)("preserves provisioning decisions for $json", async ({ json, result }) => {
    const response = await invoke("/provisioning", json);
    expect(response.status).toBe(result.status);
    await expect(response.json()).resolves.toEqual(result);
    const rows = await env.DB.prepare("SELECT * FROM probe_tenants").all();
    expect(rows.results).toEqual(result.status === 201
      ? [{ id: result.tenantId, enabled: result.enabled ? 1 : 0 }]
      : []);
  });
});
