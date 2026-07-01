using System.Runtime.Versioning;
using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

[SupportedOSPlatform("browser")]

public sealed partial class HostTests
{
    [Fact]
    public async Task TailDispatchesTraceItemsAndWaitUntil()
    {
        TestWorker.LastTailSummary = null;
        TestWorker.WaitUntilCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var payload = new
        {
            manifest = Manifest("tail", nameof(TestWorker.TailAsync)),
            @event = new
            {
                type = "tail",
                traces = new[]
                {
                    new
                    {
                        scriptName = "producer-worker",
                        @event = new
                        {
                            request = new
                            {
                                cf = new
                                {
                                    colo = "CDG"
                                },
                                headers = new Dictionary<string, string>
                                {
                                    ["authorization"] = "REDACTED",
                                    ["user-agent"] = "unit-test"
                                },
                                method = "GET",
                                url = "https://example.com/REDACTED"
                            },
                            response = new
                            {
                                status = 201
                            }
                        },
                        eventTimestamp = DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
                        logs = new[]
                        {
                            new
                            {
                                timestamp = DateTimeOffset.Parse("2026-06-29T12:00:01Z"),
                                level = "log",
                                message = new object[] { "stored", new { count = 1 } }
                            }
                        },
                        exceptions = new[]
                        {
                            new
                            {
                                timestamp = DateTimeOffset.Parse("2026-06-29T12:00:02Z"),
                                name = "TypeError",
                                message = "broken"
                            }
                        },
                        outcome = "ok"
                    }
                }
            }
        };

        var json = await Host.Tail(JsonSerializer.Serialize(payload, JsonOptions));
        using var result = JsonDocument.Parse(json);
        var handle = Assert.Single(result.RootElement.GetProperty("waitUntilHandles").EnumerateArray()).GetString();

        Assert.Equal("producer-worker:GET:201:ok:log:stored:TypeError:broken:REDACTED:CDG", TestWorker.LastTailSummary);
        Assert.False(string.IsNullOrWhiteSpace(handle));
        var waitTask = Host.WaitUntil(handle!);
        Assert.False(waitTask.IsCompleted);

        TestWorker.WaitUntilCompletion.SetResult();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DurableObjectDispatchesFetchAndAlarmToManifestClass()
    {
        TestDurableObject.LastAlarmSummary = null;
        var dispatcher = new CapturingDispatcher("""{"value":5}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var durableObjectId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        var fetchPayload = new
        {
            manifest = DurableObjectManifest(),
            exportName = "HostCounterObject",
            durableObjectId,
            request = RequestEnvelope.FromRequest(Request.Get("https://do.example/count")),
            environment = new
            {
                invocationId = "invocation-do",
                bindings = new Dictionary<string, object?>
                {
                    ["GREETING"] = "hello"
                }
            }
        };

        var json = await Host.DurableObjectFetch(JsonSerializer.Serialize(fetchPayload, JsonOptions));
        var response = JsonSerializer.Deserialize<ResponseEnvelope>(json, JsonOptions)!.ToResponse();

        var alarmPayload = new
        {
            manifest = DurableObjectManifest(),
            exportName = "HostCounterObject",
            durableObjectId,
            environment = new
            {
                invocationId = "invocation-do",
                bindings = new Dictionary<string, object?>
                {
                    ["GREETING"] = "hello"
                }
            },
            alarmInfo = new
            {
                retryCount = 2,
                isRetry = true
            }
        };

        await Host.DurableObjectAlarm(JsonSerializer.Serialize(alarmPayload, JsonOptions));

        var rpcPayload = new
        {
            manifest = DurableObjectManifest(),
            exportName = "HostCounterObject",
            methodName = "add",
            durableObjectId,
            environment = new
            {
                invocationId = "invocation-do",
                bindings = new Dictionary<string, object?>
                {
                    ["GREETING"] = "hello"
                }
            },
            arguments = new object[] { 4, 9 }
        };

        var rpcJson = await Host.DurableObjectRpc(JsonSerializer.Serialize(rpcPayload, JsonOptions));
        using var rpcResult = JsonDocument.Parse(rpcJson);

        Assert.Equal("hello:cccc:/count:5", response.Body.AsText());
        Assert.Equal("hello:2:True", TestDurableObject.LastAlarmSummary);
        Assert.Equal(13, rpcResult.RootElement.GetProperty("value").GetInt32());

        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation-do", invocation.InvocationId);
        Assert.Equal("$durableObjectState", invocation.BindingName);
        Assert.Equal("durable.storage.get", invocation.Operation);
    }

    [Fact]
    public async Task DurableObjectRpcCanReturnManagedRpcTarget()
    {
        CounterRpcTarget.DisposeCount = 0;
        var durableObjectId = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        var rpcPayload = new
        {
            manifest = DurableObjectManifest(),
            exportName = "HostCounterObject",
            methodName = "counter",
            durableObjectId,
            environment = new
            {
                invocationId = "invocation-do-rpc-target",
                bindings = new Dictionary<string, object?>
                {
                    ["GREETING"] = "hello"
                }
            },
            arguments = new object[] { 10 }
        };

        var rpcJson = await Host.DurableObjectRpc(JsonSerializer.Serialize(rpcPayload, JsonOptions));
        using var rpcResult = JsonDocument.Parse(rpcJson);
        var handle = rpcResult.RootElement.GetProperty("rpcTargetHandle").GetString();
        Assert.NotNull(handle);
        Assert.Equal(JsonValueKind.Null, rpcResult.RootElement.GetProperty("value").ValueKind);

        var invokeJson = await Host.ManagedRpcTargetInvoke(JsonSerializer.Serialize(
            new
            {
                invocationId = "invocation-managed-rpc-target",
                handle,
                methodName = "Increment",
                arguments = new object[] { 5 }
            },
            JsonOptions));
        using var invokeResult = JsonDocument.Parse(invokeJson);
        Assert.Equal(15, invokeResult.RootElement.GetProperty("value").GetInt32());

        var duplicateJson = Host.ManagedRpcTargetDup(JsonSerializer.Serialize(new { handle }, JsonOptions));
        using var duplicateResult = JsonDocument.Parse(duplicateJson);
        var duplicateHandle = duplicateResult.RootElement.GetProperty("handle").GetString();
        Assert.NotNull(duplicateHandle);
        Assert.NotEqual(handle, duplicateHandle);

        await Host.ManagedRpcTargetDispose(JsonSerializer.Serialize(new { handle }, JsonOptions));
        Assert.Equal(0, CounterRpcTarget.DisposeCount);

        await Host.ManagedRpcTargetDispose(JsonSerializer.Serialize(new { handle = duplicateHandle }, JsonOptions));
        Assert.Equal(1, CounterRpcTarget.DisposeCount);
    }

    [Fact]
    public async Task DurableObjectRpcCanAcceptRpcStubParameter()
    {
        var dispatcher = new CapturingDispatcher("""{"value":17}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var durableObjectId = "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
        var rpcPayload = new
        {
            manifest = DurableObjectManifest(),
            exportName = "HostCounterObject",
            methodName = "useStub",
            durableObjectId,
            environment = new
            {
                invocationId = "invocation-do-rpc-stub-arg",
                bindings = new Dictionary<string, object?>
                {
                    ["GREETING"] = "hello"
                }
            },
            arguments = new object[]
            {
                new { rpcStubHandle = "rpc:callback" },
                12
            }
        };

        var rpcJson = await Host.DurableObjectRpc(JsonSerializer.Serialize(rpcPayload, JsonOptions));
        using var rpcResult = JsonDocument.Parse(rpcJson);

        Assert.Equal(17, rpcResult.RootElement.GetProperty("value").GetInt32());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation-do-rpc-stub-arg", invocation.InvocationId);
        Assert.Equal("$rpc", invocation.BindingName);
        Assert.Equal("rpc.stub.invoke", invocation.Operation);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("rpc:callback", payload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("apply", payload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(12, payload.RootElement.GetProperty("arguments")[0].GetInt32());
    }

    [Fact]
    public async Task DurableObjectDispatchesHibernatableWebSocketEventsToManifestClass()
    {
        TestDurableObject.LastWebSocketMessageSummary = null;
        TestDurableObject.LastWebSocketCloseSummary = null;
        TestDurableObject.LastWebSocketErrorSummary = null;
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var durableObjectId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        var environment = new
        {
            invocationId = "invocation-do-websocket",
            bindings = new Dictionary<string, object?>
            {
                ["GREETING"] = "hello"
            }
        };

        await Host.DurableObjectWebSocketMessage(JsonSerializer.Serialize(
            new
            {
                manifest = DurableObjectManifest(),
                exportName = "HostCounterObject",
                durableObjectId,
                environment,
                webSocketHandle = "ws:message",
                message = new
                {
                    text = "ping",
                    bodyBase64 = (string?)null
                }
            },
            JsonOptions));

        await Host.DurableObjectWebSocketClose(JsonSerializer.Serialize(
            new
            {
                manifest = DurableObjectManifest(),
                exportName = "HostCounterObject",
                durableObjectId,
                environment,
                webSocketHandle = "ws:close",
                code = 1001,
                reason = "going away",
                wasClean = true
            },
            JsonOptions));

        await Host.DurableObjectWebSocketError(JsonSerializer.Serialize(
            new
            {
                manifest = DurableObjectManifest(),
                exportName = "HostCounterObject",
                durableObjectId,
                environment,
                webSocketHandle = "ws:error",
                error = new
                {
                    message = "broken"
                }
            },
            JsonOptions));

        Assert.Equal("hello:ws:message:ping", TestDurableObject.LastWebSocketMessageSummary);
        Assert.Equal("hello:ws:close:1001:going away:True", TestDurableObject.LastWebSocketCloseSummary);
        Assert.Equal("hello:ws:error:broken", TestDurableObject.LastWebSocketErrorSummary);
        Assert.Equal(["websocket.sendText"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation-do-websocket", invocation.InvocationId);
        Assert.Equal("$websocket", invocation.BindingName);
        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("ws:message", payload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("echo:ping", payload.RootElement.GetProperty("message").GetString());
    }
}
