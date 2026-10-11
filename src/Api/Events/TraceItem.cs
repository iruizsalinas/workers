namespace Workers;

/// <summary>A traced invocation delivered to a <c>tail</c> handler.</summary>
public sealed class TraceItem
{
    private TraceItem() { }

    public string? ScriptName => WorkerApi.NotExecutable<string?>();
    public string? Entrypoint => WorkerApi.NotExecutable<string?>();
    public ScriptVersion? ScriptVersion => WorkerApi.NotExecutable<ScriptVersion?>();
    /// <summary>The dispatch namespace of a Workers for Platforms user Worker.</summary>
    public string? DispatchNamespace => WorkerApi.NotExecutable<string?>();
    public IReadOnlyList<string>? ScriptTags => WorkerApi.NotExecutable<IReadOnlyList<string>?>();
    /// <summary>The ID of the Durable Object that handled the invocation.</summary>
    public string? DurableObjectId => WorkerApi.NotExecutable<string?>();
    public string Outcome => WorkerApi.NotExecutable<string>();
    /// <summary><c>stateless</c> for a Worker, or <c>durableObject</c> or <c>workflow</c>.</summary>
    public string ExecutionModel => WorkerApi.NotExecutable<string>();
    public Date? EventTimestamp => WorkerApi.NotExecutable<Date?>();
    /// <summary>Details of a fetch invocation; null for other event types.</summary>
    public TraceItemFetchEventInfo? Event => WorkerApi.NotExecutable<TraceItemFetchEventInfo?>();
    public IReadOnlyList<TraceLog> Logs => WorkerApi.NotExecutable<IReadOnlyList<TraceLog>>();
    public IReadOnlyList<TraceException> Exceptions => WorkerApi.NotExecutable<IReadOnlyList<TraceException>>();
    public bool Truncated => WorkerApi.NotExecutable<bool>();
    public double CpuTime => WorkerApi.NotExecutable<double>();
    public double WallTime => WorkerApi.NotExecutable<double>();
}

public sealed class ScriptVersion
{
    private ScriptVersion() { }

    public string? Id => WorkerApi.NotExecutable<string?>();
    public string? Tag => WorkerApi.NotExecutable<string?>();
    public string? Message => WorkerApi.NotExecutable<string?>();
}

public sealed class TraceItemFetchEventInfo
{
    private TraceItemFetchEventInfo() { }

    public TraceItemFetchEventInfoRequest Request => WorkerApi.NotExecutable<TraceItemFetchEventInfoRequest>();
    public TraceItemFetchEventInfoResponse? Response => WorkerApi.NotExecutable<TraceItemFetchEventInfoResponse?>();
}

public sealed class TraceItemFetchEventInfoRequest
{
    private TraceItemFetchEventInfoRequest() { }

    public string Url => WorkerApi.NotExecutable<string>();
    public string Method => WorkerApi.NotExecutable<string>();
    /// <summary>The request headers, copied into a <see cref="Workers.Headers"/> object. Sensitive values are redacted
    /// unless the request came from <see cref="GetUnredacted"/>.</summary>
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
    /// <summary>The request's Cloudflare properties, such as <c>colo</c> and <c>country</c>, or null when absent.</summary>
    public JsonValue? Cf => WorkerApi.NotExecutable<JsonValue?>();

    /// <summary>The same request with its URL and headers unredacted. Handle the result as sensitive data.</summary>
    public TraceItemFetchEventInfoRequest GetUnredacted() => WorkerApi.NotExecutable<TraceItemFetchEventInfoRequest>();
}

public sealed class TraceItemFetchEventInfoResponse
{
    private TraceItemFetchEventInfoResponse() { }

    public int Status => WorkerApi.NotExecutable<int>();
}

public sealed class TraceLog
{
    private TraceLog() { }

    public Date Timestamp => WorkerApi.NotExecutable<Date>();
    public string Level => WorkerApi.NotExecutable<string>();
    /// <summary>The logged values, usually an array of the arguments passed to <c>console</c>.</summary>
    public JsonValue Message => WorkerApi.NotExecutable<JsonValue>();
    /// <summary>The errors among the logged values, by argument position; null where an argument is not an error.</summary>
    public IReadOnlyList<TraceLogErrorInfo?>? ErrorInfo => WorkerApi.NotExecutable<IReadOnlyList<TraceLogErrorInfo?>?>();
}

public sealed class TraceLogErrorInfo
{
    private TraceLogErrorInfo() { }

    public string Name => WorkerApi.NotExecutable<string>();
    public string Message => WorkerApi.NotExecutable<string>();
    public string? Stack => WorkerApi.NotExecutable<string?>();
}

public sealed class TraceException
{
    private TraceException() { }

    public Date Timestamp => WorkerApi.NotExecutable<Date>();
    public string Name => WorkerApi.NotExecutable<string>();
    public string Message => WorkerApi.NotExecutable<string>();
    public string? Stack => WorkerApi.NotExecutable<string?>();
}
