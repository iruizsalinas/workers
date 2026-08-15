import {
  createExecutionContext,
  createMessageBatch,
  createScheduledController,
  env,
  getQueueResult,
  waitOnExecutionContext,
} from "cloudflare:test";
import { beforeEach, describe, expect, it, vi } from "vitest";
import worker from "../fixtures/JobProcessor/dist/worker.js";

const invoke = (path, init) => worker.fetch(
  new Request(`https://worker.test${path}`, init),
  env,
  createExecutionContext(),
);

describe("multi-entrypoint job processor scenario", () => {
  beforeEach(async () => {
    await env.DB.exec("CREATE TABLE IF NOT EXISTS jobs (id TEXT PRIMARY KEY, status TEXT NOT NULL, callback_url TEXT NOT NULL, payload TEXT, attempts INTEGER NOT NULL, created_at TEXT NOT NULL, completed_at TEXT)");
    await env.DB.exec("DELETE FROM jobs");
  });

  it("creates and reads a queued job through fetch", async () => {
    const created = await invoke("/jobs", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ callbackUrl: "https://callback.test", payload: { value: 42 } }),
    });
    expect(created.status).toBe(202);
    const result = await created.json();
    await expect((await invoke(`/jobs/${result.id}`)).json()).resolves.toMatchObject({
      id: result.id,
      status: "queued",
      callbackUrl: "https://callback.test",
    });
  });

  it("acks successful queue jobs and retries failed attempts", async () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    const job = { id: crypto.randomUUID(), callbackUrl: "https://callback.test", payload: { value: 1 }, createdAt: new Date().toISOString() };
    await env.DB.prepare("INSERT INTO jobs (id, status, callback_url, payload, attempts, created_at) VALUES (?, 'queued', ?, '{}', 0, ?)")
      .bind(job.id, job.callbackUrl, job.createdAt).run();

    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response(null, { status: 204 }));
    const successful = createMessageBatch("jobs", [{ id: "success", timestamp: new Date(), body: job, attempts: 1 }]);
    const successContext = createExecutionContext();
    await worker.queue(successful, env, successContext);
    expect((await getQueueResult(successful, successContext)).explicitAcks).toEqual(["success"]);

    vi.spyOn(globalThis, "fetch").mockResolvedValueOnce(new Response("failed", { status: 503 }));
    const failed = createMessageBatch("jobs", [{ id: "failed", timestamp: new Date(), body: job, attempts: 2 }]);
    const failureContext = createExecutionContext();
    await worker.queue(failed, env, failureContext);
    expect((await getQueueResult(failed, failureContext)).retryMessages).toEqual([{ msgId: "failed" }]);
  });

  it("runs scheduled cleanup through waitUntil", async () => {
    await env.DB.prepare("INSERT INTO jobs (id, status, callback_url, payload, attempts, created_at, completed_at) VALUES ('old', 'completed', 'https://callback.test', '{}', 1, '2020-01-01T00:00:00Z', '2020-01-02T00:00:00Z')").run();
    const context = createExecutionContext();
    worker.scheduled(createScheduledController({ scheduledTime: new Date() }), env, context);
    await waitOnExecutionContext(context);
    expect(await env.DB.prepare("SELECT id FROM jobs WHERE id = 'old'").first()).toBeNull();
  });
});
