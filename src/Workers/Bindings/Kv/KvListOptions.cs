using System.Text.Json;

namespace Workers;

/// <summary>Options for listing keys in a Workers KV namespace.</summary>
public sealed class KvListOptions
{
    /// <summary>The maximum number of keys to return. Cloudflare caps this at 1000.</summary>
    public int? Limit { get; init; }

    /// <summary>An opaque cursor returned by a previous list operation.</summary>
    public string? Cursor { get; init; }

    /// <summary>Only returns keys with this prefix.</summary>
    public string? Prefix { get; init; }
}

/// <summary>A Workers KV key listing page.</summary>
public sealed record KvListResult(IReadOnlyList<KvKey> Keys, bool ListComplete, string? Cursor);

/// <summary>A Workers KV key entry.</summary>
public sealed record KvKey(string Name, ulong? Expiration, JsonElement? Metadata);

internal sealed class KvListRequest
{
    public int? Limit { get; init; }

    public string? Cursor { get; init; }

    public string? Prefix { get; init; }

    public static KvListRequest From(KvListOptions? options)
    {
        if (options?.Limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options), options.Limit, "KV list limit must be from 1 through 1000.");

        return new KvListRequest
        {
            Limit = options?.Limit,
            Cursor = options?.Cursor,
            Prefix = options?.Prefix
        };
    }
}
