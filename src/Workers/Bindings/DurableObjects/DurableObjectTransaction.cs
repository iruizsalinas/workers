using System.Text.Json;

namespace Workers;

/// <summary>Storage operations available inside a Durable Object storage transaction.</summary>
public sealed class DurableObjectTransaction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            new { handle = _handle, key, options },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<StorageValueEnvelope>(result, JsonOptions)
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
            new { handle = _handle, keys = keyArray, options },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<StorageValuesEnvelope>(result, JsonOptions)
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
            new { handle = _handle, key, value = JsonSerializer.SerializeToElement(value, jsonOptions), options },
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
            new { handle = _handle, values = serialized, options },
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
            new { handle = _handle, key, options },
            cancellationToken);

        return JsonSerializer.Deserialize<DeleteResultEnvelope>(result, JsonOptions)?.Deleted ?? false;
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
            new { handle = _handle, keys = keyArray, options },
            cancellationToken);

        return JsonSerializer.Deserialize<DeleteManyResultEnvelope>(result, JsonOptions)?.DeletedCount ?? 0;
    }

    /// <summary>Lists storage entries inside the transaction and deserializes each value as JSON.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectStorageListOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.transaction.list",
            new { handle = _handle, options },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<StorageValuesEnvelope>(result, JsonOptions)
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
            new { handle = _handle },
            cancellationToken);
        IsCompleted = true;
    }

    internal async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (IsCompleted)
            return;

        await DispatchAsync(
            "durable.storage.transaction.commit",
            new { handle = _handle },
            cancellationToken);
        IsCompleted = true;
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

    private sealed record StorageValueEnvelope(JsonElement Value);

    private sealed record StorageValuesEnvelope(IReadOnlyDictionary<string, JsonElement> Values);

    private sealed record DeleteResultEnvelope(bool Deleted);

    private sealed record DeleteManyResultEnvelope(int DeletedCount);
}
