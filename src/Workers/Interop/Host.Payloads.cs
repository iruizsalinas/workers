using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    private sealed class FetchInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required RequestEnvelope Request { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public ContextEnvelope? Context { get; init; }
    }

    private sealed class ScheduledInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required ScheduledEventEnvelope Event { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public ContextEnvelope? Context { get; init; }
    }

    private sealed class QueueInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required QueueBatchEnvelope Batch { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public ContextEnvelope? Context { get; init; }
    }

    private sealed class EmailInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required ForwardableEmailMessageEnvelope Message { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public ContextEnvelope? Context { get; init; }
    }

    private sealed class TailInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required TailEvent Event { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public ContextEnvelope? Context { get; init; }
    }

    private sealed class DurableObjectFetchInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required string ExportName { get; init; }

        public required string DurableObjectId { get; init; }

        public required RequestEnvelope Request { get; init; }

        public EnvEnvelope? Environment { get; init; }
    }

    private sealed class DurableObjectAlarmInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required string ExportName { get; init; }

        public required string DurableObjectId { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public AlarmInfo? AlarmInfo { get; init; }
    }

    private sealed class DurableObjectRpcInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required string ExportName { get; init; }

        public required string MethodName { get; init; }

        public required string DurableObjectId { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public IReadOnlyList<JsonElement> Arguments { get; init; } = [];
    }

    private sealed class ManagedRpcTargetInvocationPayload
    {
        public required string InvocationId { get; init; }

        public required string Handle { get; init; }

        public required string MethodName { get; init; }

        public IReadOnlyList<JsonElement> Arguments { get; init; } = [];
    }

    private sealed class ManagedRpcTargetHandlePayload
    {
        public required string Handle { get; init; }
    }

    private sealed class DurableObjectWebSocketMessageInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required string ExportName { get; init; }

        public required string DurableObjectId { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public required string WebSocketHandle { get; init; }

        public required WebSocketMessageEnvelope Message { get; init; }
    }

    private sealed class DurableObjectWebSocketCloseInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required string ExportName { get; init; }

        public required string DurableObjectId { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public required string WebSocketHandle { get; init; }

        public required ushort Code { get; init; }

        public string Reason { get; init; } = "";

        public required bool WasClean { get; init; }
    }

    private sealed class DurableObjectWebSocketErrorInvocationPayload
    {
        public required RuntimeBuildManifest Manifest { get; init; }

        public required string ExportName { get; init; }

        public required string DurableObjectId { get; init; }

        public EnvEnvelope? Environment { get; init; }

        public required string WebSocketHandle { get; init; }

        public required WebSocketError Error { get; init; }
    }

    private sealed class WebSocketMessageEnvelope
    {
        public string? Text { get; init; }

        public string? BodyBase64 { get; init; }

        public WebSocketMessage ToMessage() =>
            new(Text, BodyBase64 is null ? [] : Convert.FromBase64String(BodyBase64));
    }

    private sealed record DurableObjectRpcResult(JsonElement? Value, string? RpcTargetHandle);

    private sealed record ManagedRpcTargetResult(string Handle);

    private sealed class ManagedRpcTargetEntry(RpcTarget target)
    {
        private int _refCount;

        public RpcTarget Target { get; } = target;

        public void Retain() => Interlocked.Increment(ref _refCount);

        public bool Release() => Interlocked.Decrement(ref _refCount) == 0;
    }

    private sealed record InvocationResult(
        IReadOnlyList<string> WaitUntilHandles,
        bool PassThroughOnException,
        IReadOnlyList<QueueMessageDisposition> QueueDispositions);

    private sealed record ManagedInvocationState(
        bool Completed,
        string? Handle,
        string? Result);

    private sealed class RuntimeBuildManifest
    {
        public required string EntryAssembly { get; init; }

        public required IReadOnlyList<RuntimeEntrypoint> Entrypoints { get; init; }

        public IReadOnlyList<RuntimeDurableObject> DurableObjects { get; init; } = [];
    }

    private sealed class RuntimeEntrypoint
    {
        public required RuntimeEntrypointKind Kind { get; init; }

        public required string ContainingType { get; init; }

        public required string MethodName { get; init; }
    }

    private sealed class RuntimeDurableObject
    {
        public required string ExportName { get; init; }

        public required string ContainingType { get; init; }

        public string? FetchMethodName { get; init; }

        public string? AlarmMethodName { get; init; }

        public string? WebSocketMessageMethodName { get; init; }

        public string? WebSocketCloseMethodName { get; init; }

        public string? WebSocketErrorMethodName { get; init; }

        public IReadOnlyList<RuntimeDurableObjectRpcMethod> RpcMethods { get; init; } = [];
    }

    private sealed class RuntimeDurableObjectRpcMethod
    {
        public required string Name { get; init; }

        public required string MethodName { get; init; }
    }

    private enum RuntimeEntrypointKind
    {
        Fetch,
        Scheduled,
        Queue,
        Email,
        Tail
    }

    private sealed class ScheduledEventEnvelope
    {
        public required string Cron { get; init; }

        public string? Type { get; init; }

        public required DateTimeOffset ScheduledTime { get; init; }
    }

    private sealed class QueueBatchEnvelope
    {
        public string? Queue { get; init; }

        public required IReadOnlyList<QueueMessageEnvelope> Messages { get; init; }
    }

    private sealed class QueueMessageEnvelope
    {
        public required string Id { get; init; }

        public required DateTimeOffset Timestamp { get; init; }

        public int Attempts { get; init; } = 1;

        public JsonElement Body { get; init; }

        public string? BodyBase64 { get; init; }
    }
}
