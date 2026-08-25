import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";
import queueProducer from "../../../examples/QueueProducer/dist/worker.js";

describe("queue bindings", () => {
  it("lowers queue producer calls to the native binding shape", async () => {
    const send = vi.fn(async () => undefined);
    const response = await queueProducer.fetch(
      new Request("https://worker.test/jobs/123"),
      { JOBS: { send } },
      createExecutionContext(),
    );

    expect(send).toHaveBeenCalledOnce();
    expect(send.mock.calls[0][0]).toMatchObject({ path: "/jobs/123" });
    expect(send.mock.calls[0][1]).toEqual({ contentType: "json" });
    expect(send.mock.calls[0][0].queuedAt).toBeInstanceOf(Date);
    await expect(response.json()).resolves.toEqual({ queued: true });
  });
});
