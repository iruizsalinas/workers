using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Workers.Interop;

[SupportedOSPlatform("browser")]
internal static partial class Host
{
    private static readonly HostJsonContext JsonContext = new(CreateJsonOptions());

    private static T Deserialize<T>(string json, JsonTypeInfo<T> typeInfo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new WorkersException($"Unable to deserialize invocation payload as '{typeof(T).Name}'.");
    }

    private static string SerializeInvocationResult(
        Context context,
        IReadOnlyList<QueueMessageDisposition>? queueDispositions = null) =>
        JsonSerializer.Serialize(
            new InvocationResult(
                RegisterWaitUntil(context),
                context.PassThroughOnExceptionRequested,
                queueDispositions ?? []),
            JsonContext.InvocationResult);

    private static IReadOnlyList<string> RegisterWaitUntil(Context context)
    {
        if (context.PendingTasks.Count == 0)
            return [];

        var handles = new string[context.PendingTasks.Count];
        for (var index = 0; index < context.PendingTasks.Count; index++)
        {
            var handle = "wait:" + Interlocked.Increment(ref nextWaitUntilId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            WaitUntilTasks[handle] = context.PendingTasks[index];
            handles[index] = handle;
        }

        return handles;
    }

    private static string Format(MethodInfo method) =>
        $"{method.DeclaringType?.FullName ?? "<unknown>"}.{method.Name}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }

    [JsonSerializable(typeof(FetchInvocationPayload))]
    [JsonSerializable(typeof(ScheduledInvocationPayload))]
    [JsonSerializable(typeof(QueueInvocationPayload))]
    [JsonSerializable(typeof(EmailInvocationPayload))]
    [JsonSerializable(typeof(TailInvocationPayload))]
    [JsonSerializable(typeof(DurableObjectFetchInvocationPayload))]
    [JsonSerializable(typeof(DurableObjectAlarmInvocationPayload))]
    [JsonSerializable(typeof(DurableObjectRpcInvocationPayload))]
    [JsonSerializable(typeof(ManagedRpcTargetInvocationPayload))]
    [JsonSerializable(typeof(ManagedRpcTargetHandlePayload))]
    [JsonSerializable(typeof(DurableObjectWebSocketMessageInvocationPayload))]
    [JsonSerializable(typeof(DurableObjectWebSocketCloseInvocationPayload))]
    [JsonSerializable(typeof(DurableObjectWebSocketErrorInvocationPayload))]
    [JsonSerializable(typeof(ResponseEnvelope))]
    [JsonSerializable(typeof(ManagedInvocationState))]
    [JsonSerializable(typeof(InvocationResult))]
    [JsonSerializable(typeof(DurableObjectRpcResult))]
    [JsonSerializable(typeof(ManagedRpcTargetResult))]
    private sealed partial class HostJsonContext : JsonSerializerContext
    {
    }
}
