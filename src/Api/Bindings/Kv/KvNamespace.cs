namespace Workers;

/// <summary>A Workers KV namespace. Typed JSON reads map values to C# classes and records.</summary>
public sealed class KVNamespace
{
    private KVNamespace() { }

    /// <summary>Reads a value as text, or null when the key does not exist.</summary>
    public Task<string?> GetAsync(string key, KVNamespaceGetOptions? options = null) => WorkerApi.NotExecutable<Task<string?>>();
    /// <summary>Reads up to 100 keys as text; missing keys map to null.</summary>
    public Task<IReadOnlyDictionary<string, string?>> GetAsync(IEnumerable<string> keys, KVNamespaceGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, string?>>>();
    public Task<T?> GetJsonAsync<T>(string key, KVNamespaceGetOptions? options = null) => WorkerApi.NotExecutable<Task<T?>>();
    public Task<IReadOnlyDictionary<string, T?>> GetJsonAsync<T>(IEnumerable<string> keys, KVNamespaceGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<IReadOnlyDictionary<string, T?>>>();
    public Task<byte[]?> GetBytesAsync(string key, KVNamespaceGetOptions? options = null) => WorkerApi.NotExecutable<Task<byte[]?>>();
    public Task<ReadableStream?> GetStreamAsync(string key, KVNamespaceGetOptions? options = null) => WorkerApi.NotExecutable<Task<ReadableStream?>>();

    public Task<KVNamespaceGetWithMetadataResult<string>> GetWithMetadataAsync(string key, KVNamespaceGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<KVNamespaceGetWithMetadataResult<string>>>();
    public Task<KVNamespaceGetWithMetadataResult<T>> GetJsonWithMetadataAsync<T>(string key, KVNamespaceGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<KVNamespaceGetWithMetadataResult<T>>>();
    public Task<KVNamespaceGetWithMetadataResult<byte[]>> GetBytesWithMetadataAsync(string key, KVNamespaceGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<KVNamespaceGetWithMetadataResult<byte[]>>>();
    public Task<KVNamespaceGetWithMetadataResult<ReadableStream>> GetStreamWithMetadataAsync(string key, KVNamespaceGetOptions? options = null) =>
        WorkerApi.NotExecutable<Task<KVNamespaceGetWithMetadataResult<ReadableStream>>>();

    public Task PutAsync(string key, string value, KVNamespacePutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task PutAsync(string key, ReadOnlyMemory<byte> value, KVNamespacePutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task PutAsync(string key, ReadableStream value, KVNamespacePutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    /// <summary>Stores the value as JSON text.</summary>
    public Task PutJsonAsync<T>(string key, T value, KVNamespacePutOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task DeleteAsync(string key) => WorkerApi.NotExecutable<Task>();
    public Task<KVNamespaceListResult> ListAsync(KVNamespaceListOptions? options = null) => WorkerApi.NotExecutable<Task<KVNamespaceListResult>>();
}

public sealed class KVNamespaceGetOptions
{
    /// <summary>Seconds to cache the value at the edge; at least 30.</summary>
    public int? CacheTtl { get; init; }
}

public sealed class KVNamespacePutOptions
{
    /// <summary>Absolute expiry in seconds since the Unix epoch.</summary>
    public long? Expiration { get; init; }
    /// <summary>Expiry in seconds from now; at least 60.</summary>
    public int? ExpirationTtl { get; init; }
    /// <summary>A JSON-serializable value of up to 1024 bytes stored with the key.</summary>
    public object? Metadata { get; init; }
}

public sealed class KVNamespaceListOptions
{
    public int? Limit { get; init; }
    public string? Prefix { get; init; }
    public string? Cursor { get; init; }
}

public sealed class KVNamespaceListResult
{
    private KVNamespaceListResult() { }

    public IReadOnlyList<KVNamespaceListKey> Keys => WorkerApi.NotExecutable<IReadOnlyList<KVNamespaceListKey>>();
    public bool ListComplete => WorkerApi.NotExecutable<bool>();
    /// <summary>The cursor for the next page, or null when <see cref="ListComplete"/> is true.</summary>
    public string? Cursor => WorkerApi.NotExecutable<string?>();
    public string? CacheStatus => WorkerApi.NotExecutable<string?>();
}

public sealed class KVNamespaceListKey
{
    private KVNamespaceListKey() { }

    public string Name => WorkerApi.NotExecutable<string>();
    public long? Expiration => WorkerApi.NotExecutable<long?>();
    public JsonValue? Metadata => WorkerApi.NotExecutable<JsonValue?>();
}

public sealed class KVNamespaceGetWithMetadataResult<T>
{
    private KVNamespaceGetWithMetadataResult() { }

    public T? Value => WorkerApi.NotExecutable<T?>();
    public JsonValue? Metadata => WorkerApi.NotExecutable<JsonValue?>();
    public string? CacheStatus => WorkerApi.NotExecutable<string?>();
}
