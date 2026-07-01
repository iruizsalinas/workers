using Workers.Build;
using Xunit;

namespace Workers.Tests;

public sealed class ModuleWriterTests
{
    [Fact]
    public void WritesFetchModule()
    {
        var manifest = new BuildManifest(
            "Example.dll",
            "worker.js",
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync")
            ]);

        var module = ModuleWriter.WriteModule(manifest);

        Assert.Contains("import dotnet from \"./dotnet.js\";", module, StringComparison.Ordinal);
        Assert.DoesNotContain("cloudflare:workers", module, StringComparison.Ordinal);
        Assert.Contains("let workerPromise;", module, StringComparison.Ordinal);
        Assert.Contains("workerPromise ??= dotnet(manifest);", module, StringComparison.Ordinal);
        Assert.Contains("async fetch(request, env, ctx)", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async scheduled", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async queue", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async tail", module, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesAllSupportedHandlersInStableOrder()
    {
        var manifest = new BuildManifest(
            "Example.dll",
            "worker.js",
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Queue, "Example.Worker", "QueueAsync"),
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync"),
                new Entrypoint(EntrypointKind.Email, "Example.Worker", "EmailAsync"),
                new Entrypoint(EntrypointKind.Scheduled, "Example.Worker", "ScheduledAsync"),
                new Entrypoint(EntrypointKind.Tail, "Example.Worker", "TailAsync")
            ]);

        var module = ModuleWriter.WriteModule(manifest);

        var fetchIndex = module.IndexOf("async fetch", StringComparison.Ordinal);
        var scheduledIndex = module.IndexOf("async scheduled", StringComparison.Ordinal);
        var queueIndex = module.IndexOf("async queue", StringComparison.Ordinal);
        var emailIndex = module.IndexOf("async email", StringComparison.Ordinal);
        var tailIndex = module.IndexOf("async tail", StringComparison.Ordinal);

        Assert.True(fetchIndex >= 0);
        Assert.True(scheduledIndex > fetchIndex);
        Assert.True(queueIndex > scheduledIndex);
        Assert.True(emailIndex > queueIndex);
        Assert.True(tailIndex > emailIndex);
        Assert.Contains("worker.tail(events, env, ctx)", module, StringComparison.Ordinal);
    }

    [Fact]
    public void EscapesAdapterModulePath()
    {
        var manifest = new BuildManifest(
            "Example.dll",
            "worker.js",
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync")
            ]);

        var module = ModuleWriter.WriteModule(
            manifest,
            new ModuleOptions("./runtime path/worker\"adapter.js"));

        Assert.Contains("\"./runtime path/worker\\u0022adapter.js\"", module, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesDurableObjectExports()
    {
        var manifest = new BuildManifest(
            "Example.dll",
            "worker.js",
            "Example.wasm",
            [
                new Entrypoint(EntrypointKind.Fetch, "Example.Worker", "FetchAsync")
            ])
        {
            DurableObjects =
            [
                new DurableObjectEntrypoint(
                    "CounterObject",
                    "Example.CounterObject",
                    "FetchAsync",
                    "AlarmAsync",
                    "WebSocketMessageAsync",
                    "WebSocketCloseAsync",
                    "WebSocketErrorAsync")
                {
                    RpcMethods =
                    [
                        new DurableObjectRpcMethod("add", "AddAsync")
                    ]
                }
            ]
        };

        var module = ModuleWriter.WriteModule(manifest);

        Assert.Contains("import { DurableObject } from \"cloudflare:workers\";", module, StringComparison.Ordinal);
        Assert.Contains("export class CounterObject extends DurableObject", module, StringComparison.Ordinal);
        Assert.Contains("super(ctx, env);", module, StringComparison.Ordinal);
        Assert.Contains("async fetch(request)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectFetch(\"CounterObject\", this.ctx, request, this.env)", module, StringComparison.Ordinal);
        Assert.Contains("async alarm(alarmInfo)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectAlarm(\"CounterObject\", this.ctx, this.env, alarmInfo ?? null)", module, StringComparison.Ordinal);
        Assert.Contains("async webSocketMessage(ws, message)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectWebSocketMessage(\"CounterObject\", this.ctx, ws, message, this.env)", module, StringComparison.Ordinal);
        Assert.Contains("async webSocketClose(ws, code, reason, wasClean)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectWebSocketClose(\"CounterObject\", this.ctx, ws, code, reason, wasClean, this.env)", module, StringComparison.Ordinal);
        Assert.Contains("async webSocketError(ws, error)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectWebSocketError(\"CounterObject\", this.ctx, ws, error, this.env)", module, StringComparison.Ordinal);
        Assert.Contains("async add(...args)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectRpc(\"CounterObject\", \"add\", this.ctx, this.env, args)", module, StringComparison.Ordinal);
    }

    [Fact]
    public void WritesDurableObjectOnlyModule()
    {
        var manifest = new BuildManifest("Example.dll", "worker.js", "Example.wasm", [])
        {
            DurableObjects =
            [
                new DurableObjectEntrypoint(
                    "RpcOnlyObject",
                    "Example.RpcOnlyObject",
                    FetchMethodName: null,
                    AlarmMethodName: null,
                    WebSocketMessageMethodName: null,
                    WebSocketCloseMethodName: null,
                    WebSocketErrorMethodName: null)
                {
                    RpcMethods =
                    [
                        new DurableObjectRpcMethod("ping", "PingAsync")
                    ]
                }
            ]
        };

        var module = ModuleWriter.WriteModule(manifest);

        Assert.Contains("import { DurableObject } from \"cloudflare:workers\";", module, StringComparison.Ordinal);
        Assert.Contains("export class RpcOnlyObject extends DurableObject", module, StringComparison.Ordinal);
        Assert.Contains("async ping(...args)", module, StringComparison.Ordinal);
        Assert.Contains("worker.durableObjectRpc(\"RpcOnlyObject\", \"ping\", this.ctx, this.env, args)", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async fetch(request, env, ctx)", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async fetch(request)", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async scheduled", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async queue", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async email", module, StringComparison.Ordinal);
        Assert.DoesNotContain("async tail", module, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresAtLeastOneEntrypointOrDurableObject()
    {
        var manifest = new BuildManifest("Example.dll", "worker.js", "Example.wasm", []);

        Assert.Throws<EntrypointException>(() => ModuleWriter.WriteModule(manifest));
    }
}
