namespace Workers;

/// <summary>Represents a Workers scheduled event.</summary>
public sealed class ScheduledEvent
{
    /// <summary>Creates a scheduled event.</summary>
    public ScheduledEvent(string cron, DateTimeOffset scheduledTime)
        : this(cron, scheduledTime, type: "scheduled")
    {
    }

    /// <summary>Creates a scheduled event.</summary>
    public ScheduledEvent(string cron, DateTimeOffset scheduledTime, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cron);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        Cron = cron;
        ScheduledTime = scheduledTime;
        Type = type;
    }

    /// <summary>The cron expression that triggered the event.</summary>
    public string Cron { get; }

    /// <summary>The scheduled event time.</summary>
    public DateTimeOffset ScheduledTime { get; }

    /// <summary>The scheduled controller type.</summary>
    public string Type { get; }

    /// <summary>The scheduled event time in milliseconds since the Unix epoch.</summary>
    public long Schedule => ScheduledTime.ToUnixTimeMilliseconds();
}
