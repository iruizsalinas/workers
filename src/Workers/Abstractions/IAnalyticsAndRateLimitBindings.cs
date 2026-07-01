namespace Workers;

/// <summary>Represents a Workers Rate Limiting binding.</summary>
public interface IRateLimiter : IBinding
{
    /// <summary>Checks and consumes rate-limit capacity for a key.</summary>
    Task<RateLimitOutcome> LimitAsync(string key, CancellationToken cancellationToken = default);
}

/// <summary>Represents an Analytics Engine dataset binding.</summary>
public interface IAnalyticsEngineDataset : IBinding
{
    /// <summary>Writes a data point to the dataset.</summary>
    Task WriteDataPointAsync(AnalyticsEngineDataPoint dataPoint, CancellationToken cancellationToken = default);
}
