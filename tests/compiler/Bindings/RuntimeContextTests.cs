namespace Workers.Compiler.Tests;

public sealed class RuntimeContextTests
{
    [Fact]
    public void LowersLoopbackExportsByNameAndSpecializesThemWithProps()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Greeter : WorkerEntrypoint
            {
                public string Hello(string name) => "Hello " + name;
            }
            public sealed class Counter : DurableObject
            {
                public int Next() => 1;
            }
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var plain = ctx.Exports.Service(nameof(Greeter));
                    var scoped = ctx.Exports.Service(nameof(Greeter), new { tenant = "acme" });
                    var counter = ctx.Exports.DurableObject(nameof(Counter)).GetByName("main");
                    var flow = ctx.Exports.Workflow("Pipeline");
                    var greeting = await scoped.InvokeAsync<string>("hello", "world");
                    return new Response(greeting);
                }
            }
            """);

        Assert.Contains("let plain = $workers$notNull($workers$notNull(ctx).exports)[\"Greeter\"];", module);
        Assert.Contains("let scoped = (($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver)[$workers$arg1]({ props: $workers$rpcValue($workers$arg2) }))($workers$notNull(ctx).exports, \"Greeter\", { tenant: \"acme\" });", module);
        Assert.Contains("let counter = $workers$notNull($workers$notNull($workers$notNull(ctx).exports)[\"Counter\"]).getByName(\"main\");", module);
        Assert.Contains("let flow = $workers$notNull($workers$notNull(ctx).exports)[\"Pipeline\"];", module);
    }

    [Fact]
    public void LowersDurableObjectExportsAndAbortOptions()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Room : DurableObject
            {
                public override async Task AlarmAsync(AlarmInvocationInfo alarmInfo)
                {
                    var peer = Ctx.Exports.DurableObject(nameof(Room)).GetByName("peer");
                    Ctx.Abort("reset", new DurableObjectAbortOptions { RetryAlarm = false });
                }
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    ctx.Abort("stop");
                    return new Response("unreachable");
                }
            }
            """);

        Assert.Contains("$workers$notNull($workers$notNull($workers$notNull(this.ctx).exports)[\"Room\"]).getByName(\"peer\")", module);
        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver).abort($workers$arg1, $workers$arg2))(this.ctx, \"reset\", { retryAlarm: false })", module);
        Assert.Contains("$workers$notNull(ctx).abort(\"stop\")", module);
    }

    [Fact]
    public void LowersCachePurgeAndAccessIdentity()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    if (ctx.Cache is { } cache)
                    {
                        var purged = await cache.PurgeAsync(new CachePurgeOptions { Tags = ["product-1"] });
                        await cache.InvalidateAsync(new CachePurgeOptions { PathPrefixes = ["/images/"] });
                        if (!purged.Success) return new Response(purged.Errors[0].Message, new Response(null) { Status = 502 });
                    }
                    var identity = ctx.Access is null ? null : await ctx.Access.GetIdentityAsync();
                    var user = identity?.UserUuid;
                    var warp = identity?.IsWarp == true;
                    var claim = identity is null ? null : identity["department"];
                    return new Response(ctx.Access?.Aud ?? "anonymous");
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).purge($workers$arg1))(cache, { tags: [\"product-1\"] })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).invalidate($workers$arg1$2))(cache, { pathPrefixes: [\"/images/\"] })", module);
        Assert.Contains(".getIdentity()", module);
        Assert.Contains(".user_uuid", module);
        Assert.Contains(".is_warp", module);
        Assert.Contains("[\"department\"]", module);
        Assert.Contains(".aud", module);
    }

    [Fact]
    public void LowersTracingSpansToTheNativeTracingApi()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var body = await ctx.Tracing.EnterSpan("load", async span =>
                    {
                        span.SetAttribute("cache.hit", false).SetAttribute("attempt", 2);
                        span.SetAttributes(new { route = "/users" });
                        return await Http.FetchAsync("https://example.com");
                    });
                    var manual = ctx.Tracing.StartSpan("render");
                    try { manual.UpdateName("render.html"); }
                    catch (System.Exception error) { manual.RecordException(error); }
                    finally
                    {
                        manual.SetStatus(new TracingSpanStatus { Code = TracingSpanStatusCode.Ok });
                        manual.End();
                    }
                    var traced = ctx.Tracing.GetActiveSpan()?.IsTraced ?? false;
                    return body;
                }
            }
            """);

        Assert.Contains("await $workers$notNull($workers$notNull(ctx).tracing).enterSpan(\"load\", async span =>", module);
        Assert.Contains("$workers$notNull($workers$notNull(span).setAttribute(\"cache.hit\", false)).setAttribute(\"attempt\", 2)", module);
        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).setAttributes($workers$arg1))(span, { route: \"/users\" })", module);
        Assert.Contains("let manual = $workers$notNull($workers$notNull(ctx).tracing).startSpan(\"render\");", module);
        Assert.Contains("$workers$notNull(manual).recordException(", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).setStatus($workers$arg1$2))(manual, { code: \"ok\" })", module);
        Assert.Contains("$workers$notNull(manual).end()", module);
        Assert.Contains("(($workers$receiver$3) => $workers$receiver$3 == null ? null : ($workers$notNull($workers$receiver$3).isTraced ?? null))($workers$notNull($workers$notNull(ctx).tracing).getActiveSpan())", module);
    }
}
