import { createExecutionContext } from "cloudflare:test";
import { describe, expect, it, vi } from "vitest";
import "../support.js";

describe("mocked fetcher bindings", () => {
  it("uses explicit service, assets, and mTLS fetcher mocks", async () => {
    const serviceFetch = vi.fn(async () => new Response("service"));
    const assetsFetch = vi.fn(async () => new Response("asset"));
    const mtlsFetch = vi.fn(async () => new Response("mtls"));
    const mockBindings = await import("../fixtures/MockBindings/dist/worker.js");
    const mockEnvironment = {
      SERVICE: { fetch: serviceFetch },
      ASSETS: { fetch: assetsFetch },
      MTLS: { fetch: mtlsFetch },
    };

    const service = await mockBindings.default.fetch(new Request("https://worker.test/service"), mockEnvironment, createExecutionContext());
    const assets = await mockBindings.default.fetch(new Request("https://worker.test/assets"), mockEnvironment, createExecutionContext());
    const mtls = await mockBindings.default.fetch(new Request("https://worker.test/mtls"), mockEnvironment, createExecutionContext());

    expect(await service.text()).toBe("service");
    expect(await assets.text()).toBe("asset");
    expect(await mtls.text()).toBe("mtls");
    expect(serviceFetch).toHaveBeenCalledWith("https://service.test/request");
    expect(assetsFetch).toHaveBeenCalledWith("https://assets.test/logo.svg");
    expect(mtlsFetch).toHaveBeenCalledWith("https://mtls.test/private");
  });

  it("lowers service RPC to a named method call", async () => {
    const greet = vi.fn(async (name) => `Hello, ${name}`);
    const mockBindings = await import("../fixtures/MockBindings/dist/worker.js");
    const response = await mockBindings.default.fetch(
      new Request("https://worker.test/rpc"),
      { SERVICE: { greet } },
      createExecutionContext(),
    );

    expect(greet).toHaveBeenCalledWith("Ada");
    expect(await response.text()).toBe("Hello, Ada");
  });
});
