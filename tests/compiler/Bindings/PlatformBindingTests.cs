namespace Workers.Compiler.Tests;

public sealed class PlatformBindingTests
{
    [Fact]
    public void LowersR2QueueCacheAndServiceBindingsByDeclaringType()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var body = await env.R2("FILES").GetAsync("key", new R2GetOptions
                    {
                        Range = new R2Range { Offset = 0, Length = 10 }
                    });
                    await env.Queue("JOBS").SendAsync(new { key = "value" }, new QueueSendOptions { DelaySeconds = 5 });
                    var cached = await CacheStorage.Default.MatchAsync(request, new CacheQueryOptions { IgnoreMethod = true });
                    var upstream = await env.Service("API").FetchAsync("https://example.com");
                    return cached ?? upstream;
                }
            }
            """);

        Assert.Contains("env[\"FILES\"].get(\"key\", { range: { offset: 0, length: 10 } })", module);
        Assert.Contains("env[\"JOBS\"].send({ key: \"value\" }, { delaySeconds: 5 })", module);
        Assert.Contains("caches.default.match(request, { ignoreMethod: true }).then(value => value ?? null)", module);
        Assert.Contains("env[\"API\"].fetch(\"https://example.com\")", module);
    }

    [Fact]
    public void NormalizesNativeCacheMissToCSharpNull()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var cached = await CacheStorage.Default.MatchAsync(request);
                    return cached is not null ? cached : new Response("miss");
                }
            }
            """);

        Assert.Contains("await caches.default.match(request).then(value => value ?? null)", module);
        Assert.Contains("cached != null", module);
    }

    [Fact]
    public void EnumeratesNativeQueueBatchMessagesArray()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record Job(string Path);
            public static class Worker
            {
                [Queue]
                public static async Task Consume(MessageBatch<Job> batch, Env env, ExecutionContext ctx)
                {
                    var count = batch.Messages.Count;
                    var first = batch.Messages[0];
                    foreach (var message in batch.Messages)
                    {
                        message.Retry(new QueueRetryOptions { DelaySeconds = 30 });
                        message.Ack();
                    }
                }
            }
            """);

        Assert.Contains("let count = batch.messages.length", module);
        Assert.Contains("let first = $workers$sequenceIndex(batch.messages, 0)", module);
        Assert.Contains("for (const message of batch.messages)", module);
        Assert.Contains("message.retry({ delaySeconds: 30 })", module);
        Assert.Contains("message.ack();", module);
    }

    [Fact]
    public void LowersRateLimitKeyShapeAndInferredAnonymousMemberName()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var outcome = await env.RateLimit("LIMITER").LimitAsync(new RateLimitOptions { Key = "customer" });
                    return Response.Json(new { outcome.Success });
                }
            }
            """);

        Assert.Contains("env[\"LIMITER\"].limit({ key: \"customer\" })", module);
        Assert.Contains("Response.json({ success: outcome.success })", module);
        Assert.DoesNotContain("outcome.Success:", module);
    }

}
