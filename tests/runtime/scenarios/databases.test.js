import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import worker from "../.wrangler/databases/worker.js";

const invoke = async (path) => {
  const response = await worker.fetch(new Request(`https://worker.test${path}`), env, {});
  expect(response.status, await response.clone().text()).toBe(200);
  return response.json();
};

const expectSqlReport = (report) => {
  expect(report.inserted).toBe(2);
  expect(report.activeCount).toBe(1);
  expect(report.items).toEqual([
    { id: 1, name: "first", total: 9007199254740991, active: true, created: "2026-01-02T03:04:05.000Z", payload: { 0: 1, 1: 2, 2: 3 } },
    { id: 2, name: "second", total: 7, active: false, created: "2026-02-03T04:05:06.000Z", payload: null },
  ]);
};

describe.skipIf(!env.POSTGRES_URL)("PostgreSQL through pg", () => {
  it("maps typed rows, parameters, and command results", async () => expectSqlReport(await invoke("/postgres")));
  it("connects through Hyperdrive", async () => expectSqlReport(await invoke("/postgres-hyperdrive")));
});

describe.skipIf(!env.MYSQL_URL)("MySQL through mysql2", () => {
  it("maps typed rows, parameters, and command results", async () => {
    const report = await invoke("/mysql");
    expectSqlReport(report);
    expect(report.insertId).toBe(1);
  });
  it("connects through Hyperdrive", async () => expectSqlReport(await invoke("/mysql-hyperdrive")));
});

describe.skipIf(!env.MONGODB_URL)("MongoDB through mongodb", () => {
  it("round-trips documents, ObjectIds, filters, updates, and aggregations", async () => {
    const report = await invoke("/mongo");
    expect(report.insertedId).toMatch(/^[0-9a-f]{24}$/);
    expect(report.insertedCount).toBe(2);
    expect(report.found).toEqual({ _id: report.insertedId, name: "Ada", age: 36, tags: ["math"], joined: "2026-01-02T03:04:05.000Z" });
    expect(report.matched).toBe(1);
    expect(report.modified).toBe(1);
    expect(report.adults.map(person => [person.name, person.age, person.tags])).toEqual([
      ["Grace", 45, ["navy", "cobol"]],
      ["Ada", 36, ["math"]],
    ]);
    expect(report.count).toBe(3);
    expect(report.totalAge).toBe(110);
    expect(report.deleted).toBe(3);
  });

  it("aborts a running read when its token is canceled", async () => {
    const started = performance.now();
    const response = await worker.fetch(new Request("https://worker.test/mongo-canceled"), env, {});
    await expect(response.text()).resolves.toBe("canceled");
    expect(performance.now() - started).toBeLessThan(1500);
  });
});
