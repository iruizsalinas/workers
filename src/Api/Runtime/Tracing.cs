namespace Workers;

/// <summary>
/// Custom trace spans, as <c>ctx.tracing</c>. Spans are recorded when tracing is enabled for the Worker;
/// otherwise they are no-ops, so instrumentation can stay in place.
/// </summary>
public sealed class Tracing
{
    private Tracing() { }

    /// <summary>Runs the callback in a new child span that is active for its duration and ends when it returns.
    /// When the callback returns a task, the span ends when the task completes.</summary>
    public T EnterSpan<T>(string name, Func<Span, T> callback) => WorkerApi.NotExecutable<T>();
    public void EnterSpan(string name, Action<Span> callback) => WorkerApi.NotExecutable();
    /// <summary>Runs the callback in a new active child span that stays open until <see cref="Span.End"/> is called.</summary>
    public T StartActiveSpan<T>(string name, Func<Span, T> callback) => WorkerApi.NotExecutable<T>();
    public void StartActiveSpan(string name, Action<Span> callback) => WorkerApi.NotExecutable();
    /// <summary>Starts a child span without making it active. Call <see cref="Span.End"/> when the operation finishes.</summary>
    public Span StartSpan(string name) => WorkerApi.NotExecutable<Span>();
    /// <summary>The active span, or null outside an invocation.</summary>
    public Span? GetActiveSpan() => WorkerApi.NotExecutable<Span?>();
}

/// <summary>A trace span. Names are truncated to 64 bytes.</summary>
public sealed class Span
{
    private Span() { }

    /// <summary>Whether the span is recorded; false when tracing is disabled.</summary>
    public bool IsTraced => WorkerApi.NotExecutable<bool>();

    public Span SetAttribute(string key, string value) => WorkerApi.NotExecutable<Span>();
    public Span SetAttribute(string key, double value) => WorkerApi.NotExecutable<Span>();
    public Span SetAttribute(string key, bool value) => WorkerApi.NotExecutable<Span>();
    /// <summary>Sets attributes from the properties of an object such as <c>new { route = "/users", cached = true }</c>.</summary>
    public Span SetAttributes(object attributes) => WorkerApi.NotExecutable<Span>();
    public void RecordException(string exception) => WorkerApi.NotExecutable();
    /// <summary>Records an exception's name, message and stack.</summary>
    public void RecordException(Exception exception) => WorkerApi.NotExecutable();
    public Span UpdateName(string name) => WorkerApi.NotExecutable<Span>();
    public Span SetStatus(TracingSpanStatus status) => WorkerApi.NotExecutable<Span>();
    public void End() => WorkerApi.NotExecutable();
}

public sealed class TracingSpanStatus
{
    public required TracingSpanStatusCode Code { get; init; }
    /// <summary>A developer-facing message; only used with <see cref="TracingSpanStatusCode.Error"/>.</summary>
    public string? Message { get; init; }
}

public enum TracingSpanStatusCode
{
    Unset,
    Ok,
    Error
}
