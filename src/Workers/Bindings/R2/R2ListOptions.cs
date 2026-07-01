namespace Workers;

/// <summary>Options for listing objects in an R2 bucket.</summary>
public sealed class R2ListOptions
{
    /// <summary>The maximum number of objects to return. Cloudflare caps this at 1000.</summary>
    public int? Limit { get; init; }

    /// <summary>Only returns keys that start with this prefix.</summary>
    public string? Prefix { get; init; }

    /// <summary>Starts listing after this key.</summary>
    public string? StartAfter { get; init; }

    /// <summary>An opaque cursor returned from a previous list operation.</summary>
    public string? Cursor { get; init; }

    /// <summary>A delimiter used to group keys into common prefixes.</summary>
    public string? Delimiter { get; init; }

    /// <summary>Includes HTTP metadata for listed objects.</summary>
    public bool IncludeHttpMetadata { get; init; }

    /// <summary>Includes custom metadata for listed objects.</summary>
    public bool IncludeCustomMetadata { get; init; }
}

/// <summary>A page of R2 objects returned from a list operation.</summary>
public sealed record R2Objects(
    IReadOnlyList<R2Object> Objects,
    bool Truncated,
    string? Cursor,
    IReadOnlyList<string> DelimitedPrefixes);

internal sealed class R2ListRequest
{
    public int? Limit { get; init; }

    public string? Prefix { get; init; }

    public string? StartAfter { get; init; }

    public string? Cursor { get; init; }

    public string? Delimiter { get; init; }

    public bool IncludeHttpMetadata { get; init; }

    public bool IncludeCustomMetadata { get; init; }

    public static R2ListRequest From(R2ListOptions? options)
    {
        if (options?.Limit is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(options), options.Limit, "R2 list limit must be from 1 through 1000.");

        return new R2ListRequest
        {
            Limit = options?.Limit,
            Prefix = options?.Prefix,
            StartAfter = options?.StartAfter,
            Cursor = options?.Cursor,
            Delimiter = options?.Delimiter,
            IncludeHttpMetadata = options?.IncludeHttpMetadata ?? false,
            IncludeCustomMetadata = options?.IncludeCustomMetadata ?? false
        };
    }
}

internal sealed class R2ObjectsEnvelope
{
    public IReadOnlyList<R2ObjectEnvelope> Objects { get; init; } = [];

    public bool Truncated { get; init; }

    public string? Cursor { get; init; }

    public IReadOnlyList<string> DelimitedPrefixes { get; init; } = [];
}
