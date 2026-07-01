using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Workers;

/// <summary>Persistent storage attached to a Durable Object instance.</summary>
public sealed class DurableObjectStorage
{
    internal const string BindingName = "$durableObjectState";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DurableObjectStorageJsonContext JsonContext = new(new JsonSerializerOptions(JsonOptions));

    private readonly string _invocationId;
    private readonly IBindingDispatcher _dispatcher;
    private readonly JSObject? _nativeState;

    internal DurableObjectStorage(string invocationId, IBindingDispatcher dispatcher, JSObject? nativeState = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        _invocationId = invocationId;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _nativeState = nativeState;
        Kv = new DurableObjectKvStorage(invocationId, dispatcher, nativeState);
        Sql = new DurableObjectSqlStorage(invocationId, dispatcher, nativeState);
    }

    /// <summary>Synchronous key-value storage for SQLite-backed Durable Objects.</summary>
    public DurableObjectKvStorage Kv { get; }

    /// <summary>SQLite storage for SQLite-backed Durable Objects.</summary>
    public DurableObjectSqlStorage Sql { get; }

    /// <summary>Gets a point-in-time recovery bookmark for the current storage state.</summary>
    public async Task<string> GetCurrentBookmarkAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfNativeAsyncStorageApi("getCurrentBookmark");

        var result = await DispatchAsync(
            "durable.storage.getCurrentBookmark",
            EmptyPayload(),
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.BookmarkEnvelope)?.Bookmark
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
        ThrowIfNativeAsyncStorageApi("getBookmarkForTime");

