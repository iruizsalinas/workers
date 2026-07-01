using System.Text.Json;

namespace Workers;

/// <summary>Persistent storage attached to a Durable Object instance.</summary>
public sealed class DurableObjectStorage
{
    internal const string BindingName = "$durableObjectState";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;

    internal DurableObjectStorage(string invocationId, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        Kv = new DurableObjectKvStorage(invocationId, dispatcher);
        Sql = new DurableObjectSqlStorage(invocationId, dispatcher);
    }

    /// <summary>Synchronous key-value storage for SQLite-backed Durable Objects.</summary>
    public DurableObjectKvStorage Kv { get; }

    /// <summary>SQLite storage for SQLite-backed Durable Objects.</summary>
    public DurableObjectSqlStorage Sql { get; }

    /// <summary>Gets a point-in-time recovery bookmark for the current storage state.</summary>
    public async Task<string> GetCurrentBookmarkAsync(CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.getCurrentBookmark",
            new { },
            cancellationToken);

        return JsonSerializer.Deserialize<BookmarkEnvelope>(result, JsonOptions)?.Bookmark
            ?? throw new WorkersException("Durable Object storage returned an empty bookmark result.");
    }

    /// <summary>Gets a point-in-time recovery bookmark for an approximate timestamp.</summary>
    public Task<string> GetBookmarkForTimeAsync(
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default) =>
        GetBookmarkForUnixTimeMillisecondsAsync(timestamp.ToUnixTimeMilliseconds(), cancellationToken);

    /// <summary>Gets a point-in-time recovery bookmark for an approximate Unix epoch millisecond timestamp.</summary>
    public async Task<string> GetBookmarkForUnixTimeMillisecondsAsync(
        long timestamp,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.getBookmarkForTime",
            new { timestamp },
            cancellationToken);

        return JsonSerializer.Deserialize<BookmarkEnvelope>(result, JsonOptions)?.Bookmark
            ?? throw new WorkersException("Durable Object storage returned an empty bookmark result.");
    }

    /// <summary>Schedules storage restore to the given bookmark on the Durable Object's next session.</summary>
    public async Task<string> OnNextSessionRestoreBookmarkAsync(
        string bookmark,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookmark);

        var result = await DispatchAsync(
            "durable.storage.onNextSessionRestoreBookmark",
            new { bookmark },
            cancellationToken);

        return JsonSerializer.Deserialize<BookmarkEnvelope>(result, JsonOptions)?.Bookmark
            ?? throw new WorkersException("Durable Object storage returned an empty bookmark result.");
    }

    /// <summary>Gets and deserializes a JSON value by key.</summary>
    public async Task<T?> GetJsonAsync<T>(
        string key,
        DurableObjectStorageReadOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "durable.storage.get",
            new { key, options },
            cancellationToken);

        var envelope = JsonSerializer.Deserialize<StorageValueEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object storage returned an empty get result.");

        return DeserializeValue<T>(envelope.Value, jsonOptions);
    }

    /// <summary>Gets and deserializes multiple JSON values by key.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> GetJsonAsync<T>(
        IEnumerable<string> keys,
        DurableObjectStorageReadOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var keyArray = Keys(keys);

        var result = await DispatchAsync(
            "durable.storage.getMany",
            new { keys = keyArray, options },
            cancellationToken);

        var envelope = JsonSerializer.Deserialize<StorageValuesEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object storage returned an empty multi-get result.");

        return DeserializeValues<T>(envelope.Values, jsonOptions);
    }

    /// <summary>Serializes and stores a JSON value by key.</summary>
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
            "durable.storage.put",
            new { key, value = JsonSerializer.SerializeToElement(value, jsonOptions), options },
            cancellationToken);
    }

    /// <summary>Serializes and stores multiple JSON values by key.</summary>
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
            "durable.storage.putMany",
            new { values = serialized, options },
            cancellationToken);
    }

    /// <summary>Deletes a value by key and returns whether a value was deleted.</summary>
    public async Task<bool> DeleteAsync(
        string key,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = await DispatchAsync(
            "durable.storage.delete",
            new { key, options },
            cancellationToken);

        return JsonSerializer.Deserialize<DeleteResultEnvelope>(result, JsonOptions)?.Deleted ?? false;
    }

    /// <summary>Deletes multiple values by key and returns the number of deleted values.</summary>
    public async Task<int> DeleteAsync(
        IEnumerable<string> keys,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var keyArray = Keys(keys);

        var result = await DispatchAsync(
            "durable.storage.deleteMany",
            new { keys = keyArray, options },
            cancellationToken);

        return JsonSerializer.Deserialize<DeleteManyResultEnvelope>(result, JsonOptions)?.DeletedCount ?? 0;
    }

    /// <summary>Deletes every value in this Durable Object's storage.</summary>
    public Task DeleteAllAsync(
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.storage.deleteAll", new { options }, cancellationToken);

    /// <summary>Synchronizes any pending Durable Object storage writes to disk.</summary>
    public Task SyncAsync(CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.storage.sync", new { }, cancellationToken);

    /// <summary>Lists storage entries and deserializes each value as JSON.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectStorageListOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.list",
            new { options },
            cancellationToken);

        var envelope = JsonSerializer.Deserialize<StorageValuesEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object storage returned an empty list result.");

        return DeserializeValues<T>(envelope.Values, jsonOptions);
    }

    /// <summary>Gets the scheduled alarm time, when an alarm is set.</summary>
    public async Task<DateTimeOffset?> GetAlarmAsync(
        DurableObjectStorageReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var milliseconds = await GetAlarmUnixTimeMillisecondsAsync(options, cancellationToken);
        return milliseconds is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value);
    }

    /// <summary>Gets the scheduled alarm time as Unix epoch milliseconds, when an alarm is set.</summary>
    public async Task<long?> GetAlarmUnixTimeMillisecondsAsync(
        DurableObjectStorageReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await DispatchAsync(
            "durable.storage.getAlarm",
            new { options },
            cancellationToken);

        return JsonSerializer.Deserialize<AlarmEnvelope>(result, JsonOptions)?.ScheduledTime;
    }

    /// <summary>Schedules an alarm at the given time.</summary>
    public Task SetAlarmAsync(
        DateTimeOffset scheduledTime,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SetAlarmUnixTimeMillisecondsAsync(scheduledTime.ToUnixTimeMilliseconds(), options, cancellationToken);

    /// <summary>Schedules an alarm using Unix epoch milliseconds.</summary>
    public Task SetAlarmUnixTimeMillisecondsAsync(
        long scheduledTime,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.storage.setAlarm", new { scheduledTime, options }, cancellationToken);

    /// <summary>Deletes the scheduled alarm, when one is set.</summary>
    public Task DeleteAlarmAsync(
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        DispatchAsync("durable.storage.deleteAlarm", new { options }, cancellationToken);

    /// <summary>Runs storage operations in one Durable Object storage transaction.</summary>
    public async Task TransactionAsync(
        Func<DurableObjectTransaction, Task> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            await callback(transaction);
            if (!transaction.IsCompleted)
                await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            if (!transaction.IsCompleted)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>Runs storage operations in one Durable Object storage transaction and returns a managed result.</summary>
    public async Task<TResult> TransactionAsync<TResult>(
        Func<DurableObjectTransaction, Task<TResult>> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await callback(transaction);
            if (!transaction.IsCompleted)
                await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            if (!transaction.IsCompleted)
                await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private Task<string> DispatchAsync(string operation, object payload, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            BindingName,
            operation,
            JsonSerializer.Serialize(payload, JsonOptions));

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private async Task<DurableObjectTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(
            "durable.storage.transaction.begin",
            new { },
            cancellationToken);
        var envelope = JsonSerializer.Deserialize<TransactionHandleEnvelope>(result, JsonOptions)
            ?? throw new WorkersException("Durable Object storage returned an empty transaction result.");

        return new DurableObjectTransaction(_invocationId, envelope.Handle, _dispatcher);
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

    private sealed record AlarmEnvelope(long? ScheduledTime);

    private sealed record BookmarkEnvelope(string Bookmark);

    private sealed record TransactionHandleEnvelope(string Handle);
}
