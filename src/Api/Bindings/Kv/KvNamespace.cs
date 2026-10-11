namespace Workers;

public interface IKvNamespace : IBinding
{
    Task<string?> GetTextAsync(string key);
    Task<string?> GetTextAsync(string key, KvGetOptions? options);
    Task<byte[]?> GetBytesAsync(string key);
    Task<T?> GetJsonAsync<T>(string key);
    Task<KvValueWithMetadata<string>> GetTextWithMetadataAsync(string key, KvGetOptions? options = null);
    Task<KvValueWithMetadata<byte[]>> GetBytesWithMetadataAsync(string key, KvGetOptions? options = null);
    Task<KvValueWithMetadata<T>> GetJsonWithMetadataAsync<T>(string key, KvGetOptions? options = null);
    Task<IReadOnlyDictionary<string, string?>> GetTextBulkAsync(IEnumerable<string> keys, KvGetOptions? options = null);
    Task<IReadOnlyDictionary<string, T?>> GetJsonBulkAsync<T>(IEnumerable<string> keys, KvGetOptions? options = null);
    Task PutTextAsync(string key, string value, KvPutOptions? options = null);
    Task PutBytesAsync(string key, ReadOnlyMemory<byte> value, KvPutOptions? options = null);
    Task PutJsonAsync<T>(string key, T value, KvPutOptions? options = null);
    Task DeleteAsync(string key);
    Task<KvListResult> ListAsync(KvListOptions? options = null);
}

public sealed class KvGetOptions
{
    public ulong? CacheTtl { get; init; }
}

public sealed class KvPutOptions
{
    public ulong? Expiration { get; init; }
    public ulong? ExpirationTtl { get; init; }
    public object? Metadata { get; init; }
}

public sealed class KvListOptions
{
    public int? Limit { get; init; }
    public string? Prefix { get; init; }
    public string? Cursor { get; init; }
}

public sealed record KvKey(string Name, ulong? Expiration, JsonValue? Metadata);
public sealed record KvListResult(IReadOnlyList<KvKey> Keys, bool ListComplete, string? Cursor);
public sealed record KvValueWithMetadata<T>(T? Value, JsonValue? Metadata);
