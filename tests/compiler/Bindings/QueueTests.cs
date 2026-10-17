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

        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$queueSend($workers$notNull($workers$receiver), $workers$arg1))(queue, Object.defineProperty({ id: \"one\" }, $workers$recordType, { value: \"Message\" }))", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2, $workers$arg2) => $workers$queueSend($workers$notNull($workers$receiver$2), $workers$arg1$2, $workers$arg2))(queue, \"line\", { contentType: \"text\", delaySeconds: 5 })", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3, $workers$arg2$2) => $workers$queueSendBatch($workers$notNull($workers$receiver$3), $workers$arg1$3, $workers$arg2$2))(queue, [{ body: Object.defineProperty({ id: \"two\" }, $workers$recordType, { value: \"Message\" }) }, { body: Object.defineProperty({ id: \"three\" }, $workers$recordType, { value: \"Message\" }), delaySeconds: 10 }], { delaySeconds: 1 })", module);
        Assert.Contains("{ id: \"three\" }", module);
        Assert.Contains("delaySeconds: 10 }], { delaySeconds: 1 })", module);
        Assert.Contains("(($workers$arg1$4, $workers$arg2$3) => Response.json($workers$arg1$4, { status: $workers$arg2$3 }))({ backlogCount: $workers$notNull(metrics).backlogCount, backlogBytes: $workers$notNull(metrics).backlogBytes, oldestMessageTimestamp: $workers$notNull(metrics).oldestMessageTimestamp }, 202)", module);
        Assert.Contains("(($workers$arg1$4, $workers$arg2$3) => Response.json($workers$arg1$4, { status: $workers$arg2$3 }))({ backlogCount: $workers$notNull(metrics).backlogCount, backlogBytes: $workers$notNull(metrics).backlogBytes, oldestMessageTimestamp: $workers$notNull(metrics).oldestMessageTimestamp }, 202)", module);
    }
}
