using Workers;

namespace QueueConsumer;

public static class Worker
{
    [Queue]
    public static async Task QueueAsync(MessageBatch<Job> batch)
    {
        Log.Info($"Batch from {batch.Queue}: {batch.Messages.Count} messages");
        foreach (var message in batch.Messages)
        {
            Log.Info($"Processing {message.Body.Path}");
            message.Ack();
        }
    }

    public sealed record Job(string Path);
}
