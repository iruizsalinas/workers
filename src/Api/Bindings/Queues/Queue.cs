namespace Workers;

/// <summary>A queue producer binding.</summary>
public sealed class Queue
{
    private Queue() { }

    public Task SendAsync<T>(T body, QueueSendOptions? options = null) => WorkerApi.NotExecutable<Task>();
    public Task SendBatchAsync<T>(IEnumerable<MessageSendRequest<T>> messages, QueueSendBatchOptions? options = null) =>
        WorkerApi.NotExecutable<Task>();
    public Task<QueueMetrics> MetricsAsync() => WorkerApi.NotExecutable<Task<QueueMetrics>>();
}

/// <summary>How a message body is encoded. The runtime default is <see cref="V8"/>, a structured clone.</summary>
public enum QueueContentType
{
    Text,
    Bytes,
    Json,
    V8
}

public sealed class QueueSendOptions
{
    public QueueContentType? ContentType { get; init; }
    public int? DelaySeconds { get; init; }
}

public sealed class QueueSendBatchOptions
{
    public int? DelaySeconds { get; init; }
}

public sealed class MessageSendRequest<T>
{
    public required T Body { get; init; }
    public QueueContentType? ContentType { get; init; }
    public int? DelaySeconds { get; init; }
}

public sealed class QueueMetrics
{
    private QueueMetrics() { }

    public long BacklogCount => WorkerApi.NotExecutable<long>();
    public long BacklogBytes => WorkerApi.NotExecutable<long>();
    public Date? OldestMessageTimestamp => WorkerApi.NotExecutable<Date?>();
}
