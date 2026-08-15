import { createExecutionContext, runDurableObjectAlarm, runInDurableObject } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { env } from "../support.js";

describe("Durable Object bindings", () => {
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

  it("exposes generated C# Durable Object RPC methods", async () => {
    const stub = env.ECHO.getByName(`rpc-${crypto.randomUUID()}`);

    await expect(stub.greet("Ada")).resolves.toBe("Hello, Ada");
    await expect(stub.store("persisted through storage")).resolves.toBe("persisted through storage");
    await expect(stub.increment()).resolves.toBe(1);
    await expect(stub.increment()).resolves.toBe(2);
  });

  it("runs a generated C# Durable Object alarm handler", async () => {
    const stub = env.ECHO.getByName(`alarm-${crypto.randomUUID()}`);
    await runInDurableObject(stub, async (_instance, state) => {
      await state.storage.setAlarm(Date.now() + 60_000);
    });

    await expect(runDurableObjectAlarm(stub)).resolves.toBe(true);
  });
});
