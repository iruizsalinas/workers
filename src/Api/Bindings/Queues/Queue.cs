namespace Workers;

/// <summary>A queue producer binding.</summary>
public sealed class Queue
{
    private Queue() { }

    /// <summary>Sends a message and returns the queue's backlog after it was accepted.</summary>
    public Task<QueueSendResponse> SendAsync<T>(T body, QueueSendOptions? options = null) => WorkerApi.NotExecutable<Task<QueueSendResponse>>();
    /// <summary>Sends up to 100 messages and returns the queue's backlog after they were accepted.</summary>
    public Task<QueueSendBatchResponse> SendBatchAsync<T>(IEnumerable<MessageSendRequest<T>> messages, QueueSendBatchOptions? options = null) =>
        WorkerApi.NotExecutable<Task<QueueSendBatchResponse>>();
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

public sealed class QueueSendResponse
{
    private QueueSendResponse() { }

    public QueueSendMetadata Metadata => WorkerApi.NotExecutable<QueueSendMetadata>();
}

public sealed class QueueSendBatchResponse
{
    private QueueSendBatchResponse() { }

    public QueueSendMetadata Metadata => WorkerApi.NotExecutable<QueueSendMetadata>();
}

public sealed class QueueSendMetadata
{
    private QueueSendMetadata() { }

    public QueueMetrics Metrics => WorkerApi.NotExecutable<QueueMetrics>();
}

/// <summary>A queue's backlog.</summary>
public sealed class QueueMetrics
{
    private QueueMetrics() { }

    public long BacklogCount => WorkerApi.NotExecutable<long>();
    public long BacklogBytes => WorkerApi.NotExecutable<long>();
    /// <summary>When the oldest unacknowledged message was sent, or null when the queue is empty.</summary>
    public Date? OldestMessageTimestamp => WorkerApi.NotExecutable<Date?>();
}
