using System.Text.Json;

namespace Workers;

/// <summary>Synchronous key-value storage attached to a SQLite-backed Durable Object instance.</summary>
public sealed class DurableObjectKvStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal DurableObjectKvStorage(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Gets and deserializes a JSON value by key from synchronous key-value storage.</summary>
    public async Task<T?> GetJsonAsync<T>(
        string key,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "durable.storage.kv.get",
            new { key },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<StorageValueEnvelope>(result, JsonOptions)
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

        return DispatchAsync(
            "durable.storage.kv.put",
            new { key, value = JsonSerializer.SerializeToElement(value, jsonOptions) },
            cancellationToken);
    }

    /// <summary>Deletes a synchronous key-value storage entry and returns whether a value was deleted.</summary>
    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "durable.storage.kv.delete",
            new { key },
            cancellationToken);

        return JsonSerializer.Deserialize<DeleteResultEnvelope>(result, JsonOptions)?.Deleted ?? false;
    }

    /// <summary>Lists synchronous key-value storage entries and deserializes each value as JSON.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectKvListOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.kv.list",
            new { options },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<StorageValuesEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object synchronous KV storage returned an empty list result.");

        return DeserializeValues<T>(envelope.Values, jsonOptions);
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            DurableObjectStorage.BindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

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

    private sealed record StorageValueEnvelope(JsonElement Value);

    private sealed record StorageValuesEnvelope(IReadOnlyDictionary<string, JsonElement> Values);

    private sealed record DeleteResultEnvelope(bool Deleted);
}
