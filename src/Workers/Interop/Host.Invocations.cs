using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    private static async Task<(Response Response, Context Context)> InvokeFetchAsync(
        MethodInfo method,
        RequestEnvelope requestEnvelope,
        EnvEnvelope? environmentEnvelope,
        ContextEnvelope? contextEnvelope)
    {
        var context = ToExecutionContext(contextEnvelope);
        var environment = ToEnvironment(environmentEnvelope);
        var result = Invoke(
            method,
            requestEnvelope.ToRequest(environmentEnvelope?.InvocationId, BindingDispatcher.Current),
            environment,
            context);

        var response = await AwaitResponseAsync(method, result);
        return (response, context);
    }

    private static async Task<Context> InvokeScheduledAsync(
        MethodInfo method,
        ScheduledEventEnvelope envelope,
        EnvEnvelope? environmentEnvelope,
        ContextEnvelope? contextEnvelope)
    {
        var context = ToExecutionContext(contextEnvelope);
        var cron = string.IsNullOrWhiteSpace(envelope.Cron) ? "manual" : envelope.Cron;
        var scheduledEvent = new ScheduledEvent(cron, envelope.ScheduledTime, envelope.Type ?? "scheduled");
        var result = Invoke(
            method,
            scheduledEvent,
            ToEnvironment(environmentEnvelope),
            context);

        await AwaitVoidLikeAsync(method, result);
        return context;
    }

    private static async Task<(Context Context, IReadOnlyList<QueueMessageDisposition> Dispositions)> InvokeQueueAsync(
        MethodInfo method,
        QueueBatchEnvelope envelope,
        EnvEnvelope? environmentEnvelope,
        ContextEnvelope? contextEnvelope)
    {
        var context = ToExecutionContext(contextEnvelope);
        var batch = CreateQueueBatch(method, envelope);
        var result = Invoke(
            method,
            batch,
            ToEnvironment(environmentEnvelope),
            context);

        await AwaitVoidLikeAsync(method, result);
        return (context, QueueDispositions(batch));
    }

    private static async Task<Context> InvokeEmailAsync(
        MethodInfo method,
        ForwardableEmailMessageEnvelope envelope,
        EnvEnvelope? environmentEnvelope,
        ContextEnvelope? contextEnvelope)
    {
        var context = ToExecutionContext(contextEnvelope);
        var result = Invoke(
            method,
            envelope.ToMessage(),
            ToEnvironment(environmentEnvelope),
            context);

        await AwaitVoidLikeAsync(method, result);
        return context;
    }

    private static async Task<Context> InvokeTailAsync(
        MethodInfo method,
        TailEvent tailEvent,
        EnvEnvelope? environmentEnvelope,
        ContextEnvelope? contextEnvelope)
    {
        var context = ToExecutionContext(contextEnvelope);
        var result = Invoke(
            method,
            tailEvent,
            ToEnvironment(environmentEnvelope),
            context);

        await AwaitVoidLikeAsync(method, result);
        return context;
    }

    private static async Task<Response> InvokeDurableObjectFetchAsync(
        RuntimeDurableObject durableObject,
        string durableObjectId,
        RequestEnvelope requestEnvelope,
        EnvEnvelope? environmentEnvelope)
    {
        var method = ResolveDurableObjectMethod(durableObject, durableObject.FetchMethodName, "fetch");
        var instance = CreateDurableObjectInstance(durableObject, durableObjectId, environmentEnvelope);
        var result = Invoke(
            instance,
            method,
            requestEnvelope.ToRequest(environmentEnvelope?.InvocationId, BindingDispatcher.Current));
        return await AwaitResponseAsync(method, result);
    }

    private static async Task InvokeDurableObjectAlarmAsync(
        RuntimeDurableObject durableObject,
        string durableObjectId,
        EnvEnvelope? environmentEnvelope,
        AlarmInfo? alarmInfo)
    {
        var method = ResolveDurableObjectMethod(durableObject, durableObject.AlarmMethodName, "alarm");
        var instance = CreateDurableObjectInstance(durableObject, durableObjectId, environmentEnvelope);
        var parameters = method.GetParameters().Length == 0
            ? []
            : new object[] { alarmInfo ?? new AlarmInfo(0, false) };
        var result = Invoke(instance, method, parameters);
        await AwaitVoidLikeAsync(method, result);
    }

    private static async Task<DurableObjectRpcResult> InvokeDurableObjectRpcAsync(
        RuntimeDurableObject durableObject,
        string methodName,
        string durableObjectId,
        EnvEnvelope? environmentEnvelope,
        IReadOnlyList<JsonElement> arguments)
    {
        var method = ResolveDurableObjectRpcMethod(durableObject, methodName);
        var instance = CreateDurableObjectInstance(durableObject, durableObjectId, environmentEnvelope);
        var invocationId = environmentEnvelope?.InvocationId
            ?? throw new WorkersException("Durable Object RPC invocation is missing an invocation id.");
        var result = Invoke(instance, method, ConvertArguments(method, arguments, invocationId));
        return await AwaitRpcResultAsync(method, result);
    }

    private static async Task InvokeDurableObjectWebSocketMessageAsync(
        RuntimeDurableObject durableObject,
        string durableObjectId,
        EnvEnvelope? environmentEnvelope,
        string webSocketHandle,
        WebSocketMessageEnvelope message)
    {
        var method = ResolveDurableObjectMethod(durableObject, durableObject.WebSocketMessageMethodName, "webSocketMessage");
        var instance = CreateDurableObjectInstance(durableObject, durableObjectId, environmentEnvelope);
        var result = Invoke(
            instance,
            method,
            CreateWebSocket(environmentEnvelope, webSocketHandle),
            message.ToMessage());
        await AwaitVoidLikeAsync(method, result);
    }

    private static async Task InvokeDurableObjectWebSocketCloseAsync(
        RuntimeDurableObject durableObject,
        string durableObjectId,
        EnvEnvelope? environmentEnvelope,
        string webSocketHandle,
        ushort code,
        string reason,
        bool wasClean)
    {
        var method = ResolveDurableObjectMethod(durableObject, durableObject.WebSocketCloseMethodName, "webSocketClose");
        var instance = CreateDurableObjectInstance(durableObject, durableObjectId, environmentEnvelope);
        var result = Invoke(
            instance,
            method,
            CreateWebSocket(environmentEnvelope, webSocketHandle),
            code,
            reason,
            wasClean);
        await AwaitVoidLikeAsync(method, result);
    }

    private static async Task InvokeDurableObjectWebSocketErrorAsync(
        RuntimeDurableObject durableObject,
        string durableObjectId,
        EnvEnvelope? environmentEnvelope,
        string webSocketHandle,
        WebSocketError error)
    {
        var method = ResolveDurableObjectMethod(durableObject, durableObject.WebSocketErrorMethodName, "webSocketError");
        var instance = CreateDurableObjectInstance(durableObject, durableObjectId, environmentEnvelope);
        var result = Invoke(
            instance,
            method,
            CreateWebSocket(environmentEnvelope, webSocketHandle),
            error);
        await AwaitVoidLikeAsync(method, result);
    }

    private static object CreateQueueBatch(MethodInfo method, QueueBatchEnvelope envelope)
    {
        var batchParameterType = method.GetParameters()[0].ParameterType;
        var bodyType = batchParameterType.GetGenericArguments()[0];
        var createMethod = typeof(Host).GetMethod(
            nameof(CreateQueueBatchCore),
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new WorkersException("Queue batch factory is missing.");

        return createMethod.MakeGenericMethod(bodyType).Invoke(null, [envelope])
            ?? throw new WorkersException($"Queue batch '{batchParameterType.FullName}' could not be created.");
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Queue message bodies are intentionally deserialized dynamically into the user's QueueMessageBatch<T> body type.")]
    private static QueueMessageBatch<T> CreateQueueBatchCore<T>(QueueBatchEnvelope envelope)
    {
        var messages = new List<QueueMessage<T>>(envelope.Messages.Count);
        foreach (var message in envelope.Messages)
        {
            var body = DeserializeQueueBody<T>(message);

            messages.Add(new QueueMessage<T>(
                message.Id,
                body,
                message.Timestamp,
                Math.Max(1, message.Attempts),
                messages.Count));
        }

        return new QueueMessageBatch<T>(envelope.Queue ?? "", messages);
    }

    private static T DeserializeQueueBody<T>(QueueMessageEnvelope message)
    {
        if (message.BodyBase64 is not null)
        {
            var bytes = Convert.FromBase64String(message.BodyBase64);
            if (typeof(T) == typeof(byte[]))
                return (T)(object)bytes;

            if (typeof(T) == typeof(ReadOnlyMemory<byte>))
                return (T)(object)new ReadOnlyMemory<byte>(bytes);

            if (typeof(T) == typeof(Memory<byte>))
                return (T)(object)new Memory<byte>(bytes);

            throw new WorkersException($"Queue message '{message.Id}' is binary and must be consumed as byte[], Memory<byte>, or ReadOnlyMemory<byte>.");
        }

        return message.Body.Deserialize<T>(JsonOptions)
            ?? throw new WorkersException($"Queue message '{message.Id}' body could not be deserialized as '{typeof(T).FullName}'.");
    }

    private static IReadOnlyList<QueueMessageDisposition> QueueDispositions(object batch)
    {
        if (batch is not IQueueMessageBatch queueBatch)
            throw new WorkersException($"Queue batch '{batch.GetType().FullName}' is missing disposition metadata.");

        return queueBatch.Dispositions();
    }
}
