import { createExecutionContext, env, waitOnExecutionContext } from "cloudflare:test";
import { beforeAll, describe, expect, it } from "vitest";
import worker from "../fixtures/BatchFanout/dist/worker.js";
import clr from "../generated/applications.json";

describe("asynchronous batch completion and failure handling", () => {
  let actual;
  beforeAll(async () => {
    const context = createExecutionContext();
    const response = await worker.fetch(new Request("https://worker.test/verification-async"), env, context);
    await waitOnExecutionContext(context);
    expect(response.status).toBe(200);
    actual = await response.json();
  });
  it.each(Object.entries(clr.verificationAsync))("%s agrees with the CLR", (name, expected) => {
    expect(actual[name]).toBe(expected);
  });
});

describe("independent task batch verification", () => {
  let actual;
  beforeAll(async () => {
    const context = createExecutionContext();
    const response = await worker.fetch(new Request("https://worker.test/task-review"), env, context);
    await waitOnExecutionContext(context);
    expect(response.status).toBe(200);
    actual = await response.json();
  });
  it.each(Object.entries(clr.taskReview))("%s agrees with the CLR", (name, expected) => {
    expect(actual[name]).toBe(expected);
  });
});
