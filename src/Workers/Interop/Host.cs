using System.Collections.Concurrent;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Interop;

/// <summary>Exported .NET host methods called by the generated JavaScript adapter.</summary>
[SupportedOSPlatform("browser")]
internal static partial class Host
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly ConcurrentDictionary<string, Task> WaitUntilTasks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Task<string>> ManagedInvocations = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ManagedRpcTargetEntry> RpcTargets = new(StringComparer.Ordinal);
    private static readonly HostSynchronizationContext Synchronization = new();
    private static long nextManagedInvocationId;
    private static long nextWaitUntilId;
    private static long nextRpcTargetId;

    /// <summary>Runs managed continuations posted by Worker-dispatched async code.</summary>
    [JSExport]
    public static void PumpContinuations()
    {
        Synchronization.Pump();
    }

    /// <summary>Starts a fetch dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string FetchStart(string payloadJson)
    {
        return StartManagedInvocation("fetch", FetchCoreAsync(payloadJson));
    }

    /// <summary>Polls a previously started fetch dispatch.</summary>
    [JSExport]
    public static string FetchPoll(string handle)
    {
        return PollManagedInvocation(handle);
    }

    /// <summary>Polls a managed invocation previously started by one of the exported start methods.</summary>
    [JSExport]
    public static string Poll(string handle)
    {
        return PollManagedInvocation(handle);
    }

    /// <summary>Dispatches a fetch event to the user entrypoint.</summary>
    public static async Task<string> Fetch(string payloadJson)
    {
        return await FetchCoreAsync(payloadJson);
    }

    private static async Task<string> FetchCoreAsync(string payloadJson)
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(Synchronization);

        try
        {
            var payload = Deserialize(payloadJson, JsonContext.FetchInvocationPayload);
            var entrypoint = Resolve(payload.Manifest, RuntimeEntrypointKind.Fetch);
            var (response, context) = await InvokeFetchAsync(entrypoint, payload.Request, payload.Environment, payload.Context);
            return JsonSerializer.Serialize(
                ResponseEnvelope.FromResponse(
                    response,
                    RegisterWaitUntil(context),
                    context.PassThroughOnExceptionRequested),
                JsonContext.ResponseEnvelope);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static string SerializeManagedInvocationState(Task<string> task)
    {
        if (task.IsCanceled)
            throw new TaskCanceledException(task);

        if (task.IsFaulted)
            throw task.Exception?.GetBaseException() ?? new WorkersException("Managed invocation failed.");

        return JsonSerializer.Serialize(new ManagedInvocationState(true, null, task.Result), JsonContext.ManagedInvocationState);
    }

    private static async Task<string> RunWithWorkerContextAsync(Func<Task<string>> action)
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(Synchronization);

        try
        {
            return await action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static string StartManagedInvocation(string operation, Task<string> task)
    {
        if (task.IsCompleted)
            return SerializeManagedInvocationState(task);

        var handle = operation + ":" + Interlocked.Increment(ref nextManagedInvocationId).ToString(System.Globalization.CultureInfo.InvariantCulture);
        ManagedInvocations[handle] = task;
        return JsonSerializer.Serialize(new ManagedInvocationState(false, handle, null), JsonContext.ManagedInvocationState);
    }

    private static string PollManagedInvocation(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        if (!ManagedInvocations.TryGetValue(handle, out var task))
            throw new WorkersException($"Managed invocation '{handle}' is not defined.");

        if (!task.IsCompleted)
            return JsonSerializer.Serialize(new ManagedInvocationState(false, handle, null), JsonContext.ManagedInvocationState);

        ManagedInvocations.TryRemove(handle, out _);
        return SerializeManagedInvocationState(task);
    }

    private sealed class HostSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);
            _callbacks.Enqueue((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            ArgumentNullException.ThrowIfNull(d);
            d(state);
        }

        public void Pump()
        {
            while (_callbacks.TryDequeue(out var item))
                item.Callback(item.State);
        }
    }

    /// <summary>Dispatches a scheduled event to the user entrypoint.</summary>
    public static async Task<string> Scheduled(string payloadJson)
    {
        return await ScheduledCoreAsync(payloadJson);
    }

    /// <summary>Starts a scheduled dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string ScheduledStart(string payloadJson) =>
        StartManagedInvocation("scheduled", RunWithWorkerContextAsync(() => ScheduledCoreAsync(payloadJson)));

    private static async Task<string> ScheduledCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.ScheduledInvocationPayload);
        var entrypoint = Resolve(payload.Manifest, RuntimeEntrypointKind.Scheduled);
        var context = await InvokeScheduledAsync(entrypoint, payload.Event, payload.Environment, payload.Context);
        return SerializeInvocationResult(context);
    }

    /// <summary>Dispatches a queue event to the user entrypoint.</summary>
    public static async Task<string> Queue(string payloadJson)
    {
        return await QueueCoreAsync(payloadJson);
    }

    /// <summary>Starts a queue dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string QueueStart(string payloadJson) =>
        StartManagedInvocation("queue", RunWithWorkerContextAsync(() => QueueCoreAsync(payloadJson)));

    private static async Task<string> QueueCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.QueueInvocationPayload);
        var entrypoint = Resolve(payload.Manifest, RuntimeEntrypointKind.Queue);
        var (context, dispositions) = await InvokeQueueAsync(entrypoint, payload.Batch, payload.Environment, payload.Context);
        return SerializeInvocationResult(context, dispositions);
    }

    /// <summary>Dispatches an inbound email event to the user entrypoint.</summary>
    public static async Task<string> Email(string payloadJson)
    {
        return await EmailCoreAsync(payloadJson);
    }

    /// <summary>Starts an inbound email dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string EmailStart(string payloadJson) =>
        StartManagedInvocation("email", RunWithWorkerContextAsync(() => EmailCoreAsync(payloadJson)));

    private static async Task<string> EmailCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.EmailInvocationPayload);
        var entrypoint = Resolve(payload.Manifest, RuntimeEntrypointKind.Email);
        var context = await InvokeEmailAsync(entrypoint, payload.Message, payload.Environment, payload.Context);
        return SerializeInvocationResult(context);
    }

    /// <summary>Dispatches a tail event to the user entrypoint.</summary>
    public static async Task<string> Tail(string payloadJson)
    {
        return await TailCoreAsync(payloadJson);
    }

    /// <summary>Starts a tail dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string TailStart(string payloadJson) =>
        StartManagedInvocation("tail", RunWithWorkerContextAsync(() => TailCoreAsync(payloadJson)));

    private static async Task<string> TailCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.TailInvocationPayload);
        var entrypoint = Resolve(payload.Manifest, RuntimeEntrypointKind.Tail);
        var context = await InvokeTailAsync(entrypoint, payload.Event, payload.Environment, payload.Context);
        return SerializeInvocationResult(context);
    }

    /// <summary>Dispatches a Durable Object fetch event to the user class.</summary>
    public static async Task<string> DurableObjectFetch(string payloadJson)
    {
        return await DurableObjectFetchCoreAsync(payloadJson);
    }

    [JSExport]
    public static async Task<string> DurableObjectFetchNative(string payloadJson, JSObject state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return await RunWithWorkerContextAsync(() => DurableObjectFetchCoreAsync(payloadJson, state));
    }

    /// <summary>Starts a Durable Object fetch dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectFetchStart(string payloadJson) =>
        StartManagedInvocation("durableObjectFetch", RunWithWorkerContextAsync(() => DurableObjectFetchCoreAsync(payloadJson)));

    private static async Task<string> DurableObjectFetchCoreAsync(string payloadJson, JSObject? nativeState = null)
    {
        var payload = Deserialize(payloadJson, JsonContext.DurableObjectFetchInvocationPayload);
        var durableObject = ResolveDurableObject(payload.Manifest, payload.ExportName);
        var response = await InvokeDurableObjectFetchAsync(
            durableObject,
            payload.DurableObjectId,
            payload.Request,
            payload.Environment,
            nativeState);

        return JsonSerializer.Serialize(ResponseEnvelope.FromResponse(response), JsonContext.ResponseEnvelope);
    }

    /// <summary>Dispatches a Durable Object alarm event to the user class.</summary>
    public static async Task DurableObjectAlarm(string payloadJson)
    {
        await DurableObjectAlarmCoreAsync(payloadJson);
    }

    /// <summary>Starts a Durable Object alarm dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectAlarmStart(string payloadJson) =>
        StartManagedInvocation("durableObjectAlarm", RunWithWorkerContextAsync(() => DurableObjectAlarmCoreAsync(payloadJson)));

    private static async Task<string> DurableObjectAlarmCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.DurableObjectAlarmInvocationPayload);
        var durableObject = ResolveDurableObject(payload.Manifest, payload.ExportName);
        await InvokeDurableObjectAlarmAsync(
            durableObject,
            payload.DurableObjectId,
            payload.Environment,
            payload.AlarmInfo);

        return "";
    }

    /// <summary>Dispatches a Durable Object RPC call to the user class.</summary>
    public static async Task<string> DurableObjectRpc(string payloadJson)
    {
        return await DurableObjectRpcCoreAsync(payloadJson);
    }

    [JSExport]
    public static async Task<string> DurableObjectRpcNative(string payloadJson, JSObject state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return await RunWithWorkerContextAsync(() => DurableObjectRpcCoreAsync(payloadJson, state));
    }

    /// <summary>Starts a Durable Object RPC dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectRpcStart(string payloadJson) =>
        StartManagedInvocation("durableObjectRpc", RunWithWorkerContextAsync(() => DurableObjectRpcCoreAsync(payloadJson)));

    private static async Task<string> DurableObjectRpcCoreAsync(string payloadJson, JSObject? nativeState = null)
    {
        var payload = Deserialize(payloadJson, JsonContext.DurableObjectRpcInvocationPayload);
        var durableObject = ResolveDurableObject(payload.Manifest, payload.ExportName);
        var value = await InvokeDurableObjectRpcAsync(
            durableObject,
            payload.MethodName,
            payload.DurableObjectId,
            payload.Environment,
            payload.Arguments,
            nativeState);

        return JsonSerializer.Serialize(value, JsonContext.DurableObjectRpcResult);
    }

    /// <summary>Dispatches a call to a managed RPC target returned over Workers RPC.</summary>
    public static async Task<string> ManagedRpcTargetInvoke(string payloadJson)
    {
        return await ManagedRpcTargetInvokeCoreAsync(payloadJson);
    }

    /// <summary>Starts a managed RPC target call without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string ManagedRpcTargetInvokeStart(string payloadJson) =>
        StartManagedInvocation("managedRpcTargetInvoke", RunWithWorkerContextAsync(() => ManagedRpcTargetInvokeCoreAsync(payloadJson)));

    private static async Task<string> ManagedRpcTargetInvokeCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.ManagedRpcTargetInvocationPayload);
        var entry = ResolveManagedRpcTarget(payload.Handle);
        var method = ResolveManagedRpcTargetMethod(entry.Target.GetType(), payload.MethodName);
        var result = Invoke(entry.Target, method, ConvertArguments(method, payload.Arguments, payload.InvocationId));
        var value = await AwaitRpcResultAsync(method, result);
        return JsonSerializer.Serialize(value, JsonContext.DurableObjectRpcResult);
    }

    /// <summary>Creates an independent handle for a managed RPC target.</summary>
    [JSExport]
    public static string ManagedRpcTargetDup(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.ManagedRpcTargetHandlePayload);
        var entry = ResolveManagedRpcTarget(payload.Handle);
        var handle = RetainManagedRpcTarget(entry.Target, entry);
        return JsonSerializer.Serialize(new ManagedRpcTargetResult(handle), JsonContext.ManagedRpcTargetResult);
    }

    /// <summary>Releases one handle for a managed RPC target.</summary>
    public static async Task ManagedRpcTargetDispose(string payloadJson)
    {
        await ManagedRpcTargetDisposeCoreAsync(payloadJson);
    }

    /// <summary>Starts a managed RPC target release without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string ManagedRpcTargetDisposeStart(string payloadJson) =>
        StartManagedInvocation("managedRpcTargetDispose", RunWithWorkerContextAsync(() => ManagedRpcTargetDisposeCoreAsync(payloadJson)));

    private static async Task<string> ManagedRpcTargetDisposeCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.ManagedRpcTargetHandlePayload);
        await ReleaseManagedRpcTargetAsync(payload.Handle);
        return "";
    }

    /// <summary>Dispatches a hibernatable Durable Object WebSocket message event to the user class.</summary>
    public static async Task DurableObjectWebSocketMessage(string payloadJson)
    {
        await DurableObjectWebSocketMessageCoreAsync(payloadJson);
    }

    /// <summary>Starts a Durable Object WebSocket message dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectWebSocketMessageStart(string payloadJson) =>
        StartManagedInvocation("durableObjectWebSocketMessage", RunWithWorkerContextAsync(() => DurableObjectWebSocketMessageCoreAsync(payloadJson)));

    private static async Task<string> DurableObjectWebSocketMessageCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.DurableObjectWebSocketMessageInvocationPayload);
        var durableObject = ResolveDurableObject(payload.Manifest, payload.ExportName);
        await InvokeDurableObjectWebSocketMessageAsync(
            durableObject,
            payload.DurableObjectId,
            payload.Environment,
            payload.WebSocketHandle,
            payload.Message);

        return "";
    }

    /// <summary>Dispatches a hibernatable Durable Object WebSocket close event to the user class.</summary>
    public static async Task DurableObjectWebSocketClose(string payloadJson)
    {
        await DurableObjectWebSocketCloseCoreAsync(payloadJson);
    }

    /// <summary>Starts a Durable Object WebSocket close dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectWebSocketCloseStart(string payloadJson) =>
        StartManagedInvocation("durableObjectWebSocketClose", RunWithWorkerContextAsync(() => DurableObjectWebSocketCloseCoreAsync(payloadJson)));

    private static async Task<string> DurableObjectWebSocketCloseCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.DurableObjectWebSocketCloseInvocationPayload);
        var durableObject = ResolveDurableObject(payload.Manifest, payload.ExportName);
        await InvokeDurableObjectWebSocketCloseAsync(
            durableObject,
            payload.DurableObjectId,
            payload.Environment,
            payload.WebSocketHandle,
            payload.Code,
            payload.Reason,
            payload.WasClean);

        return "";
    }

    /// <summary>Dispatches a hibernatable Durable Object WebSocket error event to the user class.</summary>
    public static async Task DurableObjectWebSocketError(string payloadJson)
    {
        await DurableObjectWebSocketErrorCoreAsync(payloadJson);
    }

    /// <summary>Starts a Durable Object WebSocket error dispatch without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectWebSocketErrorStart(string payloadJson) =>
        StartManagedInvocation("durableObjectWebSocketError", RunWithWorkerContextAsync(() => DurableObjectWebSocketErrorCoreAsync(payloadJson)));

    private static async Task<string> DurableObjectWebSocketErrorCoreAsync(string payloadJson)
    {
        var payload = Deserialize(payloadJson, JsonContext.DurableObjectWebSocketErrorInvocationPayload);
        var durableObject = ResolveDurableObject(payload.Manifest, payload.ExportName);
        await InvokeDurableObjectWebSocketErrorAsync(
            durableObject,
            payload.DurableObjectId,
            payload.Environment,
            payload.WebSocketHandle,
            payload.Error);

        return "";
    }

    /// <summary>Waits for a managed background task previously scheduled with <see cref="Context.WaitUntil"/>.</summary>
    public static async Task WaitUntil(string handle)
    {
        await WaitUntilCoreAsync(handle);
    }

    /// <summary>Starts a waitUntil task without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string WaitUntilStart(string handle) =>
        StartManagedInvocation("waitUntil", RunWithWorkerContextAsync(() => WaitUntilCoreAsync(handle)));

    private static async Task<string> WaitUntilCoreAsync(string handle)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);

        if (!WaitUntilTasks.TryRemove(handle, out var task))
            throw new WorkersException($"waitUntil task '{handle}' is not defined.");

        await task;
        return "";
    }

    /// <summary>Runs a managed Durable Object State API callback previously retained by runtime code.</summary>
    public static async Task DurableObjectStateCallback(string handle)
    {
        await DurableObjectStateCallbackCoreAsync(handle);
    }

    /// <summary>Starts a Durable Object State API callback without using a JavaScript-exported Task as the promise boundary.</summary>
    [JSExport]
    public static string DurableObjectStateCallbackStart(string handle) =>
        StartManagedInvocation("durableObjectStateCallback", RunWithWorkerContextAsync(() => DurableObjectStateCallbackCoreAsync(handle)));

    private static async Task<string> DurableObjectStateCallbackCoreAsync(string handle)
    {
        await DurableObjectStateCallbackRegistry.RunAsync(handle);
        return "";
    }
}
