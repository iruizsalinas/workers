using Workers;

public sealed record TenantProps(string Tenant);
public sealed record Job(string Path);
public sealed class TenantModel
{
    [JsonName("tenant_id")] public string Tenant { get; init; } = "none";
    public int Count { get; set; }
    public string Format(string name) => $"{Tenant}:{name}";
}

public sealed class TenantGraph
{
    public TenantGraph? Next { get; set; }
    public Dictionary<int, int> Values { get; init; } = new() { [1] = 2 };
}

public sealed class Greeter : WorkerEntrypoint
{
    public bool GraphProps()
    {
        var graph = Ctx.Props<TenantGraph>();
        return graph.Next == graph && graph.Values[1] == 2 && Ctx.Props<TenantGraph>() == graph;
    }
    public int TypedPropsIdentity()
    {
        var props = Ctx.Props<TenantModel>();
        props.Count++;
        return Ctx.Props<TenantModel>().Count;
    }
    public string TypedHello(string name) => Ctx.Props<TenantModel>().Format(name);
    public string Hello(string name)
    {
        var tenant = Ctx.Props<TenantProps?>()?.Tenant ?? "none";
        return $"{tenant}:{name}";
    }
}

public sealed class Counter : DurableObject
{
    public async Task<int> Increment()
    {
        var next = (await Ctx.Storage.GetAsync<int>("count")) + 1;
        await Ctx.Storage.PutAsync("count", next);
        return next;
    }

    public async Task<int> IncrementPeer(string name) =>
        (await Ctx.Exports.DurableObject(nameof(Counter)).GetByName(name).InvokeAsync<int>("increment"));
}

public static class Worker
{
    [Fetch]
    public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
    {
        var path = new Url(request.Url).Pathname;
        switch (path)
        {
            case "/graph-props":
            {
                var graph = new TenantGraph();
                graph.Next = graph;
                return Response.Json(await ctx.Exports.Service(nameof(Greeter), graph).InvokeAsync<bool>("graphProps"));
            }
            case "/typed-props-identity":
                return Response.Json(await ctx.Exports.Service(nameof(Greeter), new { tenant_id = "acme", count = 0 })
                    .InvokeAsync<int>("typedPropsIdentity"));
            case "/typed-props":
                return new Response(await ctx.Exports.Service(nameof(Greeter), new { tenant_id = "acme" })
                    .InvokeAsync<string>("typedHello", "ada"));
            case "/exports":
            {
                var plain = await ctx.Exports.Service(nameof(Greeter)).InvokeAsync<string>("hello", "ada");
                var scoped = await ctx.Exports.Service(nameof(Greeter), new TenantProps("acme")).InvokeAsync<string>("hello", "ada");
                var counter = ctx.Exports.DurableObject(nameof(Counter));
                var first = await counter.GetByName("main").InvokeAsync<int>("increment");
                var peer = await counter.GetByName("main").InvokeAsync<int>("incrementPeer", "peer");
                return Response.Json(new { plain, scoped, first, peer });
            }
            case "/tracing":
            {
                var traced = true;
                var value = await ctx.Tracing.EnterSpan("work", async span =>
                {
                    traced = span.IsTraced;
                    span.SetAttribute("route", path).SetAttribute("attempt", 1).SetAttribute("cached", false);
                    span.SetAttributes(new { region = "test" });
                    await Scheduler.WaitAsync(1);
                    return 42;
                });
                var manual = ctx.Tracing.StartSpan("manual");
                manual.RecordException(new InvalidOperationException("recorded"));
                manual.SetStatus(new TracingSpanStatus { Code = TracingSpanStatusCode.Error, Message = "failed" }).End();
                var sync = ctx.Tracing.EnterSpan("sync", span => span.UpdateName("renamed").IsTraced);
                return Response.Json(new { value, traced, sync, active = ctx.Tracing.GetActiveSpan() is not null });
            }
            case "/queue":
            {
                var sent = await env.Queue("JOBS").SendAsync(new Job("/one"));
                var batch = await env.Queue("JOBS").SendBatchAsync([new MessageSendRequest<Job> { Body = new Job("/two") }]);
                return Response.Json(new
                {
                    sendBacklog = sent.Metadata.Metrics.BacklogCount,
                    batchBacklog = batch.Metadata.Metrics.BacklogCount
                });
            }
            case "/queue-collections":
            {
                var numbers = new Dictionary<int, int> { [1] = 2 };
                var sent = await env.Queue("JOBS").SendAsync(numbers, new QueueSendOptions { ContentType = QueueContentType.V8 });
                var batch = await env.Queue("JOBS").SendBatchAsync([
                    new MessageSendRequest<Dictionary<int, int>> { Body = numbers, ContentType = QueueContentType.V8 }
                ]);
                return Response.Json(new { single = sent.Metadata.Metrics.BacklogCount,
                    batch = batch.Metadata.Metrics.BacklogCount });
            }
            case "/context":
                return Response.Json(new { access = ctx.Access is null, cache = ctx.Cache is null });
            case "/abort":
                ctx.Abort("stopped by test");
                return new Response("unreachable");
            default:
                return new Response("not found", new Response(null) { Status = 404 });
        }
    }
}
