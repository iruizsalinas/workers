import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";

describe("mocked platform bindings", () => {
  it("lowers remote-only bindings to explicit mock call shapes", async () => {
    const mocks = {
      dispatchGet: vi.fn(() => ({ fetch: vi.fn(async () => new Response("tenant")) })),
      limit: vi.fn(async () => ({ success: true })),
      writeDataPoint: vi.fn(() => undefined),
      sendEmail: vi.fn(async () => ({ messageId: "email-1" })),
      aiRun: vi.fn(async () => "ai-result"),
      workflowStatus: vi.fn(async () => ({ status: "running" })),
      workflowCreate: vi.fn(async () => ({ id: "workflow-1" })),
      imagesInfo: vi.fn(async () => ({ format: "png", width: 8, height: 6 })),
      mediaInput: vi.fn(),
      vectorQuery: vi.fn(async () => ({ matches: [{ id: "v1", score: 0.9 }] })),
      secretGet: vi.fn(async () => "secret-value"),
    };
    mocks.workflowCreate.mockImplementation(async () => ({ id: "workflow-1", status: mocks.workflowStatus }));
    const mediaOutput = { contentType: vi.fn(async () => "video/mp4") };
    const mediaPipeline = {
      transform: vi.fn(() => mediaPipeline),
      output: vi.fn(() => mediaOutput),
    };
    mocks.mediaInput.mockImplementation(() => mediaPipeline);
    const environment = {
      DISPATCH: { get: mocks.dispatchGet },
      RATE: { limit: mocks.limit },
      ANALYTICS: { writeDataPoint: mocks.writeDataPoint },
      EMAIL: { send: mocks.sendEmail },
      VERSION: { id: "version-1", tag: "staging", timestamp: new Date(0) },
      AI: { run: mocks.aiRun },
      WORKFLOW: { create: mocks.workflowCreate },
      IMAGES: { info: mocks.imagesInfo },
      MEDIA: { input: mocks.mediaInput },
      VECTORIZE: { query: mocks.vectorQuery },
      SECRET: { get: mocks.secretGet },
      HYPERDRIVE: {
        connectionString: "postgres://local",
        host: "database.test",
        port: 5432,
        user: "worker",
        password: "hidden",
        database: "app",
      },
    };
    const mockBindings = await import("../fixtures/MockBindings/dist/worker.js");
    const invoke = async (path, init) => mockBindings.default.fetch(
      new Request(`https://worker.test${path}`, init),
      environment,
      createExecutionContext(),
    );

    expect(await (await invoke("/dynamic")).text()).toBe("tenant");
    await expect((await invoke("/rate")).json()).resolves.toEqual({ success: true });
    expect(await (await invoke("/analytics")).text()).toBe("written");
    expect(await (await invoke("/email")).text()).toBe("email-1");
    await expect((await invoke("/metadata")).json()).resolves.toEqual({ id: "version-1", tag: "staging" });
    expect(await (await invoke("/ai")).text()).toBe("ai-result");
    await expect((await invoke("/workflow")).json()).resolves.toEqual({ id: "workflow-1", status: "running" });
    await expect((await invoke("/images", { method: "POST", body: "image" })).json()).resolves.toEqual({ format: "png", width: 8, height: 6 });
    expect(await (await invoke("/media", { method: "POST", body: "media" })).text()).toBe("video/mp4");
    await expect((await invoke("/vectorize")).json()).resolves.toEqual({ matches: [{ id: "v1", score: 0.9 }] });
    expect(await (await invoke("/secret")).text()).toBe("secret-value");
    await expect((await invoke("/hyperdrive")).json()).resolves.toEqual({ host: "database.test", port: 5432, database: "app" });

    expect(mocks.dispatchGet).toHaveBeenCalledWith("tenant-a");
    expect(mocks.writeDataPoint).toHaveBeenCalledWith({ indexes: ["tenant-a"], doubles: [1.5], blobs: ["request"] });
    expect(mocks.sendEmail).toHaveBeenCalledWith({ from: "from@example.test", to: ["to@example.test"], subject: "Subject", text: "Body" });
    expect(mocks.aiRun).toHaveBeenCalledWith("model-a", { prompt: "hello" });
    expect(mocks.workflowCreate).toHaveBeenCalledWith({ id: "workflow-1", params: { value: 42 } });
    expect(mocks.imagesInfo).toHaveBeenCalledOnce();
    expect(mediaPipeline.transform).toHaveBeenCalledWith({ width: 320 });
    expect(mediaPipeline.output).toHaveBeenCalledWith({ mode: "video", format: "mp4" });
    expect(mocks.vectorQuery).toHaveBeenCalledWith([0.25, 0.75], { topK: 3, returnValues: true });
    expect(mocks.secretGet).toHaveBeenCalledOnce();
    expect(mocks.limit).toHaveBeenCalledWith({ key: "user-42" });
  });
});
