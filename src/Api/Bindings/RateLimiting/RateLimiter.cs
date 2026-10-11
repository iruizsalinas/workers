namespace Workers;

public interface IRateLimiter : IBinding
{
    Task<RateLimitOutcome> LimitAsync(string key);
}

public sealed record RateLimitOutcome(bool Success);
