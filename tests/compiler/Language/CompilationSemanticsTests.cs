namespace Workers.Compiler.Tests;

public sealed class CompilationSemanticsTests
{
    [Fact]
    public void PreservesListInitializersAndCoalescingPrecedence()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new List<int> { 2, 4, 6 };
                    bool? configured = null;
                    var accepted = configured ?? request.Method == "POST" || request.Method == "PUT";
                    return Response.Json(new { values, accepted });
                }
            }
            """);

        Assert.Contains("let values = [2, 4, 6]", module);
        Assert.Contains("((configured) ?? (request.method === \"POST\" || request.method === \"PUT\"))", module);
    }

    [Fact]
    public void BindsNamedRecordAndNativeConstructorArguments()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var parcel = new Parcel(Count: 3, Label: "priority");
                    var rewritten = new Request(
                        options: new FetchOptions { Method = "POST" },
                        url: "https://worker.test/reordered");
                    var resolved = new Url(
                        baseUrl: "https://worker.test/root/",
                        value: "child");
                    return Response.Json(new { parcel, rewritten, resolved });
                }
            }
            public sealed record Parcel(string Label, int Count);
            """);

        Assert.Contains("{ count: 3, label: \"priority\" }", module);
        Assert.Contains("=> new Request($workers$arg2, $workers$arg1)", module);
        Assert.Contains("=> new URL($workers$arg2$2, $workers$arg1$2)", module);
    }

    [Fact]
    public void InlinesQualifiedUserConstants()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var state = ParcelState.Ready;
                    return Response.Json(new { state, ready = state == ParcelState.Ready, limit = Limits.Maximum });
                }
            }
            public enum ParcelState { Pending, Ready }
            public static class Limits { public const int Maximum = 25; }
            """);

        Assert.Contains("let state = 1", module);
        Assert.Contains("ready: state === 1", module);
        Assert.Contains("limit: 25", module);
    }
}
