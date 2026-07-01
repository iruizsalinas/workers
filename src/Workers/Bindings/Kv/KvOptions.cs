using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Workers;

/// <summary>Options for reading a value from Workers KV.</summary>
public sealed class KvGetOptions
{
    /// <summary>
    /// Caches the value in the current edge location for this many seconds.
    /// Cloudflare requires this to be at least 60 seconds.
    /// </summary>
    public ulong? CacheTtl { get; init; }
}

/// <summary>Options for storing a value in Workers KV.</summary>
public sealed class KvPutOptions
{
    /// <summary>Unix timestamp, in seconds, when the key should expire.</summary>
    public ulong? Expiration { get; init; }

    /// <summary>Number of seconds until the key should expire.</summary>
    public ulong? ExpirationTtl { get; init; }

    /// <summary>JSON-serializable metadata stored with the key.</summary>
    public object? Metadata { get; init; }
}

/// <summary>A KV value paired with its metadata.</summary>
public sealed record KvValueWithMetadata<TValue>(TValue? Value, JsonElement? Metadata)
{
    /// <summary>Deserializes the metadata as JSON.</summary>
    public TMetadata? MetadataAs<TMetadata>(JsonSerializerOptions? options = null) =>
        Metadata is { } metadata
            ? metadata.Deserialize<TMetadata>(options)
            : default;
}

internal static class KvPayloads
{
    public static JsonObject Get(string key, KvGetOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new JsonObject
        {
            ["key"] = key,
            ["options"] = KvGetOptionsEnvelope.From(options)
        };
    }
}

internal sealed class KvGetBulkRequest
{
    [JsonPropertyName("keys")]
    public IReadOnlyList<string> Keys { get; init; } = [];

    [JsonPropertyName("options")]
    public JsonObject? Options { get; init; }

    public static KvGetBulkRequest From(IEnumerable<string> keys, KvGetOptions? options)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var keyList = keys.ToArray();

        if (keyList.Length > 100)
            throw new ArgumentOutOfRangeException(nameof(keys), keyList.Length, "KV bulk get supports at most 100 keys.");

        foreach (var key in keyList)
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new KvGetBulkRequest { Keys = keyList, Options = KvGetOptionsEnvelope.From(options) };
    }
}

internal sealed class KvGetOptionsEnvelope
{
    public static JsonObject? From(KvGetOptions? options)
    {
        if (options is null)
            return null;

        if (options.CacheTtl is < 60)
            throw new ArgumentOutOfRangeException(nameof(options), options.CacheTtl, "KV cache TTL must be at least 60 seconds.");

        return new JsonObject
        {
            ["cacheTtl"] = options.CacheTtl
        };
    }
}

internal static class KvPutTextRequest
{
    public static JsonObject From(string key, string value, KvPutOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        return new JsonObject
        {
            ["key"] = key,
            ["value"] = value,
            ["options"] = KvPutOptionsEnvelope.From(options)?.ToJsonObject()
        };
    }
}

internal static class KvPutBytesRequest
{
    public static JsonObject From(string key, ReadOnlyMemory<byte> value, KvPutOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new JsonObject
        {
            ["key"] = key,
            ["bodyBase64"] = Convert.ToBase64String(value.Span),
            ["options"] = KvPutOptionsEnvelope.From(options)?.ToJsonObject()
        };
    }
}

internal static class KvPutJsonRequest
{
    public static JsonObject From<T>(string key, T value, KvPutOptions? options, JsonSerializerOptions? jsonOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return new JsonObject
        {
            ["key"] = key,
            ["valueJson"] = JsonSerializer.Serialize(value, jsonOptions),
            ["options"] = KvPutOptionsEnvelope.From(options)?.ToJsonObject()
        };
    }
}

internal sealed class KvPutOptionsEnvelope
{
    [JsonPropertyName("expiration")]
    public ulong? Expiration { get; init; }

    [JsonPropertyName("expirationTtl")]
    public ulong? ExpirationTtl { get; init; }

    [JsonPropertyName("metadata")]
    public object? Metadata { get; init; }

    public static KvPutOptionsEnvelope? From(KvPutOptions? options)
    {
        if (options is null)
            return null;

        return new KvPutOptionsEnvelope
        {
            Expiration = options.Expiration,
            ExpirationTtl = options.ExpirationTtl,
            Metadata = options.Metadata
        };
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "KV metadata intentionally accepts user-provided JSON-compatible objects; browser-wasm workers keep reflection JSON enabled by default.")]
    public JsonObject ToJsonObject()
    {
        var json = new JsonObject
        {
            ["expiration"] = Expiration,
            ["expirationTtl"] = ExpirationTtl
        };

        if (Metadata is not null)
            json["metadata"] = JsonSerializer.SerializeToNode(Metadata);

        return json;
    }
}

internal sealed class KvTextWithMetadataEnvelope
{
    public string? Value { get; init; }

    public JsonElement? Metadata { get; init; }
}

internal sealed class KvBytesEnvelope
{
    public string? BodyBase64 { get; init; }
}

internal sealed class KvBytesWithMetadataEnvelope
{
    public string? BodyBase64 { get; init; }

    public JsonElement? Metadata { get; init; }
}

internal sealed class KvJsonWithMetadataEnvelope
{
    public JsonElement? Value { get; init; }

    public JsonElement? Metadata { get; init; }
}

internal sealed class KvTextBulkEnvelope
{
    public IReadOnlyDictionary<string, string?> Values { get; init; } = new Dictionary<string, string?>(StringComparer.Ordinal);
}

internal sealed class KvJsonBulkEnvelope
{
    public IReadOnlyDictionary<string, JsonElement?> Values { get; init; } = new Dictionary<string, JsonElement?>(StringComparer.Ordinal);
}

internal sealed class KvTextBulkWithMetadataEnvelope
{
    public IReadOnlyDictionary<string, KvTextWithMetadataEnvelope> Values { get; init; } =
        new Dictionary<string, KvTextWithMetadataEnvelope>(StringComparer.Ordinal);
}

internal sealed class KvJsonBulkWithMetadataEnvelope
{
    public IReadOnlyDictionary<string, KvJsonWithMetadataEnvelope> Values { get; init; } =
        new Dictionary<string, KvJsonWithMetadataEnvelope>(StringComparer.Ordinal);
}
