using Workers;

namespace EventWrappers;

public static class Worker
{
    [Queue]
    public static async Task ConsumeAsync(MessageBatch<QueuePayload> batch, Env environment, ExecutionContext context)
    {
        foreach (var message in batch.Messages)
        {
            var body = message.Body;
            body.Name += "!";
            await environment.KV("KV").PutAsync("typed-queue-payload", message.Body.Describe());
            message.Ack();
        }
    }

    [Email]
    public static async Task EmailAsync(
        ForwardableEmailMessage message,
        Env environment,
        ExecutionContext context)
    {
        await message.ForwardAsync("archive@example.test");
    }

    [Tail]
    public static async Task TailAsync(
        IReadOnlyList<TraceItem> events,
        Env environment,
        ExecutionContext context)
    {
        await environment.Service("TAIL_SINK").InvokeAsync("record", events);
    }
}

public sealed class QueuePayload
{
    [JsonName("display_name")] public string Name { get; set; } = "none";
    public string Describe() => "payload:" + Name;
}
