namespace Workers;

public sealed record QueueSendOptions
{
    public int? DelaySeconds { get; init; }
}

public interface IQueueProducer : IBinding
{
    Task SendJsonAsync<T>(T message, QueueSendOptions? options = null);
    Task SendTextAsync(string message, QueueSendOptions? options = null);
    Task SendBytesAsync(ReadOnlyMemory<byte> message, QueueSendOptions? options = null);
    Task SendJsonBatchAsync<T>(IEnumerable<T> messages, QueueSendOptions? options = null);
    Task SendTextBatchAsync(IEnumerable<string> messages, QueueSendOptions? options = null);
    Task SendBytesBatchAsync(IEnumerable<ReadOnlyMemory<byte>> messages, QueueSendOptions? options = null);
    Task SendBatchAsync(IEnumerable<QueueSendRequest> messages, QueueSendOptions? options = null);
    Task<QueueMetrics> MetricsAsync();
}

public sealed class QueueSendRequest
{
    public static QueueSendRequest Json<T>(T body, int? delaySeconds = null) => WorkerApi.NotExecutable<QueueSendRequest>();
    public static QueueSendRequest Text(string body, int? delaySeconds = null) => WorkerApi.NotExecutable<QueueSendRequest>();
    public static QueueSendRequest Bytes(ReadOnlyMemory<byte> body, int? delaySeconds = null) => WorkerApi.NotExecutable<QueueSendRequest>();
}

public sealed record QueueMetrics(int BacklogCount, int BacklogBytes, Date? OldestMessageTimestamp);
