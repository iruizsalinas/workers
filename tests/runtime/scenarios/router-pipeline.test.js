import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";
import worker from "../fixtures/RouterPipeline/dist/worker.js";

const origin = "https://app.example";
const call = (path, init = {}) => worker.fetch(new Request(`https://worker.test${path}`, {
  ...init, headers: { origin, ...init.headers },
}), {}, createExecutionContext());
const preflight = (path = "/private", headers = {}) => call(path, {
  method: "OPTIONS",
  headers: { "access-control-request-method": "POST", "access-control-request-headers": "Content-Type, Authorization", ...headers },
});
const expectCors = response => {
  expect(response.headers.get("access-control-allow-origin")).toBe(origin);
  expect(response.headers.get("access-control-allow-credentials")).toBe("true");
};
async function expectEmptyError(response) {
  expect(response.body).toBeNull();
  expect(response.headers.get("content-type")).toBeNull();
  expect(await response.text()).toBe("");
}

describe("router middleware in workerd", () => {
  it.each(["GET", "HEAD"])("preserves native network errors for %s with CORS", async method => {
    const response = await call("/network-error", { method });
    expect(response.status).toBe(0);
    expect(response.type).toBe("error");
  });

  it("runs in registration order and unwinds in reverse with matched parameters", async () => {
    const response = await call("/ok/42");
    expect(await response.text()).toBe("ok");
    expect(response.headers.get("x-trace")).toBe("outer>inner>42<inner<outer");
    expectCors(response);
    expect(response.headers.get("vary")).toBe("Accept-Encoding, origin");
    expect(response.headers.get("access-control-expose-headers")).toBe("x-trace");
  });

  it("supports short-circuiting without running a route", async () => {
    const response = await call("/short");
    expect(response.status).toBe(202);
    expect(await response.text()).toBe("short");
    expect(response.headers.get("x-trace")).toBe("outer>inner><outer");
    expectCors(response);
  });

  it.each([["/missing", 404], ["/ok/42", 405, "POST"]])("wraps generated responses for %s", async (path, status, method = "GET") => {
    const response = await call(path, { method });
    expect(response.status).toBe(status);
    expect(response.headers.get("x-trace")).toBe("outer>inner><inner<outer");
    expectCors(response);
    if (status === 405) expect(response.headers.get("allow")).toBe("GET, HEAD");
    await expectEmptyError(response);
  });

  it("rejects a second next call without executing the handler twice", async () => {
    const response = await call("/double-next");
    expect(response.status).toBe(500);
    expect(response.headers.get("x-hits")).toBe("1");
    await expectEmptyError(response);
    expectCors(response);
  });

  it("streams responses through middleware", async () => {
    const response = await call("/stream");
    expectCors(response);
    expect(await response.text()).toBe("onetwo");
  });

  it.each(["/ok/42", "/http-error", "/missing", "/short"])("removes HEAD bodies after the whole pipeline for %s", async path => {
    const response = await call(path, { method: "HEAD" });
    expectCors(response);
    expect(response.headers.has("x-trace")).toBe(true);
    expect(await response.text()).toBe("");
  });

  it.each(["/head-tee", "/head-cancel-fails"])("returns HEAD without depending on body cleanup for %s", async path => {
    const response = await call(path, { method: "HEAD" });
    expect(response.status).toBe(201);
    expect(response.body).toBeNull();
    expectCors(response);
    expect(response.headers.has("x-trace")).toBe(true);
  });
});

describe("HTTP errors and error security in workerd", () => {
  it("preserves intentional status with an empty default response", async () => {
    const response = await call("/http-error");
    expect(response.status).toBe(404);
    await expectEmptyError(response);
    expectCors(response);
  });

  it.each(["{", "", '{"text":"secret input",}', "undefined"])("returns a safe 400 for malformed request JSON: %s", async body => {
    const response = await call("/json", { method: "POST", body });
    expect(response.status).toBe(400);
    await expectEmptyError(response);
    expectCors(response);
  });

  it.each(['{"text":"hello"}', "null"])("retains typed JSON reads and null: %s", async body => {
    const response = await call("/json", { method: "POST", body });
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual(JSON.parse(body));
  });

  it.each(["/unexpected", "/response-json", "/internal-json", "/consumed"])("keeps internal failures at 500 without leaking details: %s", async path => {
    const response = await call(path, path === "/consumed" ? { method: "POST", body: "{}" } : {});
    expect(response.status).toBe(500);
    await expectEmptyError(response);
    expectCors(response);
  });

  it("supplies custom formatting and the original exception for server logging", async () => {
    const log = vi.spyOn(console, "error").mockImplementation(() => {});
    const expected = await call("/custom");
    expect(expected.status).toBe(409);
    expect(await expected.json()).toEqual({ error: "Conflict", path: "/custom" });
    const unexpected = await call("/custom-unexpected");
    expect(unexpected.status).toBe(500);
    expect(await unexpected.json()).toEqual({ error: "Internal Server Error", path: "/custom-unexpected" });
    expect(log).toHaveBeenCalledWith("secret database password");
    expectCors(unexpected);
  });

  it("awaits asynchronous error formatters", async () => {
    const response = await call("/async-error-handler");
    expect(response.status).toBe(422);
    expect(response.headers.get("x-async")).toBe("true");
    expect(await response.json()).toEqual({ error: "Invalid input" });
    expectCors(response);
  });

  it("contains a failing error formatter without recursion or information leakage", async () => {
    const response = await call("/broken-error-handler");
    expect(response.status).toBe(500);
    await expectEmptyError(response);
    expectCors(response);
  });

  it.each(["/caught", "/pattern"])("supports HttpError catches and type patterns: %s", async path => {
    const response = await call(path);
    expect(response.status).toBe(418);
    expect(await response.text()).toBe("teapot");
  });

  it("enforces authentication even for disallowed browser origins", async () => {
    const denied = await call("/private", { headers: { origin: "https://evil.example" } });
    expect(denied.status).toBe(401);
    expect(denied.headers.get("access-control-allow-origin")).toBeNull();
    await expectEmptyError(denied);
    const allowed = await call("/private", { headers: { authorization: "Bearer good" } });
    expect(allowed.status).toBe(200);
    expect(await allowed.text()).toBe("private");
  });
});

