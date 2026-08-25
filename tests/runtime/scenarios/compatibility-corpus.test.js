import {
  createExecutionContext,
  createMessageBatch,
  createScheduledController,
  getQueueResult,
  waitOnExecutionContext,
} from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";
import requestPolicy from "../../../examples/RequestPolicy/dist/worker.js";
import telemetryPipeline from "../../../examples/TelemetryPipeline/dist/worker.js";

const jsonRequest = (payload) => {
  const body = JSON.stringify(payload);
  return new Request("https://worker.test/v1/readings", {
    method: "POST",
    headers: {
      "content-type": "application/json",
      "content-length": String(new TextEncoder().encode(body).length),
      "authorization": "Bearer visitor-value",
    },
    body,
  });
};

describe("production-shaped compatibility samples", () => {
  it("validates, sanitizes, and forwards an unrelated JSON contract", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockResolvedValue(
      Response.json({ stored: true }, { status: 202 }),
    );
    const response = await requestPolicy.fetch(
      jsonRequest({ deviceId: "sensor-17", value: 21.5, tags: ["indoor", "lab"] }),
      { ORIGIN: "https://telemetry.example" },
      createExecutionContext(),
    );

    expect(response.status).toBe(202);
    expect(response.headers.get("x-policy")).toBe("validated");
    expect(response.headers.get("x-upstream-host")).toBe("telemetry.example");
    expect(fetchMock).toHaveBeenCalledOnce();
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe("https://telemetry.example/v1/readings");
    expect(init.redirect).toBe("manual");
    expect(init.headers.get("authorization")).toBeNull();
    expect(JSON.parse(init.body)).toEqual({
      deviceId: "sensor-17",
      value: 21.5,
      tags: ["indoor", "lab"],
    });
  });

  it("rejects duplicate set members before reaching an origin", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch");
    const response = await requestPolicy.fetch(
      jsonRequest({ deviceId: "sensor-17", value: 21.5, tags: ["lab", "lab"] }),
      { ORIGIN: "https://telemetry.example" },
      createExecutionContext(),
    );

    expect(response.status).toBe(400);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("turns scheduled database rows into correctly shaped queue messages", async () => {
    const sendBatch = vi.fn(async () => undefined);
    const statement = {
      bind: vi.fn(() => statement),
      all: vi.fn(async () => ({ results: [{ sensorId: "sensor-17" }], success: true })),
    };
    const context = createExecutionContext();

    await telemetryPipeline.scheduled(
      createScheduledController({
        cron: "*/5 * * * *",
        scheduledTime: new Date("2026-08-25T10:00:00.000Z"),
      }),
      { DB: { prepare: vi.fn(() => statement) }, READINGS: { sendBatch } },
      context,
    );
    await waitOnExecutionContext(context);

    expect(sendBatch).toHaveBeenCalledOnce();
    expect(sendBatch.mock.calls[0][0]).toEqual([
      {
        body: { sensorId: "sensor-17", requestId: expect.any(String) },
        contentType: "json",
      },
    ]);
  });

  it("coordinates queue work through RPC and parallel outbound requests", async () => {
    const reserve = vi.fn(async () => ({ allowed: true, token: "lease-1" }));
    const complete = vi.fn(async () => undefined);
    const run = vi.fn(async () => ({ success: true, meta: { changes: 1 } }));
    const statement = { bind: vi.fn(() => statement), run };
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async (url) =>
      url.includes("sensors.example")
        ? Response.json({ value: 22.25 })
        : Response.json({ condition: "clear" }),
    );
    const batch = createMessageBatch("readings", [{
      id: "message-1",
      timestamp: new Date(),
      body: { sensorId: "sensor-17", requestId: "request-1" },
      attempts: 1,
    }]);
    const context = createExecutionContext();

    await telemetryPipeline.queue(batch, {
      DB: { prepare: vi.fn(() => statement) },
      RATE_GATE: { getByName: vi.fn(() => ({ reserve, complete })) },
    }, context);
    const result = await getQueueResult(batch, context);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(reserve).toHaveBeenCalledWith("request-1");
    expect(complete).toHaveBeenCalledWith("lease-1");
    expect(run).toHaveBeenCalledOnce();
    expect(result.explicitAcks).toEqual(["message-1"]);
  });
});
