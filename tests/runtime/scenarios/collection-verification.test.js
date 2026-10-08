import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { beforeAll, describe, expect, it } from "vitest";
import worker from "../fixtures/ApplicationProbe/dist/worker.js";
import clr from "../generated/applications.json";

describe("audited inventory and tenant reporting collection semantics", () => {
  let actual;
  beforeAll(async () => {
    const context = createExecutionContext();
    const response = await worker.fetch(new Request("https://worker.test/collection-verification"), env, context);
    await waitOnExecutionContext(context);
    expect(response.status).toBe(200);
    actual = await response.json();
  });
  it.each(Object.entries(clr.collectionVerification))("%s agrees with the CLR", (name, expected) => {
    expect(actual[name]).toBe(expected);
  });
});
