import { createExecutionContext, createMessageBatch, createScheduledController, getQueueResult } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import { env } from "../support.js";
import queueConsumer from "../../../examples/QueueConsumer/dist/worker.js";
import scheduledTask from "../../../examples/ScheduledTask/dist/worker.js";

describe("event workers", () => {
  it("acknowledges queue messages through a native MessageBatch", async () => {
    const log = vi.spyOn(console, "log").mockImplementation(() => undefined);
    const batch = createMessageBatch("jobs", [
      {
        id: "message-1",
        timestamp: new Date(1_000),
        body: { path: "/jobs/1" },
        attempts: 1,
      },
    ]);
    const context = createExecutionContext();

    await queueConsumer.queue(batch, env, context);
    const result = await getQueueResult(batch, context);

    expect(result.explicitAcks).toEqual(["message-1"]);
    expect(result.retryMessages).toEqual([]);
    expect(log).toHaveBeenCalledWith("Batch 1: message-1");
    expect(log).toHaveBeenCalledWith("Processing /jobs/1");
  });

  it("receives native scheduled controller values", async () => {
    const log = vi.spyOn(console, "log").mockImplementation(() => undefined);
    const controller = createScheduledController({
      cron: "*/5 * * * *",
      scheduledTime: new Date("2026-08-15T12:00:00.000Z"),
    });

    await scheduledTask.scheduled(controller, env, createExecutionContext());

    expect(log).toHaveBeenCalledWith("Ran */5 * * * * at 2026-08-15T12:00:00.0000000+00:00");
  });

  it("forwards a faithfully shaped email event mock", async () => {
    const eventWrappers = await import("../fixtures/EventWrappers/dist/worker.js");
    const forward = vi.fn(async () => undefined);
    const message = {
      from: "sender@example.test",
      to: "worker@example.test",
      headers: new Headers({ subject: "Generated event" }),
      raw: new ReadableStream(),
      rawSize: 0,
      setReject: vi.fn(),
      forward,
    };

    await eventWrappers.default.email(message, {}, createExecutionContext());

    expect(forward).toHaveBeenCalledWith("archive@example.test");
  });

  it("passes a faithfully shaped tail event mock to a service RPC", async () => {
    const eventWrappers = await import("../fixtures/EventWrappers/dist/worker.js");
    const record = vi.fn(async () => undefined);
    const tailEvents = [{
      scriptName: "origin-worker",
      outcome: "ok",
      eventTimestamp: new Date(1_000),
      event: {
        request: { url: "https://worker.test/", method: "GET", headers: new Headers() },
        response: { status: 200 },
      },
      logs: [],
      exceptions: [],
    }];

    await eventWrappers.default.tail(
      tailEvents,
      { TAIL_SINK: { record } },
      createExecutionContext(),
    );

    expect(record).toHaveBeenCalledWith(tailEvents);
  });
});
