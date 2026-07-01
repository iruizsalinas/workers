using System.Text.Json;

namespace Workers;

/// <summary>Trace data delivered to a Tail Worker invocation.</summary>
public sealed class TailEvent
{
    /// <summary>The event type. Cloudflare currently supplies <c>tail</c>.</summary>
    public string Type { get; init; } = "tail";

    /// <summary>The trace items collected for the producer Worker invocation.</summary>
    public IReadOnlyList<TailItem> Traces { get; init; } = [];
}

/// <summary>One trace item collected from a producer Worker invocation.</summary>
public sealed class TailItem
{
    /// <summary>The name of the producer script.</summary>
    public string ScriptName { get; init; } = "";

    /// <summary>Information about the triggering event, when Cloudflare supplies it.</summary>
    public TailFetchEventInfo? Event { get; init; }

    /// <summary>The producer invocation timestamp.</summary>
    public DateTimeOffset EventTimestamp { get; init; }

    /// <summary>Console log entries emitted by the producer invocation.</summary>
    public IReadOnlyList<TailLog> Logs { get; init; } = [];

    /// <summary>Unhandled exceptions observed during the producer invocation.</summary>
    public IReadOnlyList<TailException> Exceptions { get; init; } = [];

    /// <summary>The invocation outcome string supplied by Cloudflare.</summary>
    public string Outcome { get; init; } = "unknown";
}

/// <summary>Fetch-specific trace data inside a tail item.</summary>
public sealed class TailFetchEventInfo
{
    /// <summary>The triggering request summary.</summary>
    public TailRequest Request { get; init; } = new();

    /// <summary>The response summary.</summary>
    public TailResponse Response { get; init; } = new();
}

/// <summary>A redacted request summary supplied to a Tail Worker.</summary>
public sealed class TailRequest
{
    /// <summary>Cloudflare request metadata, when available.</summary>
    public JsonElement? Cf { get; init; }

    /// <summary>Lowercase request headers, redacted by Cloudflare where applicable.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The HTTP request method.</summary>
    public string Method { get; init; } = "";

    /// <summary>The request URL, redacted by Cloudflare where applicable.</summary>
    public string Url { get; init; } = "";
}

/// <summary>A response summary supplied to a Tail Worker.</summary>
public sealed class TailResponse
{
    /// <summary>The HTTP response status code.</summary>
    public int Status { get; init; }
}

/// <summary>A console log entry supplied to a Tail Worker.</summary>
public sealed class TailLog
{
    /// <summary>The log timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>The console method name supplied by Cloudflare.</summary>
    public string Level { get; init; } = "";

    /// <summary>The logged arguments as JSON values.</summary>
    public IReadOnlyList<JsonElement> Message { get; init; } = [];
}

/// <summary>An unhandled exception entry supplied to a Tail Worker.</summary>
public sealed class TailException
{
    /// <summary>The exception timestamp.</summary>
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>The exception type name.</summary>
    public string Name { get; init; } = "";

    /// <summary>The exception message as supplied by Cloudflare.</summary>
    public JsonElement Message { get; init; }
}
