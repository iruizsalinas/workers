using System.Collections;

namespace Workers;

/// <summary>Options used when retrying queue messages.</summary>
public sealed record QueueRetryOptions
{
    /// <summary>The number of seconds to delay the message before it can be delivered again.</summary>
    public int? DelaySeconds { get; init; }
}

/// <summary>A queue message delivered to a Worker consumer.</summary>
public sealed class QueueMessage<T>
{
    private readonly int? _index;

    /// <summary>Creates a queue message.</summary>
    public QueueMessage(string id, T body, DateTimeOffset timestamp)
        : this(id, body, timestamp, attempts: 1, index: null)
    {
    }

    internal QueueMessage(string id, T body, DateTimeOffset timestamp, int attempts, int? index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (attempts < 1)
            throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Queue message attempts must be at least 1.");

        Id = id;
        Body = body;
        Timestamp = timestamp;
        Attempts = attempts;
        _index = index;
    }

    /// <summary>The platform message identifier.</summary>
    public string Id { get; }

    /// <summary>The deserialized message body.</summary>
    public T Body { get; }

    /// <summary>The message timestamp.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>The number of processing attempts for this message, starting at 1.</summary>
    public int Attempts { get; }

    /// <summary>Marks the message as acknowledged.</summary>
    public void Ack()
    {
        Acked = true;
        Retried = false;
        RetryOptions = null;
    }

    /// <summary>Marks the message for retry.</summary>
    public void Retry(QueueRetryOptions? options = null)
    {
        Retried = true;
        Acked = false;
        RetryOptions = options;
    }

    /// <summary>True when <see cref="Ack"/> has been called.</summary>
    public bool Acked { get; private set; }

    /// <summary>True when <see cref="Retry"/> has been called.</summary>
    public bool Retried { get; private set; }

    /// <summary>The retry options supplied to <see cref="Retry"/>, when any.</summary>
    public QueueRetryOptions? RetryOptions { get; private set; }

    internal QueueMessageDisposition? ToDisposition()
    {
        if (_index is null)
            return null;

        if (Acked)
            return new QueueMessageDisposition(_index.Value, QueueMessageDispositionKind.Ack, null);

        return Retried
            ? new QueueMessageDisposition(_index.Value, QueueMessageDispositionKind.Retry, RetryOptions)
            : null;
    }
}

/// <summary>A batch of queue messages delivered to a Worker consumer.</summary>
public sealed class QueueMessageBatch<T> : IQueueMessageBatch, IReadOnlyList<QueueMessage<T>>
{
    /// <summary>Creates a queue message batch.</summary>
    public QueueMessageBatch(IReadOnlyList<QueueMessage<T>> messages)
        : this("", messages)
    {
    }

    /// <summary>Creates a queue message batch.</summary>
    public QueueMessageBatch(string queue, IReadOnlyList<QueueMessage<T>> messages)
    {
        Queue = queue ?? throw new ArgumentNullException(nameof(queue));
        Messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    /// <summary>The queue name for this batch.</summary>
    public string Queue { get; }

    /// <summary>The messages in the batch.</summary>
    public IReadOnlyList<QueueMessage<T>> Messages { get; }

    /// <summary>The number of messages in the batch.</summary>
    public int Count => Messages.Count;

    /// <summary>Gets a message by index.</summary>
    public QueueMessage<T> this[int index] => Messages[index];

    /// <summary>Acknowledges all messages in the batch.</summary>
    public void AckAll()
    {
        foreach (var message in Messages)
            message.Ack();
    }

    /// <summary>Retries all messages in the batch.</summary>
    public void RetryAll(QueueRetryOptions? options = null)
    {
        foreach (var message in Messages)
            message.Retry(options);
    }

    /// <inheritdoc />
    public IEnumerator<QueueMessage<T>> GetEnumerator() => Messages.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    IReadOnlyList<QueueMessageDisposition> IQueueMessageBatch.Dispositions() =>
        Messages.Select(static message => message.ToDisposition()).OfType<QueueMessageDisposition>().ToArray();
}

internal interface IQueueMessageBatch
{
    IReadOnlyList<QueueMessageDisposition> Dispositions();
}

internal enum QueueMessageDispositionKind
{
    Ack,
    Retry
}

internal sealed record QueueMessageDisposition(
    int Index,
    QueueMessageDispositionKind Kind,
    QueueRetryOptions? Options);
