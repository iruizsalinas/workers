namespace Workers.Compiler.Tests;

public sealed class CompilationSemanticsTests
{
    [Fact]
    public void ReordersNamedArgumentsWithoutChangingSourceEvaluationOrder()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Text(Pair(b: Math.Max(val1: 1, val2: 2), a: 3));

                private static string Pair(int a = 10, int b = 20) => $"{a}:{b}";
            }
            """);

        Assert.Contains("(($workers$arg1$2, $workers$arg2$2) => $workers$cs$Worker$Pair$0($workers$arg2$2, $workers$arg1$2))", module);
        Assert.Contains("(($workers$arg1, $workers$arg2) => Math.max($workers$arg1, $workers$arg2))(1, 2)", module);
    }

    [Fact]
    public void PreservesOptionalDefaultsForUserMethodsWithNamedArguments()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Text(Pair(b: 2));
                private static string Pair(int a = 10, int b = 20) => $"{a}:{b}";
            }
            """);

        Assert.Contains("$workers$cs$Worker$Pair$0(10, $workers$arg1)", module);
        Assert.Contains("function $workers$cs$Worker$Pair$0(a = 10, b = 20)", module);
    }

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
        Assert.Matches(@"=> new Request\(\$workers\$arg2(?:\$\d+)?, \$workers\$arg1(?:\$\d+)?\)", module);
        Assert.Matches(@"=> new URL\(\$workers\$arg2(?:\$\d+)?, \$workers\$arg1(?:\$\d+)?\)", module);
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
