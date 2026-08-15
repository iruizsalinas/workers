using Workers;

namespace QueueConsumer;

public static class Worker
{
    [Queue]
    public static async Task QueueAsync(
        QueueMessageBatch<Job> batch,
        Env environment,
        Context context)
    {
        foreach (var message in batch)
        {
            Console.WriteLine($"Processing {message.Body.Path}");
            message.Ack();
        }
    }

    public sealed record Job(string Path);
}
