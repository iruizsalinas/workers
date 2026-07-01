using System.Text.Json;
using System.Text.Json.Serialization;
using Workers.Interop;

namespace Workers;

internal sealed record FetchBindingRequest(RequestEnvelope Request, FetchBindingOptions? Options)
{
    public static FetchBindingRequest From(Request request, FetchOptions? options)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new FetchBindingRequest(
            RequestEnvelope.FromRequest(request),
            FetchBindingOptions.From(options));
    }
}

internal sealed record FetchBindingOptions(
    string? SignalHandle,
    string? Mode,
    string? Credentials,
    string? Referrer,
    string? ReferrerPolicy,
    string? Redirect,
    string? Cache,
    string? Integrity,
    bool? KeepAlive,
    FetchCfOptionsEnvelope? Cf)
{
    public static FetchBindingOptions? From(FetchOptions? options)
    {
        if (options is null)
            return null;

        ValidateCf(options.Cf);
        ValidateCacheMode(options);
        ValidateMode(options);
        ValidateReferrer(options.Referrer);
        var envelope = new FetchBindingOptions(
            options.Signal?.Handle,
            ModeName(options.Mode),
            CredentialsName(options.Credentials),
            options.Referrer,
            ReferrerPolicyName(options.ReferrerPolicy),
            RedirectName(options.Redirect),
            CacheName(options.Cache),
            options.Integrity,
            options.KeepAlive,
            FetchCfOptionsEnvelope.From(options.Cf));

        return envelope.SignalHandle is null
            && envelope.Mode is null
            && envelope.Credentials is null
            && envelope.Referrer is null
            && envelope.ReferrerPolicy is null
            && envelope.Redirect is null
            && envelope.Cache is null
            && envelope.Integrity is null
            && envelope.KeepAlive is null
            && envelope.Cf is null
            ? null
            : envelope;
    }

    private static string? ModeName(RequestMode? mode) =>
        mode switch
        {
            null => null,
            RequestMode.Cors => "cors",
            RequestMode.SameOrigin => "same-origin",
            RequestMode.NoCors => "no-cors",
            RequestMode.Navigate => "navigate",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported request mode.")
        };

    private static string? CredentialsName(RequestCredentials? credentials) =>
        credentials switch
        {
            null => null,
            RequestCredentials.Omit => "omit",
            RequestCredentials.SameOrigin => "same-origin",
            RequestCredentials.Include => "include",
            _ => throw new ArgumentOutOfRangeException(nameof(credentials), credentials, "Unsupported credentials mode.")
        };

    private static string? ReferrerPolicyName(ReferrerPolicy? policy) =>
        policy switch
        {
            null => null,
            global::Workers.ReferrerPolicy.NoReferrer => "no-referrer",
            global::Workers.ReferrerPolicy.NoReferrerWhenDowngrade => "no-referrer-when-downgrade",
            global::Workers.ReferrerPolicy.Origin => "origin",
            global::Workers.ReferrerPolicy.OriginWhenCrossOrigin => "origin-when-cross-origin",
            global::Workers.ReferrerPolicy.SameOrigin => "same-origin",
            global::Workers.ReferrerPolicy.StrictOrigin => "strict-origin",
            global::Workers.ReferrerPolicy.StrictOriginWhenCrossOrigin => "strict-origin-when-cross-origin",
            global::Workers.ReferrerPolicy.UnsafeUrl => "unsafe-url",
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported referrer policy.")
        };

    private static string? RedirectName(RequestRedirect? redirect) =>
        redirect switch
        {
            null => null,
            RequestRedirect.Follow => "follow",
            RequestRedirect.Error => "error",
            RequestRedirect.Manual => "manual",
            _ => throw new ArgumentOutOfRangeException(nameof(redirect), redirect, "Unsupported redirect mode.")
        };

    private static string? CacheName(RequestCache? cache) =>
        cache switch
        {
            null => null,
            RequestCache.NoStore => "no-store",
            RequestCache.NoCache => "no-cache",
            RequestCache.Reload => "reload",
            RequestCache.ForceCache => "force-cache",
            RequestCache.OnlyIfCached => "only-if-cached",
            _ => throw new ArgumentOutOfRangeException(nameof(cache), cache, "Unsupported cache mode.")
        };

    private static void ValidateCacheMode(FetchOptions options)
    {
        if (options.Cache == RequestCache.OnlyIfCached && options.Mode != RequestMode.SameOrigin)
            throw new ArgumentException("The only-if-cached cache mode requires same-origin request mode.", nameof(options));
    }

    private static void ValidateMode(FetchOptions options)
    {
        if (options.Mode == RequestMode.Navigate)
            throw new ArgumentException("The navigate request mode is not supported by Worker fetch requests.", nameof(options));
    }

    private static void ValidateReferrer(string? referrer)
    {
        if (referrer is null || referrer.Length == 0 || string.Equals(referrer, "about:client", StringComparison.Ordinal))
            return;

        if (!Uri.TryCreate(referrer, UriKind.Absolute, out _))
            throw new ArgumentException("Fetch referrer must be an absolute URI, an empty string, or 'about:client'.", nameof(referrer));
    }

    private static void ValidateCf(FetchCfOptions? options)
    {
        if (options?.CacheTtl is < 0)
            throw new ArgumentOutOfRangeException(nameof(options), options.CacheTtl, "Cloudflare fetch cache TTL cannot be negative.");
    }
}

internal sealed record FetchCfOptionsEnvelope(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Apps,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? CacheEverything,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? CacheKey,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CacheTtl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, int>? CacheTtlByStatus,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FetchImageOptionsEnvelope? Image,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    FetchMinifyOptions? Minify,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Mirage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Polish,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ResolveOverride,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? ScrapeShield)
{
    public static FetchCfOptionsEnvelope? From(FetchCfOptions? options)
    {
        if (options is null)
            return null;

        return new FetchCfOptionsEnvelope(
            options.Apps,
            options.CacheEverything,
            options.CacheKey,
            options.CacheTtl,
            options.CacheTtlByStatus,
            FetchImageOptionsEnvelope.From(options.Image),
            options.Minify,
            options.Mirage,
            PolishName(options.Polish),
            options.ResolveOverride,
            options.ScrapeShield);
    }

    private static string? PolishName(FetchPolish? polish) =>
        polish switch
        {
            null => null,
            FetchPolish.Off => "off",
            FetchPolish.Lossy => "lossy",
            FetchPolish.Lossless => "lossless",
            _ => throw new ArgumentOutOfRangeException(nameof(polish), polish, "Unsupported Polish mode.")
        };
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(FetchBindingRequest))]
[JsonSerializable(typeof(Workers.Interop.ResponseEnvelope))]
[JsonSerializable(typeof(FetchImageGravityCoordinates))]
internal sealed partial class FetchBindingJsonContext : JsonSerializerContext;
