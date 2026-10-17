import { createExecutionContext, runDurableObjectAlarm, runInDurableObject } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { env } from "../support.js";

describe("Durable Object bindings", () => {
  it("stores numeric dictionaries as native data rather than helper methods", async () => {
    const stub = env.ECHO.getByName(`stored-collections-${crypto.randomUUID()}`);
    await expect(stub.storedCollections()).resolves.toBe(true);
  });

  it("restores stored class graphs with native cycles intact", async () => {
    const stub = env.ECHO.getByName(`stored-graph-${crypto.randomUUID()}`);
    await expect(stub.storedGraph()).resolves.toBe(true);
  });

  it("persists lists without iterator metadata and restores their enumeration checks", async () => {
    const stub = env.ECHO.getByName(`stored-list-${crypto.randomUUID()}`);
    await expect(stub.storedListEnumeration()).resolves.toBe(true);
  });

  it("runs awaited switch arms and conditional calls inside generated methods", async () => {
    const stub = env.ECHO.getByName(`async-flow-${crypto.randomUUID()}`);
    await expect(stub.asyncControlFlow(0)).resolves.toBe("awaited");
    await expect(stub.asyncControlFlow(1)).resolves.toBe("present");
    await expect(stub.asyncControlFlow(-1)).resolves.toBe("missing");
  });

  it("restarts iterator helpers with their original arguments and current receiver", async () => {
    const stub = env.ECHO.getByName(`iterator-${crypto.randomUUID()}`);
    await expect(stub.iteratorSnapshot(2)).resolves.toEqual([12, 13, 22, 23]);
  });

  it("exports a C# Durable Object class usable by the local namespace", async () => {
    const stub = env.ECHO.getByName(`echo-${crypto.randomUUID()}`);
    const response = await stub.fetch("https://worker.test/native");

    expect(await response.text()).toBe("durable:/native");
  });

  it("uses the Durable Object namespace from generated C#", async () => {
    const nativeBindings = await import("../fixtures/NativeBindings/dist/worker.js");
    const response = await nativeBindings.default.fetch(
      new Request("https://worker.test/durable"),
      env,
      createExecutionContext(),
    );

    expect(await response.text()).toBe("durable:/from-csharp");
  });

  it("routes a stub to the primary instance", async () => {
    const nativeBindings = await import("../fixtures/NativeBindings/dist/worker.js");
    const response = await nativeBindings.default.fetch(
      new Request("https://worker.test/durable-primary"),
      env,
      createExecutionContext(),
    );

    expect(await response.text()).toBe("durable:/primary");
  });

  it("exposes generated C# Durable Object RPC methods", async () => {
    const stub = env.ECHO.getByName(`rpc-${crypto.randomUUID()}`);

    await expect(stub.greet("Ada")).resolves.toBe("Hello, Ada");
    await expect(stub.store("persisted through storage")).resolves.toBe("persisted through storage");
    await expect(stub.increment()).resolves.toBe(1);
    await expect(stub.increment()).resolves.toBe(2);
    await expect(stub.storageLifecycle()).resolves.toBe(true);
    await expect(stub.transactionLifecycle()).resolves.toBe(true);
    await expect(stub.alarmOptionsLifecycle()).resolves.toBe(true);
  });

  it("runs a generated C# Durable Object alarm handler", async () => {
    const stub = env.ECHO.getByName(`alarm-${crypto.randomUUID()}`);
    await runInDurableObject(stub, async (_instance, state) => {
      await state.storage.setAlarm(Date.now() + 60_000);
    });

    await expect(runDurableObjectAlarm(stub)).resolves.toBe(true);
  });
});
