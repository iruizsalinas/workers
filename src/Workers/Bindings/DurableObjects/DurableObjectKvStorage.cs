using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>Synchronous key-value storage attached to a SQLite-backed Durable Object instance.</summary>
public sealed class DurableObjectKvStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableObjectKvStorageJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;
    private readonly JSObject? _nativeState;

    internal DurableObjectKvStorage(string invocationId, IBindingDispatcher dispatcher, JSObject? nativeState = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _nativeState = nativeState;
    }

    /// <summary>Gets and deserializes a JSON value by key from synchronous key-value storage.</summary>
    public async Task<T?> GetJsonAsync<T>(
        string key,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectKvStorage.Get(_nativeState, key)
            : await DispatchAsync(
                "durable.storage.kv.get",
                JsonSerializer.Serialize(new DurableObjectKvKeyPayload { Key = key }, JsonContext.DurableObjectKvKeyPayload),
                cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableObjectKvStorageValueEnvelope)
            ?? throw new WorkersException("Durable Object synchronous KV storage returned an empty get result.");

        return DeserializeValue<T>(envelope.Value, jsonOptions);
    }

    /// <summary>Serializes and stores a JSON value by key in synchronous key-value storage.</summary>
    public Task PutJsonAsync<T>(
        string key,
        T value,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        jsonOptions ??= JsonOptions;

        if (_nativeState is not null && OperatingSystem.IsBrowser())
        {
            var valueJson = JsonSerializer.Serialize(value, jsonOptions);
            NativeDurableObjectKvStorage.Put(_nativeState, key, valueJson);
            return Task.CompletedTask;
        }

        return DispatchAsync(
            "durable.storage.kv.put",
            JsonSerializer.Serialize(
                new DurableObjectKvPutPayload
                {
                    Key = key,
                    Value = JsonSerializer.SerializeToElement(value, jsonOptions)
                },
                JsonContext.DurableObjectKvPutPayload),
            cancellationToken);
    }

    /// <summary>Deletes a synchronous key-value storage entry and returns whether a value was deleted.</summary>
    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectKvStorage.Delete(_nativeState, key)
            : await DispatchAsync(
                "durable.storage.kv.delete",
                JsonSerializer.Serialize(new DurableObjectKvKeyPayload { Key = key }, JsonContext.DurableObjectKvKeyPayload),
                cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.DurableObjectKvDeleteResultEnvelope)?.Deleted ?? false;
    }

    /// <summary>Lists synchronous key-value storage entries and deserializes each value as JSON.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectKvListOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(
            new DurableObjectKvListPayload { Options = options },
            JsonContext.DurableObjectKvListPayload);
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectKvStorage.List(_nativeState, payloadJson)
            : await DispatchAsync(
                "durable.storage.kv.list",
                payloadJson,
                cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.DurableObjectKvStorageValuesEnvelope)
            ?? throw new WorkersException("Durable Object synchronous KV storage returned an empty list result.");

        return DeserializeValues<T>(envelope.Values, jsonOptions);
    }

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private static T? DeserializeValue<T>(JsonElement value, JsonSerializerOptions? options) =>
        value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? default
            : value.Deserialize<T>(options ?? JsonOptions);

    private static IReadOnlyDictionary<string, T?> DeserializeValues<T>(
        IReadOnlyDictionary<string, JsonElement> values,
        JsonSerializerOptions? options)
    {
        var result = new Dictionary<string, T?>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
            result[key] = DeserializeValue<T>(value, options);

        return result;
    }
}

[SupportedOSPlatform("browser")]
internal static partial class NativeDurableObjectKvStorage
{
    [JSImport("cloudflareWorkers.durableStorage.kvGet", "dotnet.js")]
    internal static partial string Get(JSObject state, string key);

    [JSImport("cloudflareWorkers.durableStorage.kvPut", "dotnet.js")]
    internal static partial void Put(JSObject state, string key, string valueJson);

    [JSImport("cloudflareWorkers.durableStorage.kvDelete", "dotnet.js")]
    internal static partial string Delete(JSObject state, string key);

    [JSImport("cloudflareWorkers.durableStorage.kvList", "dotnet.js")]
    internal static partial string List(JSObject state, string optionsJson);
}

internal sealed class DurableObjectKvKeyPayload
{
    public string Key { get; set; } = "";
}

internal sealed class DurableObjectKvPutPayload
{
    public string Key { get; set; } = "";

    public JsonElement Value { get; set; }
}

internal sealed class DurableObjectKvListPayload
{
    public DurableObjectKvListOptions? Options { get; set; }
}

internal sealed class DurableObjectKvStorageValueEnvelope
{
    public JsonElement Value { get; set; }
}

internal sealed class DurableObjectKvStorageValuesEnvelope
{
    public IReadOnlyDictionary<string, JsonElement> Values { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}

internal sealed class DurableObjectKvDeleteResultEnvelope
{
    public bool Deleted { get; set; }
}

[JsonSerializable(typeof(DurableObjectKvKeyPayload))]
[JsonSerializable(typeof(DurableObjectKvPutPayload))]
[JsonSerializable(typeof(DurableObjectKvListPayload))]
[JsonSerializable(typeof(DurableObjectKvListOptions))]
[JsonSerializable(typeof(DurableObjectKvStorageValueEnvelope))]
[JsonSerializable(typeof(DurableObjectKvStorageValuesEnvelope))]
[JsonSerializable(typeof(DurableObjectKvDeleteResultEnvelope))]
internal sealed partial class DurableObjectKvStorageJsonContext : JsonSerializerContext
{
}
