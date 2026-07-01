namespace Workers;

/// <summary>CORS response-header configuration.</summary>
public sealed class Cors
{
    /// <summary>An empty CORS configuration.</summary>
    public static Cors Empty { get; } = new();

    private readonly IReadOnlyList<string> _origins;
    private readonly IReadOnlyList<string> _methods;
    private readonly IReadOnlyList<string> _allowedHeaders;
    private readonly IReadOnlyList<string> _exposedHeaders;

    /// <summary>Creates an empty CORS configuration.</summary>
    public Cors()
        : this(
            allowCredentials: false,
            maxAgeSeconds: null,
            origins: [],
            methods: [],
            allowedHeaders: [],
            exposedHeaders: [])
    {
    }

    private Cors(
        bool allowCredentials,
        int? maxAgeSeconds,
        IReadOnlyList<string> origins,
        IReadOnlyList<string> methods,
        IReadOnlyList<string> allowedHeaders,
        IReadOnlyList<string> exposedHeaders)
    {
        AllowCredentials = allowCredentials;
        MaxAgeSeconds = maxAgeSeconds;
        _origins = origins;
        _methods = methods;
        _allowedHeaders = allowedHeaders;
        _exposedHeaders = exposedHeaders;
    }

    /// <summary>True when credentialed requests are allowed.</summary>
    public bool AllowCredentials { get; }

    /// <summary>The preflight cache lifetime in seconds, if configured.</summary>
    public int? MaxAgeSeconds { get; }

    /// <summary>Allowed origins.</summary>
    public IReadOnlyList<string> Origins => _origins;

    /// <summary>Allowed methods.</summary>
    public IReadOnlyList<string> Methods => _methods;

    /// <summary>Allowed request headers.</summary>
    public IReadOnlyList<string> AllowedHeaders => _allowedHeaders;

    /// <summary>Response headers exposed to clients.</summary>
    public IReadOnlyList<string> ExposedHeaders => _exposedHeaders;

    /// <summary>Configures whether browsers may include credentials.</summary>
    public Cors WithCredentials(bool allowCredentials = true) =>
        new(allowCredentials, MaxAgeSeconds, _origins, _methods, _allowedHeaders, _exposedHeaders);

    /// <summary>Configures how long browsers may cache a preflight response.</summary>
    public Cors WithMaxAge(int seconds)
    {
        if (seconds < 0)
            throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "CORS max-age must be non-negative.");

        return new Cors(AllowCredentials, seconds, _origins, _methods, _allowedHeaders, _exposedHeaders);
    }

    /// <summary>Configures which origins are allowed.</summary>
    public Cors WithOrigins(IEnumerable<string> origins) =>
        new(AllowCredentials, MaxAgeSeconds, Normalize(origins, nameof(origins)), _methods, _allowedHeaders, _exposedHeaders);

    /// <summary>Configures which methods are allowed.</summary>
    public Cors WithMethods(IEnumerable<string> methods) =>
        new(AllowCredentials, MaxAgeSeconds, _origins, Normalize(methods, nameof(methods)), _allowedHeaders, _exposedHeaders);

    /// <summary>Configures which request headers are allowed.</summary>
    public Cors WithAllowedHeaders(IEnumerable<string> headers) =>
        new(AllowCredentials, MaxAgeSeconds, _origins, _methods, Normalize(headers, nameof(headers)), _exposedHeaders);

    /// <summary>Configures which response headers are exposed to clients.</summary>
    public Cors WithExposedHeaders(IEnumerable<string> headers) =>
        new(AllowCredentials, MaxAgeSeconds, _origins, _methods, _allowedHeaders, Normalize(headers, nameof(headers)));

    /// <summary>Applies configured CORS headers to a response.</summary>
    public Response ApplyTo(Response response)
    {
        ArgumentNullException.ThrowIfNull(response);
        ApplyTo(response.Headers);
        return response;
    }

    /// <summary>Applies configured CORS headers to a header collection.</summary>
    public Headers ApplyTo(Headers headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (AllowCredentials)
            headers.Set("access-control-allow-credentials", "true");

        if (MaxAgeSeconds is { } maxAge)
            headers.Set("access-control-max-age", maxAge.ToString(System.Globalization.CultureInfo.InvariantCulture));

        SetJoined(headers, "access-control-allow-origin", _origins);
        SetJoined(headers, "access-control-allow-methods", _methods);
        SetJoined(headers, "access-control-allow-headers", _allowedHeaders);
        SetJoined(headers, "access-control-expose-headers", _exposedHeaders);

        return headers;
    }

    private static void SetJoined(Headers headers, string name, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
            headers.Set(name, string.Join(",", values));
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);

        var normalized = values.Select(value =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
            return value;
        }).ToArray();

        return Array.AsReadOnly(normalized);
    }
}
