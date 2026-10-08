import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { expect, it } from "vitest";
import worker from "../fixtures/ApplicationProbe/dist/worker.js";
import clr from "../generated/applications.json";

it("preserves numeric parsing, invoice formats, rounding and scheduling boundaries", async () => {
  const context = createExecutionContext();
  const response = await worker.fetch(new Request("https://worker.test/numeric-verification"), env, context);
  await waitOnExecutionContext(context);
  expect(response.status).toBe(200);
  expect(await response.json()).toEqual(clr.numericVerification);
});
