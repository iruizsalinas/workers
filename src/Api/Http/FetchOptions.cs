namespace Workers;

public sealed class FetchOptions
{
    public string? Method { get; init; }
    public Headers? Headers { get; init; }
    public Body? Body { get; init; }
    public object? Cf { get; init; }
    public AbortSignal? Signal { get; init; }
    public RequestRedirect? Redirect { get; init; }
}

public readonly record struct RequestPriority(int Weight, bool Exclusive, int Group, int GroupWeight);
public enum RequestMode
{
    Cors,
    NoCors,
    SameOrigin,
    Navigate
}

public enum RequestCredentials
{
    Omit,
    SameOrigin,
    Include
}

public enum ReferrerPolicy
{
    Empty,
    NoReferrer,
    NoReferrerWhenDowngrade,
    Origin,
    OriginWhenCrossOrigin,
    SameOrigin,
    StrictOrigin,
    StrictOriginWhenCrossOrigin,
    UnsafeUrl
}

public enum RequestRedirect
{
    Follow,
    Error,
    Manual
}

public enum RequestCache
{
    Default,
    NoStore,
    Reload,
    NoCache,
    ForceCache,
    OnlyIfCached
}

public enum FetchPolish
{
    Off,
    Lossless,
    Lossy,
    Webp
}

public sealed class FetchCfOptions
{
    public object? CacheTtl { get; init; }
    public FetchImageOptions? Image { get; init; }
}

public sealed class FetchMinifyOptions
{
    public bool? JavaScript { get; init; }
    public bool? Css { get; init; }
    public bool? Html { get; init; }
}
