namespace Workers;

public sealed class FetchOptions
{
    public string? Method { get; init; }
    public Headers? Headers { get; init; }
    public BodyInit? Body { get; init; }
    public object? Cf { get; init; }
    public RedirectMode? Redirect { get; init; }
    public AbortSignal? Signal { get; init; }
}

public enum RedirectMode
{
    Follow,
    Error,
    Manual
}
