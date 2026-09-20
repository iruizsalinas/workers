namespace Workers.Compiler.Tests;

public sealed class CancellationTests
{
    [Fact]
    public void EmitsTokenSourcesStateAndCancellationChecks()
    {
        var module = Compile("""
            using System.Threading;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var source = new CancellationTokenSource();
                    var token = source.Token;
                    var none = CancellationToken.None;
                    CancellationToken fallback = default;
                    source.CancelAfter(25);
                    token.ThrowIfCancellationRequested();
                    source.Cancel();
                    return Response.Json(new {
                        canBeCanceled = token.CanBeCanceled,
                        tokenCanceled = token.IsCancellationRequested,
                        sourceCanceled = source.IsCancellationRequested,
                        equal = none == fallback,
                        unequal = token != none
                    });
                }
            }
            """);

        Assert.Contains("new AbortController()", module);
        Assert.Contains("source.signal", module);
        Assert.Contains("source.abort()", module);
        Assert.Contains("function $workers$cancellationCheck(signal)", module);
        Assert.Contains("function $workers$cancellationCancelAfter(controller, milliseconds)", module);
        Assert.Contains("let fallback = null", module);
    }

    [Fact]
    public void EmitsAbortableDelayAndFetch()
    {
        var module = Compile("""
            using System.Threading;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var source = new CancellationTokenSource();
                    await Task.Delay(10, source.Token);
                    return await Http.FetchAsync("https://example.com", new FetchOptions { Method = "GET" }, source.Token);
                }
            }
            """);

        Assert.Contains("function $workers$cancellationDelay(milliseconds, signal)", module);
        Assert.Contains("$workers$cancellationDelay(", module);
        Assert.Contains("source.signal", module);
        Assert.Contains("=> fetch(", module);
        Assert.Contains("...($workers$arg3 == null ? {} : { signal: $workers$arg3 })", module);
        Assert.Contains("{ signal:", module);
    }

    [Fact]
    public void ChecksBindingTokensBeforeDispatchAndPreservesEvaluationOrder()
    {
        var module = Compile("""
            using System.Threading;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var source = new CancellationTokenSource();
                    var value = await env.Kv("DATA").GetTextAsync("key", source.Token);
                    return Response.Text(value ?? "missing");
                }
            }
            """);

        Assert.Contains("=> ($workers$cancellationCheck(", module);
        Assert.Contains(".get(", module);
    }

    [Theory]
    [InlineData("token.Register(() => { })")]
    [InlineData("CancellationTokenSource.CreateLinkedTokenSource(token)")]
    [InlineData("source.CancelAfter(TimeSpan.FromSeconds(1))")]
    public void RejectsCancellationApisOutsideTheFocusedProfile(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Threading;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var source = new CancellationTokenSource();
                    var token = source.Token;
                    {{operation}};
                    return Response.Empty();
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }
}
