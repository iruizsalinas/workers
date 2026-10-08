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

describe("independent JSON contract verification", () => {
  let actual;
  beforeAll(async () => { actual = await (await invoke("/verification-json")).json(); });
  it.each(Object.entries(clr.verificationJson))("%s agrees with the CLR", (name, expected) => {
    expect(actual[name]).toBe(expected);
  });
});

describe("configuration submissions with required web contracts and KV persistence", () => {
  beforeEach(async () => { await env.KV.delete("verification-config:alpha"); });
  it.each(clr.verificationWeb)("validates and persists $json", async ({ json, result }) => {
    const response = await invoke("/verification-config", json);
    expect(response.status).toBe(result.status);
    expect(await response.json()).toEqual(result);
    const stored = await env.KV.get("verification-config:alpha", "json");
    if (result.status === 201) {
      expect(stored).toMatchObject({ name: result.name, enabled: result.enabled });
      expect(stored).not.toHaveProperty("secret");
      const read = await invoke(`/verification-config/${result.name}`);
      expect(read.status).toBe(200);
      expect(await read.json()).toEqual(result);
    } else {
      expect(stored).toBeNull();
      expect((await invoke("/verification-config/alpha")).status).toBe(404);
    }
  });
});
