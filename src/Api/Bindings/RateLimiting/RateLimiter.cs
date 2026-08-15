namespace Workers;

public interface IRateLimiter : IBinding
{
    Task<RateLimitOutcome> LimitAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record RateLimitOutcome(bool Success);
