namespace Workers.Compiler.Tests;

public sealed class QueueTests
{
    [Fact]
    public void EmitsTypedQueueMessagesAndBatches()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var queue = env.Queue("EVENTS");
                    await queue.SendAsync(new Message("one"));
                    await queue.SendAsync("line", new QueueSendOptions { ContentType = QueueContentType.Text, DelaySeconds = 5 });
                    await queue.SendBatchAsync([
                        new MessageSendRequest<Message> { Body = new Message("two") },
                        new MessageSendRequest<Message> { Body = new Message("three"), DelaySeconds = 10 }
                    ], new QueueSendBatchOptions { DelaySeconds = 1 });
                    var metrics = await queue.MetricsAsync();
                    return Response.Json(new
                    {
                        metrics.BacklogCount,
                        metrics.BacklogBytes,
                        metrics.OldestMessageTimestamp
                    }, status: 202);
                }
            }
            public sealed record Message(string Id);
            """);

        Assert.Contains("queue.send({ id: \"one\" })", module);
        Assert.Contains("queue.send(\"line\", { contentType: \"text\", delaySeconds: 5 })", module);
        Assert.Contains("queue.sendBatch([{ body: { id: \"two\" } }, { body: { id: \"three\" }, delaySeconds: 10 }], { delaySeconds: 1 })", module);
        Assert.Contains("backlogCount: metrics.backlogCount", module);
        Assert.Contains("oldestMessageTimestamp: metrics.oldestMessageTimestamp", module);
    }
}
