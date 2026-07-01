using Workers;

namespace QueueConsumer;

public static class Worker
{
    [QueueEvent]
    public static async Task QueueAsync(
        QueueMessageBatch<Job> batch,
        Env environment,
        Context context)
    {
        foreach (var message in batch)
        {
            await environment.Log().LogAsync($"Processing {message.Body.Path}");
            message.Ack();
        }
    }

    public sealed record Job(string Path);
}
