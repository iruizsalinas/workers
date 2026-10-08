import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { beforeAll, describe, expect, it } from "vitest";
import worker from "../fixtures/ApplicationProbe/dist/worker.js";
import clr from "../generated/applications.json";

describe("invoice, scheduling and policy applications", () => {
  let actual;
  beforeAll(async () => {
    const context = createExecutionContext();
    const response = await worker.fetch(new Request("https://worker.test/language"), env, context);
    await waitOnExecutionContext(context);
    expect(response.status).toBe(200);
    actual = await response.json();
  });
  it.each(Object.entries(clr.language))("%s agrees with the CLR", (name, expected) => {
    expect(actual[name]).toBe(expected);
  });
});
