namespace Workers;

/// <summary>Marks the method that handles the Workers fetch event.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class FetchEventAttribute : Attribute
{
    /// <summary>When true, generated glue should turn unhandled exceptions into 500 responses.</summary>
    public bool RespondWithErrors { get; init; }
}

/// <summary>Marks the method that handles the Workers scheduled event.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ScheduledEventAttribute : Attribute
{
}

/// <summary>Marks the method that handles a Workers queue consumer event.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class QueueEventAttribute : Attribute
{
}

/// <summary>Marks the method that handles a Workers inbound email event.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EmailEventAttribute : Attribute
{
}

/// <summary>Marks the method that handles a Workers tail event.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TailEventAttribute : Attribute
{
}

/// <summary>Marks a class as a Durable Object export.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class DurableObjectAttribute : Attribute
{
    /// <summary>Creates a Durable Object export using the CLR type name as the JavaScript class name.</summary>
    public DurableObjectAttribute()
    {
    }

    /// <summary>Creates a Durable Object export using an explicit JavaScript class name.</summary>
    public DurableObjectAttribute(string exportName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportName);
        ExportName = exportName;
    }

    /// <summary>The JavaScript class name exported from the Worker module.</summary>
    public string? ExportName { get; }
}

/// <summary>Marks an RPC interface for generated typed client extensions.</summary>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class RpcClientAttribute : Attribute
{
}

/// <summary>Retry metadata supplied to a Durable Object alarm handler.</summary>
public sealed record AlarmInfo(int RetryCount, bool IsRetry);
