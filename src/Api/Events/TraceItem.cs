namespace Workers;

/// <summary>A traced invocation delivered to a <c>tail</c> handler.</summary>
public sealed class TraceItem
{
    private TraceItem() { }

    public string? ScriptName => WorkerApi.NotExecutable<string?>();
    public string? Entrypoint => WorkerApi.NotExecutable<string?>();
    public string Outcome => WorkerApi.NotExecutable<string>();
    public Date? EventTimestamp => WorkerApi.NotExecutable<Date?>();
    /// <summary>Details of a fetch invocation; null for other event types.</summary>
    public TraceItemFetchEventInfo? Event => WorkerApi.NotExecutable<TraceItemFetchEventInfo?>();
    public IReadOnlyList<TraceLog> Logs => WorkerApi.NotExecutable<IReadOnlyList<TraceLog>>();
    public IReadOnlyList<TraceException> Exceptions => WorkerApi.NotExecutable<IReadOnlyList<TraceException>>();
    public bool Truncated => WorkerApi.NotExecutable<bool>();
    public double CpuTime => WorkerApi.NotExecutable<double>();
    public double WallTime => WorkerApi.NotExecutable<double>();
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
    /// <summary>The request headers, copied into a <see cref="Workers.Headers"/> object.</summary>
    public Headers Headers => WorkerApi.NotExecutable<Headers>();
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
}

public sealed class TraceException
{
    private TraceException() { }

    public Date Timestamp => WorkerApi.NotExecutable<Date>();
    public string Name => WorkerApi.NotExecutable<string>();
    public string Message => WorkerApi.NotExecutable<string>();
    public string? Stack => WorkerApi.NotExecutable<string?>();
}
