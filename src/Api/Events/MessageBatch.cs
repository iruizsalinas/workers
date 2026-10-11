namespace Workers;

public sealed class MessageBatch<T>
{
    private MessageBatch() { }

    public string Queue => WorkerApi.NotExecutable<string>();
    public IReadOnlyList<Message<T>> Messages => WorkerApi.NotExecutable<IReadOnlyList<Message<T>>>();
    /// <summary>The queue's backlog when the batch was delivered.</summary>
    public MessageBatchMetadata Metadata => WorkerApi.NotExecutable<MessageBatchMetadata>();

    public void AckAll() => WorkerApi.NotExecutable();
    public void RetryAll(QueueRetryOptions? options = null) => WorkerApi.NotExecutable();
}

public sealed class Message<T>
{
    private Message() { }

    public string Id => WorkerApi.NotExecutable<string>();
    public Date Timestamp => WorkerApi.NotExecutable<Date>();
    /// <summary>The message body. Typed reads map JSON properties to C# classes and records.</summary>
    public T Body => WorkerApi.NotExecutable<T>();
    public int Attempts => WorkerApi.NotExecutable<int>();

    public void Ack() => WorkerApi.NotExecutable();
    public void Retry(QueueRetryOptions? options = null) => WorkerApi.NotExecutable();
}

public sealed class MessageBatchMetadata
{
    private MessageBatchMetadata() { }

    public QueueMetrics Metrics => WorkerApi.NotExecutable<QueueMetrics>();
}

public sealed class QueueRetryOptions
{
    public int? DelaySeconds { get; init; }
}
