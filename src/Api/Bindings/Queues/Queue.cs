namespace Workers;

public sealed record QueueSendOptions
{
    public int? DelaySeconds { get; init; }
}

public interface IQueueProducer : IBinding
{
    Task SendJsonAsync<T>(T message, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task SendTextAsync(string message, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task SendBytesAsync(ReadOnlyMemory<byte> message, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task SendJsonBatchAsync<T>(IEnumerable<T> messages, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task SendTextBatchAsync(IEnumerable<string> messages, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task SendBytesBatchAsync(IEnumerable<ReadOnlyMemory<byte>> messages, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task SendBatchAsync(IEnumerable<QueueSendRequest> messages, QueueSendOptions? options = null, CancellationToken cancellationToken = default);
    Task<QueueMetrics> MetricsAsync(CancellationToken cancellationToken = default);
}

public enum QueueContentType
{
    Text,
    Bytes,
    Json,
    V8
}

public sealed class QueueSendRequest
{
    public static QueueSendRequest Json<T>(T body, int? delaySeconds = null) => WorkerApi.NotExecutable<QueueSendRequest>();
    public static QueueSendRequest Text(string body, int? delaySeconds = null) => WorkerApi.NotExecutable<QueueSendRequest>();
    public static QueueSendRequest Bytes(ReadOnlyMemory<byte> body, int? delaySeconds = null) => WorkerApi.NotExecutable<QueueSendRequest>();
}

public sealed record QueueMetrics(int? BacklogCount, int? BacklogBytes, int? OldestMessageAgeSeconds);
