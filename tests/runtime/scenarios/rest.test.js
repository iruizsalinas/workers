import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import worker from "../fixtures/UserApi/dist/worker.js";

const invoke = async (path, init) => {
  const context = createExecutionContext();
  const response = await worker.fetch(new Request(`https://worker.test${path}`, init), env, context);
  await waitOnExecutionContext(context);
  return response;
};

describe("user API scenario", () => {
  beforeEach(async () => {
    await env.DB.exec("CREATE TABLE IF NOT EXISTS users (id TEXT PRIMARY KEY, name TEXT NOT NULL, email TEXT NOT NULL, created_at TEXT NOT NULL)");
    await env.DB.exec("DELETE FROM users");
  });

  it("creates, caches, reads, and deletes a user", async () => {
    const created = await invoke("/users", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ name: " Ada ", email: " ADA@EXAMPLE.COM " }),
    });
    expect(created.status).toBe(201);
    const user = await created.json();
    expect(user).toMatchObject({ name: "Ada", email: "ada@example.com" });
    expect(created.headers.get("location")).toBe(`/users/${user.id}`);

    await expect((await invoke(`/users/${user.id}`)).json()).resolves.toMatchObject({ source: "kv" });
    await env.USERS.delete(`user:${user.id}`);
    await expect((await invoke(`/users/${user.id}`)).json()).resolves.toMatchObject({ source: "d1" });

    expect((await invoke(`/users/${user.id}`, { method: "DELETE" })).status).toBe(204);
    expect((await invoke(`/users/${user.id}`)).status).toBe(404);
  });

  it("preserves validation, routing, and method responses", async () => {
    expect((await invoke("/users", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ name: "x", email: "invalid" }),
    })).status).toBe(400);
    expect((await invoke("/users/missing", { method: "PATCH" })).headers.get("allow")).toBe("GET, DELETE");
    expect((await invoke("/missing")).status).toBe(404);
  });
});