describe("CORS and CORS security in workerd", () => {
  it("answers real preflights before auth or routing, including unregistered paths", async () => {
    for (const path of ["/private", "/unknown"]) {
      const response = await preflight(path);
      expect(response.status).toBe(204);
      expect(await response.text()).toBe("");
      expectCors(response);
      expect(response.headers.get("x-trace")).toBeNull();
      expect(response.headers.get("access-control-allow-methods")).toBe("GET, HEAD, POST");
      expect(response.headers.get("access-control-allow-headers")).toBe("content-type, authorization");
      expect(response.headers.get("access-control-max-age")).toBe("600");
      expect(response.headers.get("vary")).toBe("Origin, Access-Control-Request-Method, Access-Control-Request-Headers");
    }
  });

  it("leaves ordinary OPTIONS requests to the router", async () => {
    const response = await call("/options", { method: "OPTIONS" });
    expect(response.status).toBe(200);
    expect(await response.text()).toBe("ordinary options");
    expectCors(response);
    expect(response.headers.get("access-control-allow-methods")).toBeNull();
    const withoutOrigin = await worker.fetch(new Request("https://worker.test/options", {
      method: "OPTIONS", headers: { "access-control-request-method": "POST" },
    }), {}, createExecutionContext());
    expect(await withoutOrigin.text()).toBe("ordinary options");
  });

  it.each(["https://evil.example", "https://app.example.evil", "http://app.example", "https://app.example:444", "https://app.example/path", "https://user@app.example", "null", "not-an-origin"])("rejects origin bypasses: %s", async badOrigin => {
    const response = await preflight("/private", { origin: badOrigin });
    expect(response.status).toBe(403);
    expect(response.headers.get("access-control-allow-origin")).toBeNull();
    await expectEmptyError(response);
    expect(response.headers.get("access-control-allow-credentials")).toBeNull();
    expect(response.headers.get("access-control-allow-methods")).toBeNull();
  });

  it.each([
    { "access-control-request-method": "DELETE" },
    { "access-control-request-method": "post" },
    { "access-control-request-headers": "x-admin" },
    { "access-control-request-headers": "authorization, x-admin" },
    { "access-control-request-headers": "authorization,,content-type" },
    { "access-control-request-headers": "*" },
  ])("rejects unapproved methods or headers: %j", async headers => {
    const response = await preflight("/private", headers);
    expect(response.status).toBe(403);
    expect(response.headers.get("access-control-allow-origin")).toBeNull();
    await expectEmptyError(response);
  });

  it("matches a second explicit origin and headers case-insensitively", async () => {
    const response = await preflight("/private", { origin: "https://second.example", "access-control-request-headers": "AUTHORIZATION" });
    expect(response.status).toBe(204);
    expect(response.headers.get("access-control-allow-origin")).toBe("https://second.example");
  });

  it("does not require request headers for a headerless preflight", async () => {
    const response = await call("/private", { method: "OPTIONS", headers: { "access-control-request-method": "GET" } });
    expect(response.status).toBe(204);
  });

  it("allows a public wildcard policy without credentials", async () => {
    const response = await call("/wildcard", { headers: { origin: "https://any.example" } });
    expect(response.headers.get("access-control-allow-origin")).toBe("*");
    expect(response.headers.get("access-control-allow-credentials")).toBeNull();
    const opaque = await call("/wildcard", { headers: { origin: "null" } });
    expect(opaque.headers.get("access-control-allow-origin")).toBeNull();
    expect(opaque.headers.get("vary")).toBe("Origin");
  });

  it("removes unsafe downstream grants for denied origins", async () => {
    const response = await call("/downstream-cors", { headers: { origin: "https://evil.example" } });
    expect(response.status).toBe(200);
    expect(response.headers.get("access-control-allow-origin")).toBeNull();
    expect(response.headers.get("access-control-allow-credentials")).toBeNull();
    expect(response.headers.get("vary")).toBe("Origin");
  });

  it("does not grant CORS when Origin is absent", async () => {
    const response = await worker.fetch(new Request("https://worker.test/ok/42"), {}, createExecutionContext());
    expect(response.status).toBe(200);
    expect(response.headers.get("access-control-allow-origin")).toBeNull();
  });

  it.each(["credentials", "origin", "null", "headers", "trailing-newline", "header-wildcard", "methods", "exposed", "age", "status", "status-high", "duplicate"])("rejects unsafe or invalid configuration: %s", async config => {
    const response = await call(`/config/${config}`);
    expect(response.status).toBe(400);
    expect(await response.text()).not.toBe("accepted");
  });
});
