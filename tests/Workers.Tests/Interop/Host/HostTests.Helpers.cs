using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Tests;

[SupportedOSPlatform("browser")]

public sealed partial class HostTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static object Manifest(string kind, string methodName) =>
        new
        {
            entryAssembly = typeof(TestWorker).Assembly.GetName().Name + ".dll",
            entrypoints = new[]
            {
                new
                {
                    kind,
                    containingType = typeof(TestWorker).FullName!,
                    methodName
                }
            }
        };

    private static object DurableObjectManifest() =>
        new
        {
            entryAssembly = typeof(TestDurableObject).Assembly.GetName().Name + ".dll",
            entrypoints = Array.Empty<object>(),
            durableObjects = new[]
            {
                new
                {
                    exportName = "HostCounterObject",
                    containingType = typeof(TestDurableObject).FullName!,
                    fetchMethodName = nameof(TestDurableObject.FetchAsync),
                    alarmMethodName = nameof(TestDurableObject.AlarmAsync),
                    webSocketMessageMethodName = nameof(TestDurableObject.WebSocketMessageAsync),
                    webSocketCloseMethodName = nameof(TestDurableObject.WebSocketCloseAsync),
                    webSocketErrorMethodName = nameof(TestDurableObject.WebSocketErrorAsync),
                    rpcMethods = new[]
                    {
                        new
                        {
                            name = "add",
                            methodName = nameof(TestDurableObject.AddAsync)
                        },
                        new
                        {
                            name = "counter",
                            methodName = nameof(TestDurableObject.CounterAsync)
                        },
                        new
                        {
                            name = "useStub",
                            methodName = nameof(TestDurableObject.UseStubAsync)
                        }
                    }
                }
            }
        };

    private sealed class QueueBody
    {
        public required string Value { get; init; }
    }

    private sealed class ContextProps
    {
        public required string ClientId { get; init; }

        public required IReadOnlyList<string> Permissions { get; init; }
    }

    private sealed class RequestCf
    {
        public required string Colo { get; init; }

        public required string Country { get; init; }

        public int Asn { get; init; }
    }

    private static class TestWorker
    {
        public static string? LastCron { get; set; }

        public static string? LastScheduleSummary { get; set; }

        public static string? LastQueueValue { get; set; }

        public static string? LastQueueBatchSummary { get; set; }

        public static string? LastQueueBytesSummary { get; set; }

        public static string? LastEmailSummary { get; set; }

        public static string? LastRawEmail { get; set; }

        public static string? LastTailSummary { get; set; }

        public static TaskCompletionSource? WaitUntilCompletion { get; set; }

        public static string? LastPropsSummary { get; set; }

        public static string? LastObjectVarSummary { get; set; }

        public static string? LastCfSummary { get; set; }

        public static Task<Response> FetchAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;
            return Task.FromResult(Response.Text($"{environment.Var("GREETING")} {request.Method} {request.Path}"));
        }

        public static async Task<Response> FetchKvAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = request;
            _ = context;
            var value = await environment.Kv("CACHE").GetTextAsync("message");
            return Response.Text(value ?? "");
        }

        public static Task<Response> FetchWaitUntilAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = request;
            _ = environment;
            context.WaitUntil(WaitUntilCompletion!.Task);
            return Task.FromResult(Response.Text("scheduled"));
        }

        public static Task<Response> FetchContextAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = request;
            _ = environment;
            var props = context.Props<ContextProps>();
            LastPropsSummary = $"{props.ClientId}:{string.Join(",", props.Permissions)}";
            context.PassThroughOnException();
            return Task.FromResult(Response.Text("context"));
        }

        public static Task<Response> FetchObjectVarAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = request;
            _ = context;
            var config = environment.ObjectVar<ContextProps>("CONFIG");
            LastObjectVarSummary = $"{config.ClientId}:{string.Join(",", config.Permissions)}";
            return Task.FromResult(Response.Text("object-var"));
        }

        public static Task<Response> FetchCfAsync(
            Request request,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;
            var cf = request.CfAs<RequestCf>();
            LastCfSummary = $"{cf.Colo}:{cf.Country}:{cf.Asn}";
            return Task.FromResult(Response.Text("cf"));
        }

        public static ValueTask ScheduledAsync(
            ScheduledEvent scheduledEvent,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;
            LastCron = scheduledEvent.Cron;
            LastScheduleSummary = $"{scheduledEvent.Type}:{scheduledEvent.Schedule}";
            return ValueTask.CompletedTask;
        }

        public static Task QueueAsync(
            QueueMessageBatch<QueueBody> batch,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;
            LastQueueValue = batch.Messages.Single().Body.Value;
            LastQueueBatchSummary = $"{batch.Queue}:{batch[0].Id}:{batch[0].Attempts}:{batch.Count}:{string.Join(",", batch.Select(static message => message.Body.Value))}";
            batch.AckAll();
            return Task.CompletedTask;
        }

        public static Task QueueRetryAsync(
            QueueMessageBatch<QueueBody> batch,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;
            batch.Messages.Single().Retry(new QueueRetryOptions { DelaySeconds = 30 });
            return Task.CompletedTask;
        }

        public static Task QueueBytesAsync(
            QueueMessageBatch<byte[]> batch,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;
            var message = batch.Messages.Single();
            LastQueueBytesSummary = $"{message.Id}:{message.Attempts}:{Convert.ToHexString(message.Body)}";
            message.Ack();
            return Task.CompletedTask;
        }

        public static async Task EmailAsync(
            ForwardableEmailMessage message,
            Env environment,
            Context context)
        {
            _ = environment;
            _ = context;

            LastEmailSummary = $"{message.From}>{message.To}:{message.Headers.Get("subject")}:{message.RawSize}";
            LastRawEmail = (await message.RawAsync()).AsText();
            _ = await message.ForwardAsync("archive@example.com", new Headers().Set("x-forwarded-by", "test"));
            _ = await message.ReplyRawAsync("inbox@example.com", "sender@example.com", "raw-reply");
            await message.RejectAsync("blocked");
        }

        public static Task TailAsync(
            TailEvent tailEvent,
            Env environment,
            Context context)
        {
            _ = environment;
            var trace = tailEvent.Traces.Single();
            var log = trace.Logs.Single();
            var exception = trace.Exceptions.Single();
            var request = trace.Event!.Request;
            var response = trace.Event.Response;

            LastTailSummary = string.Join(
                ":",
                trace.ScriptName,
                request.Method,
                response.Status,
                trace.Outcome,
                log.Level,
                log.Message[0].GetString(),
                exception.Name,
                exception.Message.GetString(),
                request.Headers["authorization"],
                request.Cf!.Value.GetProperty("colo").GetString());

            context.WaitUntil(WaitUntilCompletion!.Task);
            return Task.CompletedTask;
        }
    }

    [DurableObject("HostCounterObject")]
    private sealed class TestDurableObject
    {
        private readonly DurableObjectState _state;
        private readonly Env _environment;

        public TestDurableObject(DurableObjectState state, Env environment)
        {
            _state = state;
            _environment = environment;
        }

        public static string? LastAlarmSummary { get; set; }

        public static string? LastWebSocketMessageSummary { get; set; }

        public static string? LastWebSocketCloseSummary { get; set; }

        public static string? LastWebSocketErrorSummary { get; set; }

        public async Task<Response> FetchAsync(Request request)
        {
            var count = await _state.Storage.GetJsonAsync<int>("count");
            return Response.Text(
                $"{_environment.Var("GREETING")}:{_state.Id.Value[..4]}:{request.Path}:{count}");
        }

        public Task AlarmAsync(AlarmInfo alarmInfo)
        {
            LastAlarmSummary = $"{_environment.Var("GREETING")}:{alarmInfo.RetryCount}:{alarmInfo.IsRetry}";
            return Task.CompletedTask;
        }

        public ValueTask<int> AddAsync(int left, int right) =>
            ValueTask.FromResult(left + right);

        public Task<CounterRpcTarget> CounterAsync(int initialValue) =>
            Task.FromResult(new CounterRpcTarget(initialValue));

        public Task<int> UseStubAsync(RpcStub callback, int value) =>
            callback.InvokeAsync<int>("apply", [value]);

        public async Task WebSocketMessageAsync(WebSocket socket, WebSocketMessage message)
        {
            LastWebSocketMessageSummary = $"{_environment.Var("GREETING")}:{socket.Handle}:{message.Text}";
            await socket.SendTextAsync($"echo:{message.Text}");
        }

        public Task WebSocketCloseAsync(WebSocket socket, ushort code, string reason, bool wasClean)
        {
            LastWebSocketCloseSummary = $"{_environment.Var("GREETING")}:{socket.Handle}:{code}:{reason}:{wasClean}";
            return Task.CompletedTask;
        }

        public Task WebSocketErrorAsync(WebSocket socket, WebSocketError error)
        {
            LastWebSocketErrorSummary = $"{_environment.Var("GREETING")}:{socket.Handle}:{error.Message}";
            return Task.CompletedTask;
        }
    }

    private sealed class CounterRpcTarget(int value) : RpcTarget, IAsyncDisposable
    {
        private int _value = value;

        public static int DisposeCount { get; set; }

        public int Increment(int amount)
        {
            _value += amount;
            return _value;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingDispatcher : IBindingDispatcher
    {
        private readonly Func<BindingInvocation, string> _dispatch;

        public CapturingDispatcher(string result)
            : this(_ => result)
        {
        }

        public CapturingDispatcher(Func<BindingInvocation, string> dispatch)
        {
            _dispatch = dispatch;
        }

        public List<BindingInvocation> Invocations { get; } = [];

        public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(invocation);
            return Task.FromResult(_dispatch(invocation));
        }
    }
}
