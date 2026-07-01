using System.Text.Json;
using Workers.Interop;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    private static Env EnvironmentWithInvocation(string invocationId)
    {
        var json = JsonSerializer.Serialize(
            new
            {
                invocationId,
                bindings = new Dictionary<string, object?>()
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return JsonSerializer.Deserialize<EnvEnvelope>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ToEnvironment();
    }

    private sealed class CapturingDispatcher : IBindingDispatcher
    {
        private readonly Queue<string> _results;
        private string _lastResult;

        public CapturingDispatcher(params string[] results)
        {
            if (results.Length == 0)
                throw new ArgumentException("At least one result is required.", nameof(results));

            _results = new Queue<string>(results);
            _lastResult = results[^1];
        }

        public List<BindingInvocation> Invocations { get; } = [];

        public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(invocation);
            if (_results.Count > 0)
                _lastResult = _results.Dequeue();

            return Task.FromResult(_lastResult);
        }
    }

    private sealed class UserRow
    {
        public int Id { get; init; }

        public string Name { get; init; } = "";
    }

    private sealed class KvMetadata
    {
        public int Version { get; init; }

        public string Kind { get; init; } = "";
    }

    private sealed class ResponseCf
    {
        public string Colo { get; init; } = "";

        public string CacheStatus { get; init; } = "";
    }

    private sealed record AiPrompt(IReadOnlyList<string> Messages);

    private sealed class AiTextOutput
    {
        public string Response { get; init; } = "";

        public AiUsage Usage { get; init; } = new();
    }

    private sealed class AiUsage
    {
        public int TotalTokens { get; init; }
    }

    private sealed class RoomStatus
    {
        public bool Ok { get; init; }

        public int Count { get; init; }
    }

    private sealed class TouchOptions
    {
        public int Ttl { get; init; }
    }

    private interface IRoomRpc
    {
        Task<RoomStatus?> Status(int roomId, string mode);

        Task Touch(TouchOptions options);

        Task<RpcStub> NewCounter(int initialValue);
    }

    private interface ICounterRpc
    {
        ValueTask<int> Add(int amount);

        Task<RpcStub> Child(RpcStub parent);
    }
}