        var result = await DispatchAsync(
            "durable.storage.getBookmarkForTime",
            JsonSerializer.Serialize(new DurableStorageBookmarkForTimePayload { Timestamp = timestamp }, JsonContext.DurableStorageBookmarkForTimePayload),
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.BookmarkEnvelope)?.Bookmark
            ?? throw new WorkersException("Durable Object storage returned an empty bookmark result.");
    }

    /// <summary>Schedules storage restore to the given bookmark on the Durable Object's next session.</summary>
    public async Task<string> OnNextSessionRestoreBookmarkAsync(
        string bookmark,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookmark);

        ThrowIfNativeAsyncStorageApi("onNextSessionRestoreBookmark");

        var result = await DispatchAsync(
            "durable.storage.onNextSessionRestoreBookmark",
            JsonSerializer.Serialize(new DurableStorageBookmarkPayload { Bookmark = bookmark }, JsonContext.DurableStorageBookmarkPayload),
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.BookmarkEnvelope)?.Bookmark
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

        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectStorage.Get(
                _nativeState,
                key,
                AsyncReadOptionsJson(options))
            : await DispatchAsync(
                "durable.storage.get",
                JsonSerializer.Serialize(new DurableStorageKeyReadPayload { Key = key, Options = options }, JsonContext.DurableStorageKeyReadPayload),
                cancellationToken);

        var envelope = JsonSerializer.Deserialize(result, JsonContext.StorageValueEnvelope)
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
        var payloadJson = JsonSerializer.Serialize(
            new DurableStorageKeysReadPayload { Keys = keyArray, Options = options },
            JsonContext.DurableStorageKeysReadPayload);

        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectStorage.GetMany(
                _nativeState,
                JsonSerializer.Serialize(keyArray, JsonContext.StringArray),
                AsyncReadOptionsJson(options))
            : await DispatchAsync(
                "durable.storage.getMany",
                payloadJson,
                cancellationToken);

        var envelope = JsonSerializer.Deserialize(result, JsonContext.StorageValuesEnvelope)
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

        if (_nativeState is not null && OperatingSystem.IsBrowser())
        {
            NativeDurableObjectStorage.Put(
                _nativeState,
                key,
                JsonSerializer.Serialize(value, jsonOptions),
                AsyncWriteOptionsJson(options));
            return Task.CompletedTask;
        }

        return DispatchAsync(
            "durable.storage.put",
            JsonSerializer.Serialize(
                new DurableStoragePutPayload
                {
                    Key = key,
                    Value = JsonSerializer.SerializeToElement(value, jsonOptions),
                    Options = options
                },
                JsonContext.DurableStoragePutPayload),
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

        if (_nativeState is not null && OperatingSystem.IsBrowser())
        {
            NativeDurableObjectStorage.PutMany(
                _nativeState,
                JsonSerializer.Serialize(
                    new DurableStoragePutManyPayload { Values = serialized, Options = options },
                    JsonContext.DurableStoragePutManyPayload),
                AsyncWriteOptionsJson(options));
            return Task.CompletedTask;
        }

        return DispatchAsync(
            "durable.storage.putMany",
            JsonSerializer.Serialize(new DurableStoragePutManyPayload { Values = serialized, Options = options }, JsonContext.DurableStoragePutManyPayload),
            cancellationToken);
    }

    /// <summary>Deletes a value by key and returns whether a value was deleted.</summary>
    public async Task<bool> DeleteAsync(
        string key,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectStorage.Delete(_nativeState, key, AsyncWriteOptionsJson(options))
            : await DispatchAsync(
                "durable.storage.delete",
                JsonSerializer.Serialize(new DurableStorageKeyWritePayload { Key = key, Options = options }, JsonContext.DurableStorageKeyWritePayload),
                cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.DeleteResultEnvelope)?.Deleted ?? false;
    }

    /// <summary>Deletes multiple values by key and returns the number of deleted values.</summary>
    public async Task<int> DeleteAsync(
        IEnumerable<string> keys,
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var keyArray = Keys(keys);

        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectStorage.DeleteMany(
                _nativeState,
                JsonSerializer.Serialize(keyArray, JsonContext.StringArray),
                AsyncWriteOptionsJson(options))
            : await DispatchAsync(
                "durable.storage.deleteMany",
                JsonSerializer.Serialize(new DurableStorageKeysWritePayload { Keys = keyArray, Options = options }, JsonContext.DurableStorageKeysWritePayload),
                cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.DeleteManyResultEnvelope)?.DeletedCount ?? 0;
    }

    /// <summary>Deletes every value in this Durable Object's storage.</summary>
    public Task DeleteAllAsync(
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_nativeState is not null && OperatingSystem.IsBrowser())
        {
            NativeDurableObjectStorage.DeleteAll(_nativeState, AsyncWriteOptionsJson(options));
            return Task.CompletedTask;
        }

        return DispatchAsync(
            "durable.storage.deleteAll",
            JsonSerializer.Serialize(new DurableStorageWriteOptionsPayload { Options = options }, JsonContext.DurableStorageWriteOptionsPayload),
            cancellationToken);
    }

    /// <summary>Synchronizes any pending Durable Object storage writes to disk.</summary>
    public Task SyncAsync(CancellationToken cancellationToken = default) =>
        _nativeState is not null && OperatingSystem.IsBrowser()
            ? throw NativeAsyncStorageApiException("sync")
            : DispatchAsync("durable.storage.sync", EmptyPayload(), cancellationToken);

    /// <summary>Lists storage entries and deserializes each value as JSON.</summary>
    public async Task<IReadOnlyDictionary<string, T?>> ListJsonAsync<T>(
        DurableObjectStorageListOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default)
    {
        var payloadJson = JsonSerializer.Serialize(
            new DurableStorageListPayload { Options = options },
            JsonContext.DurableStorageListPayload);
        var result = _nativeState is not null && OperatingSystem.IsBrowser()
            ? NativeDurableObjectStorage.List(_nativeState, AsyncListOptionsJson(options, payloadJson))
            : await DispatchAsync(
                "durable.storage.list",
                payloadJson,
                cancellationToken);

        var envelope = JsonSerializer.Deserialize(result, JsonContext.StorageValuesEnvelope)
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
        ThrowIfNativeAsyncStorageApi("getAlarm");

        var result = await DispatchAsync(
            "durable.storage.getAlarm",
            JsonSerializer.Serialize(new DurableStorageReadOptionsPayload { Options = options }, JsonContext.DurableStorageReadOptionsPayload),
            cancellationToken);

        return JsonSerializer.Deserialize(result, JsonContext.AlarmEnvelope)?.ScheduledTime;
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
        _nativeState is not null && OperatingSystem.IsBrowser()
            ? throw NativeAsyncStorageApiException("setAlarm")
            : DispatchAsync(
                "durable.storage.setAlarm",
                JsonSerializer.Serialize(new DurableStorageSetAlarmPayload { ScheduledTime = scheduledTime, Options = options }, JsonContext.DurableStorageSetAlarmPayload),
                cancellationToken);

    /// <summary>Deletes the scheduled alarm, when one is set.</summary>
    public Task DeleteAlarmAsync(
        DurableObjectStorageWriteOptions? options = null,
        CancellationToken cancellationToken = default) =>
        _nativeState is not null && OperatingSystem.IsBrowser()
            ? throw NativeAsyncStorageApiException("deleteAlarm")
            : DispatchAsync(
                "durable.storage.deleteAlarm",
                JsonSerializer.Serialize(new DurableStorageWriteOptionsPayload { Options = options }, JsonContext.DurableStorageWriteOptionsPayload),
                cancellationToken);

    /// <summary>Runs storage operations in one Durable Object storage transaction.</summary>
    public async Task TransactionAsync(
        Func<DurableObjectTransaction, Task> callback,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ThrowIfNativeAsyncTransaction();

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
        ThrowIfNativeAsyncTransaction();

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

    private Task<string> DispatchAsync(string operation, string payloadJson, CancellationToken cancellationToken)
    {
        var invocation = new BindingInvocation(
            _invocationId,
            BindingName,
            operation,
            payloadJson);

        return _dispatcher.DispatchAsync(invocation, cancellationToken);
    }

    private async Task<DurableObjectTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        var result = await DispatchAsync(
            "durable.storage.transaction.begin",
            EmptyPayload(),
            cancellationToken);
        var envelope = JsonSerializer.Deserialize(result, JsonContext.TransactionHandleEnvelope)
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

    private static string EmptyPayload() =>
        JsonSerializer.Serialize(new DurableStorageEmptyPayload(), JsonContext.DurableStorageEmptyPayload);

    private static string AsyncReadOptionsJson(DurableObjectStorageReadOptions? options)
    {
        ThrowIfAsyncReadOptions(options);
        return JsonSerializer.Serialize(new DurableStorageReadOptionsPayload { Options = options }, JsonContext.DurableStorageReadOptionsPayload);
    }

    private static string AsyncWriteOptionsJson(DurableObjectStorageWriteOptions? options)
    {
        ThrowIfAsyncWriteOptions(options);
        return JsonSerializer.Serialize(new DurableStorageWriteOptionsPayload { Options = options }, JsonContext.DurableStorageWriteOptionsPayload);
    }

    private static string AsyncListOptionsJson(DurableObjectStorageListOptions? options, string payloadJson)
    {
        ThrowIfAsyncReadOptions(options);
        return payloadJson;
    }

    private static void ThrowIfAsyncReadOptions(DurableObjectStorageReadOptions? options)
    {
        if (options?.AllowConcurrency is not null || options?.NoCache is not null)
            throw new WorkersException(
                "Durable Object storage options AllowConcurrency and NoCache require Cloudflare's async storage API, which is not available through the native nested Durable Object storage bridge. Use Storage.Kv or omit those options.");
    }

    private static void ThrowIfAsyncWriteOptions(DurableObjectStorageWriteOptions? options)
    {
        if (options?.AllowConcurrency is not null || options?.AllowUnconfirmed is not null || options?.NoCache is not null)
            throw new WorkersException(
                "Durable Object storage options AllowConcurrency, AllowUnconfirmed, and NoCache require Cloudflare's async storage API, which is not available through the native nested Durable Object storage bridge. Use Storage.Kv or omit those options.");
    }

    private void ThrowIfNativeAsyncTransaction()
    {
        if (_nativeState is not null && OperatingSystem.IsBrowser())
            throw new WorkersException(
                "Durable Object async storage transactions cannot be held open across the C# WebAssembly boundary. Use Storage.Sql.TransactionSyncRawAsync for SQLite-backed Durable Objects, or perform individual Storage.Kv operations.");
    }

    private void ThrowIfNativeAsyncStorageApi(string operation)
    {
        if (_nativeState is not null && OperatingSystem.IsBrowser())
            throw NativeAsyncStorageApiException(operation);
    }

    private static WorkersException NativeAsyncStorageApiException(string operation) =>
        new($"Durable Object storage operation '{operation}' uses Cloudflare's async storage API, which is not available through the native nested Durable Object storage bridge. Use SQLite-backed Storage.Kv or Storage.Sql APIs where possible.");
}

[SupportedOSPlatform("browser")]
internal static partial class NativeDurableObjectStorage
{
    [JSImport("cloudflareWorkers.durableStorage.get", "dotnet.js")]
    internal static partial string Get(JSObject state, string key, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.put", "dotnet.js")]
    internal static partial void Put(JSObject state, string key, string valueJson, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.getMany", "dotnet.js")]
    internal static partial string GetMany(JSObject state, string keysJson, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.putMany", "dotnet.js")]
    internal static partial void PutMany(JSObject state, string valuesJson, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.delete", "dotnet.js")]
    internal static partial string Delete(JSObject state, string key, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.deleteMany", "dotnet.js")]
    internal static partial string DeleteMany(JSObject state, string keysJson, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.deleteAll", "dotnet.js")]
    internal static partial void DeleteAll(JSObject state, string optionsJson);

    [JSImport("cloudflareWorkers.durableStorage.list", "dotnet.js")]
    internal static partial string List(JSObject state, string optionsJson);

}

internal sealed class DurableStorageEmptyPayload
{
}

internal sealed class DurableStorageBookmarkForTimePayload
{
    public long Timestamp { get; set; }
}

internal sealed class DurableStorageBookmarkPayload
{
    public string Bookmark { get; set; } = "";
}

internal sealed class DurableStorageKeyReadPayload
{
    public string Key { get; set; } = "";

    public DurableObjectStorageReadOptions? Options { get; set; }
}

internal sealed class DurableStorageKeysReadPayload
{
    public IReadOnlyList<string> Keys { get; set; } = [];

    public DurableObjectStorageReadOptions? Options { get; set; }
}

internal sealed class DurableStorageKeyWritePayload
{
    public string Key { get; set; } = "";

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageKeysWritePayload
{
    public IReadOnlyList<string> Keys { get; set; } = [];

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStoragePutPayload
{
    public string Key { get; set; } = "";

    public JsonElement Value { get; set; }

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStoragePutManyPayload
{
    public IReadOnlyDictionary<string, JsonElement> Values { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageReadOptionsPayload
{
    public DurableObjectStorageReadOptions? Options { get; set; }
}

internal sealed class DurableStorageWriteOptionsPayload
{
    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class DurableStorageListPayload
{
    public DurableObjectStorageListOptions? Options { get; set; }
}

internal sealed class DurableStorageSetAlarmPayload
{
    public long ScheduledTime { get; set; }

    public DurableObjectStorageWriteOptions? Options { get; set; }
}

internal sealed class StorageValueEnvelope
{
    public JsonElement Value { get; set; }
}

internal sealed class StorageValuesEnvelope
{
    public IReadOnlyDictionary<string, JsonElement> Values { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);
}

internal sealed class DeleteResultEnvelope
{
    public bool Deleted { get; set; }
}

internal sealed class DeleteManyResultEnvelope
{
    public int DeletedCount { get; set; }
}

internal sealed class AlarmEnvelope
{
    public long? ScheduledTime { get; set; }
}

internal sealed class BookmarkEnvelope
{
    public string Bookmark { get; set; } = "";
}

internal sealed class TransactionHandleEnvelope
{
    public string Handle { get; set; } = "";
}

[JsonSerializable(typeof(DurableStorageEmptyPayload))]
[JsonSerializable(typeof(DurableStorageBookmarkForTimePayload))]
[JsonSerializable(typeof(DurableStorageBookmarkPayload))]
[JsonSerializable(typeof(DurableStorageKeyReadPayload))]
[JsonSerializable(typeof(DurableStorageKeysReadPayload))]
[JsonSerializable(typeof(DurableStorageKeyWritePayload))]
[JsonSerializable(typeof(DurableStorageKeysWritePayload))]
[JsonSerializable(typeof(DurableStoragePutPayload))]
[JsonSerializable(typeof(DurableStoragePutManyPayload))]
[JsonSerializable(typeof(DurableStorageReadOptionsPayload))]
[JsonSerializable(typeof(DurableStorageWriteOptionsPayload))]
[JsonSerializable(typeof(DurableStorageListPayload))]
[JsonSerializable(typeof(DurableStorageSetAlarmPayload))]
[JsonSerializable(typeof(DurableStorageTransactionHandlePayload))]
[JsonSerializable(typeof(DurableStorageTransactionKeyReadPayload))]
[JsonSerializable(typeof(DurableStorageTransactionKeysReadPayload))]
[JsonSerializable(typeof(DurableStorageTransactionKeyWritePayload))]
[JsonSerializable(typeof(DurableStorageTransactionKeysWritePayload))]
[JsonSerializable(typeof(DurableStorageTransactionPutPayload))]
[JsonSerializable(typeof(DurableStorageTransactionPutManyPayload))]
[JsonSerializable(typeof(DurableStorageTransactionListPayload))]
[JsonSerializable(typeof(DurableStorageSqlStatementPayload))]
[JsonSerializable(typeof(DurableStorageSqlStatementsPayload))]
[JsonSerializable(typeof(DurableStorageSqlDatabaseSizeEnvelope))]
[JsonSerializable(typeof(DurableStorageSqlOneEnvelope))]
[JsonSerializable(typeof(DurableStorageSqlRowsEnvelope))]
[JsonSerializable(typeof(DurableStorageSqlTransactionRawEnvelope))]
[JsonSerializable(typeof(DurableStorageSqlCursorOpenEnvelope))]
[JsonSerializable(typeof(DurableStorageSqlCursorHandlePayload))]
[JsonSerializable(typeof(DurableStorageSqlCursorNextEnvelope))]
[JsonSerializable(typeof(DurableStorageSqlCursorRawNextEnvelope))]
[JsonSerializable(typeof(DurableObjectSqlRawResult))]
[JsonSerializable(typeof(D1Value))]
[JsonSerializable(typeof(DurableStateCallbackPayload))]
[JsonSerializable(typeof(DurableStateAbortPayload))]
[JsonSerializable(typeof(DurableStateWebSocketHandlePayload))]
[JsonSerializable(typeof(DurableStateWebSocketAcceptPayload))]
[JsonSerializable(typeof(DurableStateWebSocketTagPayload))]
[JsonSerializable(typeof(DurableStateWebSocketAutoResponsePayload))]
[JsonSerializable(typeof(DurableStateWebSocketEventTimeoutPayload))]
[JsonSerializable(typeof(DurableStateWebSocketHandlesEnvelope))]
[JsonSerializable(typeof(DurableStateWebSocketTagsEnvelope))]
[JsonSerializable(typeof(DurableStateWebSocketAutoResponseEnvelope))]
[JsonSerializable(typeof(DurableStateWebSocketAutoResponseTimestampEnvelope))]
[JsonSerializable(typeof(DurableStateWebSocketEventTimeoutEnvelope))]
[JsonSerializable(typeof(WebSocketAutoResponse))]
[JsonSerializable(typeof(DurableObjectStorageReadOptions))]
[JsonSerializable(typeof(DurableObjectStorageWriteOptions))]
[JsonSerializable(typeof(DurableObjectStorageListOptions))]
[JsonSerializable(typeof(StorageValueEnvelope))]
[JsonSerializable(typeof(StorageValuesEnvelope))]
[JsonSerializable(typeof(DeleteResultEnvelope))]
[JsonSerializable(typeof(DeleteManyResultEnvelope))]
[JsonSerializable(typeof(AlarmEnvelope))]
[JsonSerializable(typeof(BookmarkEnvelope))]
[JsonSerializable(typeof(TransactionHandleEnvelope))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class DurableObjectStorageJsonContext : JsonSerializerContext
{
}
