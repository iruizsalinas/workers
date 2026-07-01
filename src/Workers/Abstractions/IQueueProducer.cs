namespace Workers;

/// <summary>Represents a Workers Queue producer binding.</summary>
public interface IQueueProducer : IBinding
{
    /// <summary>Sends a JSON-serializable message to the queue.</summary>
    Task SendJsonAsync<T>(T message, QueueSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a text message to the queue.</summary>
    Task SendTextAsync(string message, QueueSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a binary message to the queue.</summary>
    Task SendBytesAsync(ReadOnlyMemory<byte> message, QueueSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a batch of JSON-serializable messages to the queue.</summary>
    Task SendJsonBatchAsync<T>(IEnumerable<T> messages, QueueSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a batch of text messages to the queue.</summary>
    Task SendTextBatchAsync(IEnumerable<string> messages, QueueSendOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>Sends a batch of binary messages to the queue.</summary>
    Task SendBytesBatchAsync(
        IEnumerable<ReadOnlyMemory<byte>> messages,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sends a batch of explicitly typed queue message requests.</summary>
    Task SendBatchAsync(
        IEnumerable<QueueSendRequest> messages,
        QueueSendOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reads realtime queue backlog metrics.</summary>
    Task<QueueMetrics> MetricsAsync(CancellationToken cancellationToken = default);
}
