import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import "../support.js";
import routing from "../../../examples/Routing/dist/worker.js";
import semantics from "../fixtures/RouterSemantics/dist/worker.js";

function invoke(worker, environment, path, init) {
  return worker.fetch(new Request(`https://worker.test${path}`, init), environment, createExecutionContext());
}

describe("router semantics", () => {
  const environment = { PREFIX: "p" };
  const call = (path, init) => invoke(semantics, environment, path, init);
  const text = async (path, init) => {
    const response = await call(path, init);
    return [response.status, await response.text()];
  };

  it("prefers literals, then constrained parameters, then parameters", async () => {
    expect(await text("/users/me")).toEqual([200, "p:me"]);
    expect(await text("/users/41")).toEqual([200, "p:int:42"]);
    expect(await text("/users/ada")).toEqual([200, "p:name:ada"]);
  });

  it("matches constraints exactly when int.Parse and Guid.Parse would succeed", async () => {
    expect(await text("/users/2147483648")).toEqual([200, "p:name:2147483648"]);
    expect(await text("/users/-7")).toEqual([200, "p:int:-6"]);
    expect(await text("/items/{6F9619FF-8B86-D011-B42D-00C04FC964FF}"))
      .toEqual([200, "6f9619ff-8b86-d011-b42d-00c04fc964ff"]);
    expect(await text("/items/not-a-guid")).toEqual([404, "fallback:/items/not-a-guid"]);
  });

  it("decodes path parameters and joins catch-all segments", async () => {
    expect(await text("/users/ada%20lovelace")).toEqual([200, "p:name:ada lovelace"]);
    expect(await text("/users/%E0%A4%A")).toEqual([200, "p:name:%E0%A4%A"]);
    expect(await text("/proxy/a/b%2Fc", { method: "PATCH" })).toEqual([200, "PATCH:a/b/c"]);
    expect(await text("/proxy", { method: "POST" })).toEqual([200, "POST:"]);
  });

  it("prefers a method route over an Any route of the same shape or a less specific one", async () => {
    expect(await text("/proxy/status")).toEqual([200, "status"]);
    expect(await text("/proxy/status", { method: "POST" })).toEqual([200, "POST:status"]);
  });

  it("runs synchronous and asynchronous handlers with the native request", async () => {
    expect(await text("/users/ada", { method: "DELETE" })).toEqual([200, "deleted:ada"]);
    expect(await text("/echo", { method: "POST", body: "hello" })).toEqual([201, "hello"]);
    expect(await text("/")).toEqual([200, "root"]);
  });

  it("answers HEAD from GET routes without a body", async () => {
    const response = await call("/users/me", { method: "HEAD" });
    expect(response.status).toBe(200);
    expect(response.headers.get("content-type")).toBe("text/plain;charset=UTF-8");
    expect(await response.text()).toBe("");
  });

  it("answers 405 with Allow when only the method differs", async () => {
    const response = await call("/echo");
    expect(response.status).toBe(405);
    expect(response.headers.get("allow")).toBe("POST");
    const users = await call("/users/ada", { method: "PUT" });
    expect(users.status).toBe(405);
    expect(users.headers.get("allow")).toBe("GET, HEAD, DELETE");
  });

  it("uses the fallback for unmatched paths, including empty segments and trailing slashes", async () => {
    expect(await text("/missing")).toEqual([404, "fallback:/missing"]);
    expect(await text("/users/")).toEqual([404, "fallback:/users/"]);
    expect(await text("/echo/")).toEqual([404, "fallback:/echo/"]);
  });

  it("rejects routes that match exactly the same paths", async () => {
    expect(await text("/conflict")).toEqual([
      500,
      "The route GET /users/{name} conflicts with /users/{id}, which matches the same paths.",
    ]);
  });
});

describe("routing example", () => {
  function notesEnvironment() {
    const values = new Map();
    return {
      values,
      NOTES: {
        get: async (key, options) => {
          const value = values.get(key);
          return value === undefined ? null : options?.type === "json" ? JSON.parse(value) : value;
        },
        put: async (key, value) => void values.set(key, value),
        delete: async key => void values.delete(key),
      },
    };
  }

  it("stores, reads, and deletes notes through guid routes", async () => {
    const environment = notesEnvironment();
    const id = "6f9619ff-8b86-d011-b42d-00c04fc964ff";
    const saved = await invoke(routing, environment, `/notes/${id.toUpperCase()}`, {
      method: "PUT",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ text: "  Buy milk  " }),
    });
    expect(saved.headers.get("x-router")).toBe("csharp");
    await expect(saved.json()).resolves.toEqual({ id, text: "Buy milk" });
    expect([...environment.values.keys()]).toEqual([`note:${id}`]);

    const read = await invoke(routing, environment, `/notes/${id}`);
    await expect(read.json()).resolves.toEqual({ id, text: "Buy milk" });

    const removed = await invoke(routing, environment, `/notes/${id}`, { method: "DELETE" });
    expect(removed.status).toBe(204);
    expect((await invoke(routing, environment, `/notes/${id}`)).status).toBe(404);
  });

  it("serves catch-all, fallback, and method errors with the shared header", async () => {
    const environment = notesEnvironment();
    const file = await invoke(routing, environment, "/files/docs/readme.md");
    expect(await file.text()).toBe("File: docs/readme.md");

    const missing = await invoke(routing, environment, "/notes/not-a-guid");
    expect(missing.status).toBe(404);
    expect(missing.headers.get("x-router")).toBe("csharp");
    await expect(missing.json()).resolves.toEqual({ error: "Not found" });

    const method = await invoke(routing, environment, "/", { method: "POST" });
    expect(method.status).toBe(405);
    expect(method.headers.get("allow")).toBe("GET, HEAD");
  });
});
