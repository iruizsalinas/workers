using System.Text.Json.Serialization;

namespace Workers;

/// <summary>Options for looking up or deleting entries in the Workers Cache API.</summary>
public sealed class CacheQueryOptions
{
    /// <summary>Consider the request method to be GET regardless of the actual request method.</summary>
    public bool IgnoreMethod { get; init; }
}

internal sealed class CacheQueryOptionsEnvelope
{
    [JsonPropertyName("ignoreMethod")]
    public bool? IgnoreMethod { get; set; }

    public static CacheQueryOptionsEnvelope? From(CacheQueryOptions? options)
    {
        if (options is null)
            return null;

        return options.IgnoreMethod
            ? new CacheQueryOptionsEnvelope { IgnoreMethod = true }
            : null;
    }
}
