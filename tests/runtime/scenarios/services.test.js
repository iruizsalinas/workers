import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import worker from "../fixtures/ServiceGateway/dist/worker.js";

const invoke = (path, init) => worker.fetch(new Request(`https://worker.test${path}`, init), env, {});

beforeEach(async () => {
  await env.DB.exec("CREATE TABLE IF NOT EXISTS users (id TEXT PRIMARY KEY, username TEXT NOT NULL, created_at TEXT NOT NULL); DELETE FROM users;");
});

describe("named WorkerEntrypoint service bindings", () => {
  it("calls the same service through RPC and fetch", async () => {
    const response = await invoke("/health");
    const result = await response.json();
    expect(result.rpc).toMatchObject({ ok: true, service: "core" });
    expect(result.http).toMatchObject({ ok: true, service: "core" });
  });

  it("propagates RPC values, defaults, D1 work, and validation results", async () => {
    const created = await invoke("/users", { method: "POST", body: JSON.stringify({ username: "ada_1" }) });
    expect(created.status).toBe(201);
    const user = await created.json();
    expect(user.username).toBe("ada_1");

    const list = await (await invoke("/users?prefix=ada")).json();
    expect(list.users).toHaveLength(1);
    expect((await invoke(`/users/${user.id}`)).status).toBe(200);

    const invalid = await invoke("/users", { method: "POST", body: JSON.stringify({ username: "!" }) });
    expect(invalid.status).toBe(400);
    await expect(invalid.json()).resolves.toEqual({ error: "Invalid username" });
  });

  it("returns a native Response through named RPC", async () => {
    const response = await invoke("/assets/missing.txt");
    expect(response.status).toBe(404);
    expect(response.headers.get("x-served-via")).toBe("rpc");
  });
});
