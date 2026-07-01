using System.Text.Json;

namespace Workers;

/// <summary>Represents a Workers KV namespace binding.</summary>
public interface IKvNamespace : IBinding
{
    /// <summary>Gets a UTF-8 text value by key.</summary>
    Task<string?> GetTextAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets a UTF-8 text value by key.</summary>
    Task<string?> GetTextAsync(string key, KvGetOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Gets a UTF-8 text value and its metadata by key.</summary>
    Task<KvValueWithMetadata<string>> GetTextWithMetadataAsync(
        string key,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets multiple UTF-8 text values by key.</summary>
    Task<IReadOnlyDictionary<string, string?>> GetTextBulkAsync(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets multiple UTF-8 text values and metadata by key.</summary>
    Task<IReadOnlyDictionary<string, KvValueWithMetadata<string>>> GetTextBulkWithMetadataAsync(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a binary value by key.</summary>
    Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Gets a binary value by key.</summary>
    Task<byte[]?> GetBytesAsync(string key, KvGetOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Gets a binary value and its metadata by key.</summary>
    Task<KvValueWithMetadata<byte[]>> GetBytesWithMetadataAsync(
        string key,
        KvGetOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets and deserializes a JSON value by key.</summary>
    Task<T?> GetJsonAsync<T>(
        string key,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets and deserializes a JSON value by key.</summary>
    Task<T?> GetJsonAsync<T>(
        string key,
        KvGetOptions? options,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets and deserializes a JSON value and its metadata by key.</summary>
    Task<KvValueWithMetadata<T>> GetJsonWithMetadataAsync<T>(
        string key,
        KvGetOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets and deserializes multiple JSON values by key.</summary>
    Task<IReadOnlyDictionary<string, T?>> GetJsonBulkAsync<T>(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Gets and deserializes multiple JSON values and metadata by key.</summary>
    Task<IReadOnlyDictionary<string, KvValueWithMetadata<T>>> GetJsonBulkWithMetadataAsync<T>(
        IEnumerable<string> keys,
        KvGetOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a UTF-8 text value by key.</summary>
    Task PutTextAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Stores a UTF-8 text value by key.</summary>
    Task PutTextAsync(string key, string value, KvPutOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Stores a binary value by key.</summary>
    Task PutBytesAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>Stores a binary value by key.</summary>
    Task PutBytesAsync(string key, ReadOnlyMemory<byte> value, KvPutOptions? options, CancellationToken cancellationToken = default);

    /// <summary>Serializes and stores a JSON value by key.</summary>
    Task PutJsonAsync<T>(
        string key,
        T value,
        KvPutOptions? options = null,
        JsonSerializerOptions? jsonOptions = null,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a value by key.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Lists keys in the namespace.</summary>
    Task<KvListResult> ListAsync(KvListOptions? options = null, CancellationToken cancellationToken = default);
}
