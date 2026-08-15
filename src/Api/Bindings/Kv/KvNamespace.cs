namespace Workers;

public interface IKvNamespace : IBinding
{
    Task<string?> GetTextAsync(string key, CancellationToken cancellationToken = default);
    Task<string?> GetTextAsync(string key, KvGetOptions? options, CancellationToken cancellationToken = default);
    Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default);
    Task<T?> GetJsonAsync<T>(string key, CancellationToken cancellationToken = default);
    Task<KvValueWithMetadata<string>> GetTextWithMetadataAsync(string key, KvGetOptions? options = null, CancellationToken cancellationToken = default);
    Task<KvValueWithMetadata<byte[]>> GetBytesWithMetadataAsync(string key, KvGetOptions? options = null, CancellationToken cancellationToken = default);
    Task<KvValueWithMetadata<T>> GetJsonWithMetadataAsync<T>(string key, KvGetOptions? options = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, string?>> GetTextBulkAsync(IEnumerable<string> keys, KvGetOptions? options = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, T?>> GetJsonBulkAsync<T>(IEnumerable<string> keys, KvGetOptions? options = null, CancellationToken cancellationToken = default);
    Task PutTextAsync(string key, string value, KvPutOptions? options = null, CancellationToken cancellationToken = default);
    Task PutBytesAsync(string key, ReadOnlyMemory<byte> value, KvPutOptions? options = null, CancellationToken cancellationToken = default);
    Task PutJsonAsync<T>(string key, T value, KvPutOptions? options = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<KvListResult> ListAsync(KvListOptions? options = null, CancellationToken cancellationToken = default);
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

public sealed record KvKey(string Name, ulong? Expiration, JsonElement? Metadata);
public sealed record KvListResult(IReadOnlyList<KvKey> Keys, bool ListComplete, string? Cursor);
public sealed record KvValueWithMetadata<T>(T? Value, JsonElement? Metadata);
