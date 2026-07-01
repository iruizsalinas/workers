using System.Text.Json;

namespace Workers;

/// <summary>Storage operations available inside a Durable Object storage transaction.</summary>
public sealed class DurableObjectTransaction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableObjectStorageJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly string _handle;
    private readonly IBindingDispatcher _dispatcher;

    internal DurableObjectTransaction(
        string invocationId,
        string handle,
        IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(handle);
        _invocationId = invocationId;
        _handle = handle;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>True after the transaction has been committed or rolled back.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Gets and deserializes a JSON value by key inside the transaction.</summary>
    public async Task<T?> GetJsonAsync<T>(
        string key,
        DurableObjectStorageReadOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "durable.storage.transaction.get",
            JsonSerializer.Serialize(
                new DurableStorageTransactionKeyReadPayload
                {
                    Handle = _handle,
                    Key = key,
                    Options = options
                },
                JsonContext.DurableStorageTransactionKeyReadPayload),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.StorageValueEnvelope)
            ?? throw new WorkersException("Durable Object transaction returned an empty get result.");

        return DeserializeValue<T>(envelope.Value, jsonOptions);
    }

    /// <summary>Gets and deserializes multiple JSON values by key inside the transaction.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> GetJsonAsync<T>(
        IEnumerable<string> keys,
        DurableObjectStorageReadOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var keyArray = Keys(keys);

        var result = await DispatchAsync(
            "durable.storage.transaction.getMany",
            JsonSerializer.Serialize(
                new DurableStorageTransactionKeysReadPayload
                {
                    Handle = _handle,
                    Keys = keyArray,
                    Options = options
                },
                JsonContext.DurableStorageTransactionKeysReadPayload),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.StorageValuesEnvelope)
            ?? throw new WorkersException("Durable Object transaction returned an empty multi-get result.");

        return DeserializeValues<T>(envelope.Values, jsonOptions);
    }

    /// <summary>Serializes and stores a JSON value by key inside the transaction.</summary>
    public Task PutJsonAsync<T>(
        string key,
        T value,
        DurableObjectStorageWriteOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        jsonOptions ??= JsonOptions;

        return DispatchAsync(
            "durable.storage.transaction.put",
            JsonSerializer.Serialize(
                new DurableStorageTransactionPutPayload
                {
                    Handle = _handle,
                    Key = key,
                    Value = JsonSerializer.SerializeToElement(value, jsonOptions),
                    Options = options
                },
                JsonContext.DurableStorageTransactionPutPayload),
            cancellationToken);
    }

    /// <summary>Serializes and stores multiple JSON values by key inside the transaction.</summary>
    public Task PutJsonAsync<T>(
        IReadOnlyDictionary<string, T> values,
        DurableObjectStorageWriteOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        jsonOptions ??= JsonOptions;

        var serialized = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            serialized[key] = JsonSerializer.SerializeToElement(value, jsonOptions);
        }

        return DispatchAsync(
            "durable.storage.transaction.putMany",
            JsonSerializer.Serialize(
                new DurableStorageTransactionPutManyPayload
                {
                    Handle = _handle,
                    Values = serialized,
                    Options = options
                },
                JsonContext.DurableStorageTransactionPutManyPayload),
            cancellationToken);
    }

    /// <summary>Deletes a value by key inside the transaction and returns whether a value was deleted.</summary>
    public async Task<bool> DeleteAsync(
        string key,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "durable.storage.transaction.delete",
            JsonSerializer.Serialize(
                new DurableStorageTransactionKeyWritePayload
                {
                    Handle = _handle,
                    Key = key,
                    Options = options
                },
                JsonContext.DurableStorageTransactionKeyWritePayload),
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.DeleteResultEnvelope)?.Deleted ?? false;
    }

    /// <summary>Deletes multiple values by key inside the transaction and returns the number of deleted values.</summary>
    public async Task<int> DeleteAsync(
        IEnumerable<string> keys,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var keyArray = Keys(keys);

        var result = await DispatchAsync(
            "durable.storage.transaction.deleteMany",
            JsonSerializer.Serialize(
                new DurableStorageTransactionKeysWritePayload
                {
                    Handle = _handle,
                    Keys = keyArray,
                    Options = options
                },
                JsonContext.DurableStorageTransactionKeysWritePayload),
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.DeleteManyResultEnvelope)?.DeletedCount ?? 0;
    }

    /// <summary>Lists storage entries inside the transaction and deserializes each value as JSON.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectStorageListOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.transaction.list",
            JsonSerializer.Serialize(
                new DurableStorageTransactionListPayload
                {
                    Handle = _handle,
                    Options = options
                },
                JsonContext.DurableStorageTransactionListPayload),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.StorageValuesEnvelope)
            ?? throw new WorkersException("Durable Object transaction returned an empty list result.");

        return DeserializeValues<T>(envelope.Values, jsonOptions);
    }

    /// <summary>Rolls back the transaction instead of committing it.</summary>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return;

        await DispatchAsync(
            "durable.storage.transaction.rollback",
            JsonSerializer.Serialize(new DurableStorageTransactionHandlePayload { Handle = _handle }, JsonContext.DurableStorageTransactionHandlePayload),
            cancellationToken);
        IsCompleted = true;
    }

    internal async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return;

        await DispatchAsync(
            "durable.storage.transaction.commit",
            JsonSerializer.Serialize(new DurableStorageTransactionHandlePayload { Handle = _handle }, JsonContext.DurableStorageTransactionHandlePayload),
            cancellationToken);
        IsCompleted = true;
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

    private static string[] Keys(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var keyArray = keys.ToArray();
        if (keyArray.Length == 0)
            throw new ArgumentException("At least one key is required.", nameof(keys));

        foreach (var key in keyArray)
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return keyArray;
    }

}

internal sealed class DurableStorageTransactionHandlePayload
{
    public string Handle { get; set; } = "";
}

internal sealed class DurableStorageTransactionKeyReadPayload
{
    public string Handle { get; set; } = "";

    public string Key { get; set; } = "";

    public DurableObjectStorageReadOptions? Options { get; set; }
}

internal sealed class DurableStorageTransactionKeysReadPayload
{
    public string Handle { get; set; } = "";

    public IReadOnlyList<string> Keys { get; set; } = [];

    public DurableObjectStorageReadOptions? Options { get; set; }
}

internal sealed class DurableStorageTransactionKeyWritePayload
{
    public string Handle { get; set; } = "";

    public string Key { get; set; } = "";

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageTransactionKeysWritePayload
{
    public string Handle { get; set; } = "";

    public IReadOnlyList<string> Keys { get; set; } = [];

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageTransactionPutPayload
{
    public string Handle { get; set; } = "";

    public string Key { get; set; } = "";

    public JsonElement Value { get; set; }

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageTransactionPutManyPayload
{
    public string Handle { get; set; } = "";

    public IReadOnlyDictionary<string, JsonElement> Values { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageTransactionListPayload
{
    public string Handle { get; set; } = "";

    public DurableObjectStorageListOptions? Options { get; set; }
}
