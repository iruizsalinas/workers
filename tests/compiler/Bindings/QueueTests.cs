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
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var queue = env.Queue("EVENTS");
                    await queue.SendJsonAsync(new { id = "one" });
                    await queue.SendTextAsync("line", new QueueSendOptions { DelaySeconds = 5 });
                    byte[] bytes = [1, 2];
                    await queue.SendBytesAsync(bytes);
                    var batch = new List<Message> { new Message("batch") };
                    await queue.SendJsonBatchAsync(batch);
                    await queue.SendBatchAsync([
                        QueueSendRequest.Json(new { id = "two" }),
                        QueueSendRequest.Text("later", 10)
                    ]);
                    var metrics = await queue.MetricsAsync();
                    return Response.Json(new
                    {
                        metrics.BacklogCount,
                        metrics.BacklogBytes,
                        metrics.OldestMessageTimestamp
                    }, 202);
                }
            }
            public sealed record Message(string Id);
            """);

        Assert.Contains("queue.send({ id: \"one\" }, { contentType: \"json\" })", module);
        Assert.Contains("queue.send(\"line\", { ...({ delaySeconds: 5 } ?? {}), contentType: \"text\" })", module);
        Assert.Contains("queue.send(bytes, { contentType: \"bytes\" })", module);
        Assert.Contains("queue.sendBatch(Array.from", module);
        Assert.Contains("contentType: \"json\"", module);
        Assert.Contains("{ body: { id: \"two\" }, contentType: \"json\" }", module);
        Assert.Contains("{ body: \"later\", contentType: \"text\", delaySeconds: 10 ?? undefined }", module);
        Assert.Contains("backlogCount: metrics.backlogCount", module);
        Assert.Contains("oldestMessageTimestamp: metrics.oldestMessageTimestamp", module);
    }
}
