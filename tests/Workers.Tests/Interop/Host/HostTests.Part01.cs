using System.Runtime.Versioning;
using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

[SupportedOSPlatform("browser")]

public sealed partial class HostTests
{
    [Fact]
    public async Task FetchDispatchesToManifestEntrypoint()
    {
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchAsync)),
            request = RequestEnvelope.FromRequest(Request.Get("https://example.com/hello")),
            environment = new
            {
                bindings = new Dictionary<string, object?>
                {
                    ["GREETING"] = "hello"
                }
            }
        };

        var json = await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));
        var response = JsonSerializer.Deserialize<ResponseEnvelope>(json, JsonOptions)!.ToResponse();

        Assert.Equal(200, response.Status);
        Assert.Equal("hello GET /hello", response.Body.AsText());
    }

    [Fact]
    public async Task FetchDispatchIgnoresSameNameHelperMethods()
    {
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchWithSameNameHelperAsync)),
            request = RequestEnvelope.FromRequest(Request.Get("https://example.com/helper")),
            environment = new
            {
                bindings = new Dictionary<string, object?>()
            }
        };

        var json = await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));
        var response = JsonSerializer.Deserialize<ResponseEnvelope>(json, JsonOptions)!.ToResponse();

        Assert.Equal(200, response.Status);
        Assert.Equal("entrypoint", response.Body.AsText());
    }

    [Fact]
    public async Task FetchEntrypointCanUseKvBindingProxy()
    {
        var dispatcher = new CapturingDispatcher("""{"value":"from-kv"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchKvAsync)),
            request = RequestEnvelope.FromRequest(Request.Get("https://example.com/kv")),
            environment = new
            {
                invocationId = "invocation-kv",
                bindings = new Dictionary<string, object?>()
            }
        };

        var json = await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));
        var response = JsonSerializer.Deserialize<ResponseEnvelope>(json, JsonOptions)!.ToResponse();

        Assert.Equal("from-kv", response.Body.AsText());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation-kv", invocation.InvocationId);
        Assert.Equal("CACHE", invocation.BindingName);
        Assert.Equal("kv.getText", invocation.Operation);
    }

    [Fact]
    public async Task FetchReturnsWaitUntilHandles()
    {
        TestWorker.WaitUntilCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchWaitUntilAsync)),
            request = RequestEnvelope.FromRequest(Request.Get("https://example.com/wait")),
            environment = new
            {
                bindings = new Dictionary<string, object?>()
            }
        };

        var json = await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));
        var envelope = JsonSerializer.Deserialize<ResponseEnvelope>(json, JsonOptions)!;
        var handle = Assert.Single(envelope.WaitUntilHandles);

        var waitTask = Host.WaitUntil(handle);
        Assert.False(waitTask.IsCompleted);

        TestWorker.WaitUntilCompletion.SetResult();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DurableObjectStateCallbackRunsRetainedCallbackOnce()
    {
        var calls = 0;
        var handle = DurableObjectStateCallbackRegistry.Retain(() =>
        {
            calls++;
            return Task.CompletedTask;
        });

        await Host.DurableObjectStateCallback(handle);

        Assert.Equal(1, calls);
        await Assert.ThrowsAsync<WorkersException>(() => Host.DurableObjectStateCallback(handle));
    }

    [Fact]
    public async Task DurableObjectStateCallbackPropagatesFailuresAndReleasesHandle()
    {
        var handle = DurableObjectStateCallbackRegistry.Retain(() =>
            throw new InvalidOperationException("init failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Host.DurableObjectStateCallback(handle));

        Assert.Equal("init failed", ex.Message);
        await Assert.ThrowsAsync<WorkersException>(() => Host.DurableObjectStateCallback(handle));
    }

    [Fact]
    public async Task FetchContextReadsPropsAndPassThroughFlag()
    {
        TestWorker.LastPropsSummary = null;
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchContextAsync)),
            request = RequestEnvelope.FromRequest(Request.Get("https://example.com/context")),
            environment = new
            {
                bindings = new Dictionary<string, object?>()
            },
            context = new
            {
                props = new
                {
                    clientId = "frontend",
                    permissions = new[] { "read", "write" }
                }
            }
        };

        var json = await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));
        var envelope = JsonSerializer.Deserialize<ResponseEnvelope>(json, JsonOptions)!;

        Assert.True(envelope.PassThroughOnException);
        Assert.Equal("frontend:read,write", TestWorker.LastPropsSummary);
    }

    [Fact]
    public async Task FetchEntrypointCanReadObjectVariables()
    {
        TestWorker.LastObjectVarSummary = null;
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchObjectVarAsync)),
            request = RequestEnvelope.FromRequest(Request.Get("https://example.com/config")),
            environment = new
            {
                bindings = new Dictionary<string, object?>
                {
                    ["CONFIG"] = new
                    {
                        clientId = "frontend",
                        permissions = new[] { "read", "write" }
                    }
                }
            }
        };

        await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("frontend:read,write", TestWorker.LastObjectVarSummary);
    }

    [Fact]
    public async Task FetchEntrypointCanReadCloudflareMetadata()
    {
        TestWorker.LastCfSummary = null;
        var payload = new
        {
            manifest = Manifest("fetch", nameof(TestWorker.FetchCfAsync)),
            request = new
            {
                url = "https://example.com/cf",
                method = "GET",
                headers = Array.Empty<Header>(),
                bodyBase64 = (string?)null,
                cf = new
                {
                    colo = "CDG",
                    country = "FR",
                    asn = 13335
                }
            }
        };

        await Host.Fetch(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("CDG:FR:13335", TestWorker.LastCfSummary);
    }

    [Fact]
    public async Task ScheduledDispatchesToManifestEntrypoint()
    {
        TestWorker.LastCron = null;
        TestWorker.LastScheduleSummary = null;
        var payload = new
        {
            manifest = Manifest("scheduled", nameof(TestWorker.ScheduledAsync)),
            @event = new
            {
                cron = "*/5 * * * *",
                type = "scheduled",
                scheduledTime = DateTimeOffset.Parse("2026-06-29T12:00:00Z")
            }
        };

        await Host.Scheduled(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("*/5 * * * *", TestWorker.LastCron);
        Assert.Equal("scheduled:1782734400000", TestWorker.LastScheduleSummary);
    }

    [Fact]
    public async Task ScheduledDispatchUsesManualCronWhenLocalTriggerOmitsCron()
    {
        TestWorker.LastCron = null;
        TestWorker.LastScheduleSummary = null;
        var payload = new
        {
            manifest = Manifest("scheduled", nameof(TestWorker.ScheduledAsync)),
            @event = new
            {
                cron = "",
                type = "scheduled",
                scheduledTime = DateTimeOffset.Parse("2026-06-29T12:00:00Z")
            }
        };

        await Host.Scheduled(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("manual", TestWorker.LastCron);
        Assert.Equal("scheduled:1782734400000", TestWorker.LastScheduleSummary);
    }

    [Fact]
    public async Task QueueDispatchesTypedMessages()
    {
        TestWorker.LastQueueValue = null;
        TestWorker.LastQueueBatchSummary = null;
        var payload = new
        {
            manifest = Manifest("queue", nameof(TestWorker.QueueAsync)),
            batch = new
            {
                queue = "jobs",
                messages = new[]
                {
                    new
                    {
                        id = "msg-1",
                        timestamp = DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
                        attempts = 3,
                        body = new { value = "queued" }
                    }
                }
            }
        };

        var json = await Host.Queue(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("queued", TestWorker.LastQueueValue);
        Assert.Equal("jobs:msg-1:3:1:queued", TestWorker.LastQueueBatchSummary);
        using var result = JsonDocument.Parse(json);
        var disposition = Assert.Single(result.RootElement.GetProperty("queueDispositions").EnumerateArray());
        Assert.Equal(0, disposition.GetProperty("index").GetInt32());
        Assert.Equal("ack", disposition.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task QueueDispatchesRetryDispositionsWithOptions()
    {
        var payload = new
        {
            manifest = Manifest("queue", nameof(TestWorker.QueueRetryAsync)),
            batch = new
            {
                messages = new[]
                {
                    new
                    {
                        id = "msg-1",
                        timestamp = DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
                        body = new { value = "retry" }
                    }
                }
            }
        };

        var json = await Host.Queue(JsonSerializer.Serialize(payload, JsonOptions));

        using var result = JsonDocument.Parse(json);
        var disposition = Assert.Single(result.RootElement.GetProperty("queueDispositions").EnumerateArray());
        Assert.Equal(0, disposition.GetProperty("index").GetInt32());
        Assert.Equal("retry", disposition.GetProperty("kind").GetString());
        Assert.Equal(30, disposition.GetProperty("options").GetProperty("delaySeconds").GetInt32());
    }

    [Fact]
    public async Task QueueDispatchesBinaryMessages()
    {
        TestWorker.LastQueueBytesSummary = null;
        var payload = new
        {
            manifest = Manifest("queue", nameof(TestWorker.QueueBytesAsync)),
            batch = new
            {
                messages = new[]
                {
                    new
                    {
                        id = "msg-bin",
                        timestamp = DateTimeOffset.Parse("2026-06-29T12:00:00Z"),
                        attempts = 2,
                        body = (object?)null,
                        bodyBase64 = Convert.ToBase64String([1, 2, 255])
                    }
                }
            }
        };

        var json = await Host.Queue(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("msg-bin:2:0102FF", TestWorker.LastQueueBytesSummary);
        using var result = JsonDocument.Parse(json);
        var disposition = Assert.Single(result.RootElement.GetProperty("queueDispositions").EnumerateArray());
        Assert.Equal(0, disposition.GetProperty("index").GetInt32());
        Assert.Equal("ack", disposition.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task EmailDispatchesForwardableMessage()
    {
        TestWorker.LastEmailSummary = null;
        TestWorker.LastRawEmail = null;

        var dispatcher = new CapturingDispatcher(invocation => invocation.Operation switch
        {
            "email.rawBytes" => """{"bodyBase64":"cmF3LW1pbWU="}""",
            "email.forward" => """{"messageId":"forwarded"}""",
            "email.replyRaw" => """{"messageId":"replied"}""",
            "email.reject" => "{}",
            _ => throw new InvalidOperationException(invocation.Operation)
        });
        using var _ = BindingDispatcher.Use(dispatcher);

        var payload = new
        {
            manifest = Manifest("email", nameof(TestWorker.EmailAsync)),
            environment = new
            {
                invocationId = "invocation-email",
                bindings = new Dictionary<string, object?>()
            },
            message = new
            {
                invocationId = "invocation-email",
                handle = "email:1",
                from = "sender@example.com",
                to = "inbox@example.com",
                headers = new[]
                {
                    new Header("subject", "hello")
                },
                rawSize = 8
            }
        };

        await Host.Email(JsonSerializer.Serialize(payload, JsonOptions));

        Assert.Equal("sender@example.com>inbox@example.com:hello:8", TestWorker.LastEmailSummary);
        Assert.Equal("raw-mime", TestWorker.LastRawEmail);
        Assert.Equal(
            ["email.rawBytes", "email.forward", "email.replyRaw", "email.reject"],
            dispatcher.Invocations.Select(static invocation => invocation.Operation).ToArray());
        Assert.All(dispatcher.Invocations, invocation =>
        {
            Assert.Equal("invocation-email", invocation.InvocationId);
            Assert.Equal("$email", invocation.BindingName);
        });
    }
}
