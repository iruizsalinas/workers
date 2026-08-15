namespace Workers;

public sealed record QueueRetryOptions
{
    public int? DelaySeconds { get; init; }
}

public sealed class QueueMessage<T>
{
    public string Id => WorkerApi.NotExecutable<string>();
    public T Body => WorkerApi.NotExecutable<T>();
    public DateTimeOffset Timestamp => WorkerApi.NotExecutable<DateTimeOffset>();
    public int Attempts => WorkerApi.NotExecutable<int>();

    public void Ack() => WorkerApi.NotExecutable();
    public void Retry(QueueRetryOptions? options = null) => WorkerApi.NotExecutable();
}

public sealed class QueueMessageBatch<T> : IReadOnlyList<QueueMessage<T>>
{
    public string Queue => WorkerApi.NotExecutable<string>();
    public int Count => WorkerApi.NotExecutable<int>();

    public QueueMessage<T> this[int index] => WorkerApi.NotExecutable<QueueMessage<T>>();

    public void AckAll() => WorkerApi.NotExecutable();
    public void RetryAll(QueueRetryOptions? options = null) => WorkerApi.NotExecutable();
    public IEnumerator<QueueMessage<T>> GetEnumerator() =>
        WorkerApi.NotExecutable<IEnumerator<QueueMessage<T>>>();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
