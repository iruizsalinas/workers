using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task ServiceProxyDispatchesRpc()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":{"ok":true,"count":3}}""",
            """{"value":null}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-service-rpc");

        var status = await environment.Service("API").InvokeAsync<RoomStatus>(
            "status",
            [42, "compact"]);
        await environment.Service("API").InvokeVoidAsync(
            "touch",
            [new { ttl = 30 }]);

        Assert.NotNull(status);
        Assert.True(status.Ok);
        Assert.Equal(3, status.Count);
        Assert.Equal(["service.rpc", "service.rpc"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("API", call.BindingName));

        using var payload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("status", payload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(42, payload.RootElement.GetProperty("arguments")[0].GetInt32());
        Assert.Equal("compact", payload.RootElement.GetProperty("arguments")[1].GetString());

        using var voidPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("touch", voidPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(30, voidPayload.RootElement.GetProperty("arguments")[0].GetProperty("ttl").GetInt32());
    }

    [Fact]
    public async Task ServiceProxyCreatesTypedRpcClients()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":{"ok":true,"count":3}}""",
            """{"value":null}""",
            """{"handle":"rpc:typed"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-service-typed-rpc");

        var api = environment.Service("API").AsRpc<IRoomRpc>();
        var status = await api.Status(42, "compact");
        await api.Touch(new TouchOptions { Ttl = 30 });
        var counter = await api.NewCounter(1);

        Assert.NotNull(status);
        Assert.True(status.Ok);
        Assert.Equal(3, status.Count);
        Assert.NotNull(counter);
        Assert.Equal(["service.rpc", "service.rpc", "service.rpcStub"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("API", call.BindingName));

        using var statusPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("Status", statusPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(42, statusPayload.RootElement.GetProperty("arguments")[0].GetInt32());
        Assert.Equal("compact", statusPayload.RootElement.GetProperty("arguments")[1].GetString());

        using var touchPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("Touch", touchPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(30, touchPayload.RootElement.GetProperty("arguments")[0].GetProperty("ttl").GetInt32());

        using var counterPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("NewCounter", counterPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(1, counterPayload.RootElement.GetProperty("arguments")[0].GetInt32());
    }

    [Fact]
    public async Task ServiceProxyDispatchesRpcStubOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"rpc:1"}""",
            """{"value":2}""",
            """{"handle":"rpc:2"}""",
            """{"handle":"rpc:3"}""",
            "{}",
            "{}",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-service-rpc-stub");

        var counter = await environment.Service("API").InvokeStubAsync("newCounter", [1]);
        var count = await counter.CallAsync<int>([2]);
        var child = await counter.InvokeStubAsync("child", [counter]);
        var duplicate = await counter.DuplicateAsync();
        await child.DisposeAsync();
        await duplicate.DisposeAsync();
        await counter.DisposeAsync();

        Assert.Equal(2, count);
        Assert.Equal(
            [
                "service.rpcStub",
                "rpc.stub.call",
                "rpc.stub.invokeStub",
                "rpc.stub.dup",
                "rpc.stub.dispose",
                "rpc.stub.dispose",
                "rpc.stub.dispose"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.Equal("API", dispatcher.Invocations[0].BindingName);
        Assert.All(dispatcher.Invocations.Skip(1), call => Assert.Equal("$rpc", call.BindingName));

        using var createPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("newCounter", createPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(1, createPayload.RootElement.GetProperty("arguments")[0].GetInt32());

        using var callPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("rpc:1", callPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal(2, callPayload.RootElement.GetProperty("arguments")[0].GetInt32());

        using var childPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("child", childPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal("rpc:1", childPayload.RootElement.GetProperty("arguments")[0].GetProperty("rpcStubHandle").GetString());

        using var disposePayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal("rpc:2", disposePayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task RpcStubCreatesTypedRpcClients()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"rpc:1"}""",
            """{"value":4}""",
            """{"handle":"rpc:2"}""",
            "{}",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-stub-typed-rpc");

        var counter = await environment.Service("API").InvokeStubAsync("newCounter");
        var typed = counter.AsRpc<ICounterRpc>();
        var value = await typed.Add(3);
        var child = await typed.Child(counter);
        await child.DisposeAsync();
        await counter.DisposeAsync();

        Assert.Equal(4, value);
        Assert.Equal(
            ["service.rpcStub", "rpc.stub.invoke", "rpc.stub.invokeStub", "rpc.stub.dispose", "rpc.stub.dispose"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var addPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("rpc:1", addPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("Add", addPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(3, addPayload.RootElement.GetProperty("arguments")[0].GetInt32());

        using var childPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("Child", childPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal("rpc:1", childPayload.RootElement.GetProperty("arguments")[0].GetProperty("rpcStubHandle").GetString());
    }

    [Fact]
    public async Task AssetsFetcherDispatchesRequestEnvelope()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("asset", 206));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-assets");

        var result = await environment.Assets("ASSETS").FetchAsync("https://assets.example/logo.png");

        Assert.Equal(206, result.Status);
        Assert.Equal("asset", result.Body.AsText());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("service.fetch", invocation.Operation);
        Assert.Equal("ASSETS", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("https://assets.example/logo.png", payload.RootElement.GetProperty("request").GetProperty("url").GetString());
    }

    [Fact]
    public async Task MtlsCertificateDispatchesFetcherRequest()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("mtls", 202));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-mtls");

        var result = await environment.MtlsCertificate("MY_CERT").FetchAsync("https://secured-origin.example/data");

        Assert.Equal(202, result.Status);
        Assert.Equal("mtls", result.Body.AsText());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("service.fetch", invocation.Operation);
        Assert.Equal("MY_CERT", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("https://secured-origin.example/data", payload.RootElement.GetProperty("request").GetProperty("url").GetString());
    }

    [Fact]
    public async Task DynamicDispatcherFetchesNamespacedWorker()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("dynamic", 204));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-dynamic-dispatcher");

        var result = await environment.DynamicDispatcher("DISPATCHER")
            .Get("tenant-worker")
            .FetchAsync("https://tenant.example/path");

        Assert.Equal(204, result.Status);
        Assert.Equal("dynamic", result.Body.AsText());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("dynamicDispatcher.fetch", invocation.Operation);
        Assert.Equal("DISPATCHER", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("tenant-worker", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("https://tenant.example/path", payload.RootElement.GetProperty("fetch").GetProperty("request").GetProperty("url").GetString());
    }

    [Fact]
    public async Task DynamicDispatcherDispatchesRpc()
    {
        var dispatcher = new CapturingDispatcher(
            """{"value":{"ok":true,"count":5}}""",
            """{"handle":"rpc:dynamic"}""",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-dynamic-dispatcher-rpc");

        var service = environment.DynamicDispatcher("DISPATCHER").Get("tenant-worker");
        var status = await service.InvokeAsync<RoomStatus>("status", [7]);
        var stub = await service.InvokeStubAsync("newSession", [8]);
        await stub.DisposeAsync();

        Assert.NotNull(status);
        Assert.True(status.Ok);
        Assert.Equal(5, status.Count);
        Assert.Equal(["dynamicDispatcher.rpc", "dynamicDispatcher.rpcStub", "rpc.stub.dispose"], dispatcher.Invocations.Select(static call => call.Operation));
        var invocation = dispatcher.Invocations[0];
        Assert.Equal("dynamicDispatcher.rpc", invocation.Operation);
        Assert.Equal("DISPATCHER", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("tenant-worker", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("status", payload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(7, payload.RootElement.GetProperty("arguments")[0].GetInt32());

        using var stubPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("tenant-worker", stubPayload.RootElement.GetProperty("name").GetString());
        Assert.Equal("newSession", stubPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(8, stubPayload.RootElement.GetProperty("arguments")[0].GetInt32());
    }

    [Fact]
    public async Task DynamicDispatcherCreatesTypedRpcClients()
    {
        var dispatcher = new CapturingDispatcher("""{"value":{"ok":true,"count":6}}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-dynamic-typed-rpc");

        var status = await environment.DynamicDispatcher("DISPATCHER")
            .GetRpc<IRoomRpc>("tenant-worker")
            .Status(7, "compact");

        Assert.NotNull(status);
        Assert.True(status.Ok);
        Assert.Equal(6, status.Count);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("dynamicDispatcher.rpc", invocation.Operation);
        Assert.Equal("DISPATCHER", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("tenant-worker", payload.RootElement.GetProperty("name").GetString());
        Assert.Equal("Status", payload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(7, payload.RootElement.GetProperty("arguments")[0].GetInt32());
        Assert.Equal("compact", payload.RootElement.GetProperty("arguments")[1].GetString());
    }

    [Fact]
    public async Task QueueProxyDispatchesSendOperations()
    {
        var dispatcher = new CapturingDispatcher("{}", "{}", "{}", "{}", "{}", "{}", "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-4");

        await environment.Queue("JOBS").SendJsonAsync(new { id = 1 }, new QueueSendOptions { DelaySeconds = 5 });
        await environment.Queue("JOBS").SendTextAsync("plain");
        await environment.Queue("JOBS").SendBytesAsync(new byte[] { 1, 2, 3 });
        await environment.Queue("JOBS").SendJsonBatchAsync([new { id = 2 }, new { id = 3 }]);
        await environment.Queue("JOBS").SendTextBatchAsync(["alpha", "beta"], new QueueSendOptions { DelaySeconds = 10 });
        await environment.Queue("JOBS").SendBytesBatchAsync(
            [new byte[] { 4, 5 }, new byte[] { 6 }],
            new QueueSendOptions { DelaySeconds = 20 });
        await environment.Queue("JOBS").SendBatchAsync(
            [
                QueueSendRequest.Json(new { id = 4 }, delaySeconds: 3),
                QueueSendRequest.Text("gamma", delaySeconds: 4),
                QueueSendRequest.Bytes(new byte[] { 7, 8 }, delaySeconds: 5)
            ],
            new QueueSendOptions { DelaySeconds = 30 });

        Assert.Equal(
            ["queue.send", "queue.send", "queue.send", "queue.sendBatch", "queue.sendBatch", "queue.sendBatch", "queue.sendBatch"],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("JOBS", call.BindingName));

        using var firstPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("json", firstPayload.RootElement.GetProperty("contentType").GetString());
        Assert.Equal(5, firstPayload.RootElement.GetProperty("delaySeconds").GetInt32());
        Assert.Equal(1, firstPayload.RootElement.GetProperty("body").GetProperty("id").GetInt32());

        using var secondPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("text", secondPayload.RootElement.GetProperty("contentType").GetString());
        Assert.Equal("plain", secondPayload.RootElement.GetProperty("body").GetString());

        using var bytesPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("bytes", bytesPayload.RootElement.GetProperty("contentType").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), bytesPayload.RootElement.GetProperty("bodyBase64").GetString());

        using var batchPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal(2, batchPayload.RootElement.GetProperty("messages").GetArrayLength());
        Assert.All(batchPayload.RootElement.GetProperty("messages").EnumerateArray(), message =>
            Assert.Equal("json", message.GetProperty("contentType").GetString()));

        using var textBatchPayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal(10, textBatchPayload.RootElement.GetProperty("delaySeconds").GetInt32());
        var textMessages = textBatchPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("text", textMessages[0].GetProperty("contentType").GetString());
        Assert.Equal("alpha", textMessages[0].GetProperty("body").GetString());
        Assert.Equal("text", textMessages[1].GetProperty("contentType").GetString());
        Assert.Equal("beta", textMessages[1].GetProperty("body").GetString());

        using var bytesBatchPayload = JsonDocument.Parse(dispatcher.Invocations[5].PayloadJson);
        Assert.Equal(20, bytesBatchPayload.RootElement.GetProperty("delaySeconds").GetInt32());
        var bytesMessages = bytesBatchPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("bytes", bytesMessages[0].GetProperty("contentType").GetString());
        Assert.Equal(Convert.ToBase64String([4, 5]), bytesMessages[0].GetProperty("bodyBase64").GetString());
        Assert.Equal("bytes", bytesMessages[1].GetProperty("contentType").GetString());
        Assert.Equal(Convert.ToBase64String([6]), bytesMessages[1].GetProperty("bodyBase64").GetString());

        using var requestBatchPayload = JsonDocument.Parse(dispatcher.Invocations[6].PayloadJson);
        Assert.Equal(30, requestBatchPayload.RootElement.GetProperty("delaySeconds").GetInt32());
        var requestMessages = requestBatchPayload.RootElement.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("json", requestMessages[0].GetProperty("contentType").GetString());
        Assert.Equal(3, requestMessages[0].GetProperty("delaySeconds").GetInt32());
        Assert.Equal(4, requestMessages[0].GetProperty("body").GetProperty("id").GetInt32());
        Assert.Equal("text", requestMessages[1].GetProperty("contentType").GetString());
        Assert.Equal(4, requestMessages[1].GetProperty("delaySeconds").GetInt32());
        Assert.Equal("gamma", requestMessages[1].GetProperty("body").GetString());
        Assert.Equal("bytes", requestMessages[2].GetProperty("contentType").GetString());
        Assert.Equal(5, requestMessages[2].GetProperty("delaySeconds").GetInt32());
        Assert.Equal(Convert.ToBase64String([7, 8]), requestMessages[2].GetProperty("bodyBase64").GetString());
    }

    [Fact]
    public async Task QueueProxyDispatchesMetrics()
    {
        var dispatcher = new CapturingDispatcher(
            """{"backlogCount":7,"backlogBytes":4096,"oldestMessageTimestamp":1782734400000}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-queue-metrics");

        var metrics = await environment.Queue("JOBS").MetricsAsync();

        Assert.Equal(7, metrics.BacklogCount);
        Assert.Equal(4096, metrics.BacklogBytes);
        Assert.Equal(1782734400000, metrics.OldestMessageTimestamp);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("queue.metrics", invocation.Operation);
        Assert.Equal("JOBS", invocation.BindingName);
        Assert.Equal("{}", invocation.PayloadJson);
    }
}
