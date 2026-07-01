namespace Workers;

/// <summary>Options used when reading Durable Object storage.</summary>
public record DurableObjectStorageReadOptions
{
    /// <summary>Allows the read to run concurrently with other storage operations on the object.</summary>
    public bool? AllowConcurrency { get; init; }

    /// <summary>Bypasses the in-memory cache maintained by the Durable Object runtime.</summary>
    public bool? NoCache { get; init; }
}

/// <summary>Options used when writing Durable Object storage.</summary>
public record DurableObjectStorageWriteOptions
{
    /// <summary>Allows the write to run concurrently with other storage operations on the object.</summary>
    public bool? AllowConcurrency { get; init; }

    /// <summary>Allows the write to return before the runtime confirms persistence.</summary>
    public bool? AllowUnconfirmed { get; init; }

    /// <summary>Bypasses the in-memory cache maintained by the Durable Object runtime.</summary>
    public bool? NoCache { get; init; }
}

/// <summary>Options used when listing Durable Object storage keys.</summary>
public sealed record DurableObjectStorageListOptions : DurableObjectStorageReadOptions
{
    /// <summary>Only returns keys greater than or equal to this key.</summary>
    public string? Start { get; init; }

    /// <summary>Only returns keys strictly greater than this key.</summary>
    public string? StartAfter { get; init; }

    /// <summary>Only returns keys less than this key.</summary>
    public string? End { get; init; }

    /// <summary>Only returns keys with this prefix.</summary>
    public string? Prefix { get; init; }

    /// <summary>Returns keys in reverse lexicographic order.</summary>
    public bool? Reverse { get; init; }

    /// <summary>Limits the number of keys returned.</summary>
    public int? Limit { get; init; }
}

/// <summary>Options used when listing Durable Object synchronous key-value storage keys.</summary>
public sealed record DurableObjectKvListOptions
{
    /// <summary>Only returns keys greater than or equal to this key.</summary>
    public string? Start { get; init; }

    /// <summary>Only returns keys strictly greater than this key.</summary>
    public string? StartAfter { get; init; }

    /// <summary>Only returns keys less than this key.</summary>
    public string? End { get; init; }

    /// <summary>Only returns keys with this prefix.</summary>
    public string? Prefix { get; init; }

    /// <summary>Returns keys in reverse lexicographic order.</summary>
    public bool? Reverse { get; init; }

    /// <summary>Limits the number of keys returned.</summary>
    public int? Limit { get; init; }
}
