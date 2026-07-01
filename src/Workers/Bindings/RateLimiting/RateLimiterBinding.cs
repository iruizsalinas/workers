using System.Text.Json;

namespace Workers;

/// <summary>The result of a Rate Limiting binding check.</summary>
public sealed record RateLimitOutcome(bool Success);

internal sealed class RateLimiterBinding : IRateLimiter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _invocationId;
    private readonly string _bindingName;
    private readonly IBindingDispatcher _dispatcher;

    public RateLimiterBinding(string invocationId, string bindingName, IBindingDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invocationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bindingName);
        _invocationId = invocationId;
        _bindingName = bindingName;
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<RateLimitOutcome> LimitAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var invocation = new BindingInvocation(
            _invocationId,
            _bindingName,
            "ratelimit.limit",
            JsonSerializer.Serialize(new { key }, JsonOptions));

        var result = await _dispatcher.DispatchAsync(invocation, cancellationToken);
        return JsonSerializer.Deserialize<RateLimitOutcome>(result, JsonOptions)
            ?? throw new WorkersException("Rate limiter returned an empty outcome.");
    }
}
