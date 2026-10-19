namespace Workers.Compiler.Tests;

public sealed class HeaderMutationRegressionTests
{
    [Theory]
    [InlineData("Receiver()[Key()] ??= Input()", null, "new", 1)]
    [InlineData("Receiver()[Key()] ??= Input()", "old", "old", 0)]
    [InlineData("Receiver()[Key()] += Input()", "old", "oldnew", 1)]
    [InlineData("Receiver()[Key()] += Input()", null, "new", 1)]
    public void HeaderMutationsCaptureStorageAndReturnTheAssignedValue(string expression, string? initial, string expected, int expectedInputs)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                private static Headers headers = new Headers();
                private static int receivers, keys, inputs;
                private static Headers Receiver() { receivers++; return headers; }
                private static string Key() { keys++; return "X-Test"; }
                private static string Input() { inputs++; return "new"; }
                [Fetch]
                public static Response Fetch(Request request)
                {
                    string? initial = {{(initial is null ? "null" : "\"" + initial + "\"")}};
                    if (initial is not null) headers["x-test"] = initial;
                    var assigned = {{expression}};
                    return Response.Json(new { assigned, stored = headers["x-test"], receivers, keys, inputs });
                }
            }
            """);
        Assert.Equal(expected, result.GetProperty("assigned").GetString());
        Assert.Equal(expected, result.GetProperty("stored").GetString());
        Assert.Equal(1, result.GetProperty("receivers").GetInt32());
        Assert.Equal(1, result.GetProperty("keys").GetInt32());
        Assert.Equal(expectedInputs, result.GetProperty("inputs").GetInt32());
    }

    [Fact]
    public void AwaitedHeaderMutationsRetainTheirOriginalReceiver()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static Headers current = new Headers();
                private static int inputs;
                private static async Task<string> Input()
                {
                    inputs++;
                    current = new Headers();
                    await Task.FromResult(0);
                    return "new";
                }
                [Fetch]
                public static async Task<Response> Fetch(Request request)
                {
                    var original = current;
                    var assigned = current["X-Test"] ??= await Input();
                    var skipped = original["x-test"] ??= await Input();
                    var combined = original["x-test"] += await Task.FromResult("-suffix");
                    return Response.Json(new { assigned, skipped, combined, stored = original["x-test"],
                        replacement = current["x-test"], inputs });
                }
            }
            """);
        Assert.Equal("new", result.GetProperty("assigned").GetString());
        Assert.Equal("new", result.GetProperty("skipped").GetString());
        Assert.Equal("new-suffix", result.GetProperty("combined").GetString());
        Assert.Equal("new-suffix", result.GetProperty("stored").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, result.GetProperty("replacement").ValueKind);
        Assert.Equal(1, result.GetProperty("inputs").GetInt32());
    }

    [Fact]
    public void HeadersCanBeDeconstructionTargets()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    var headers = new Headers();
                    var assigned = (headers["x-first"], headers["x-second"]) = ("one", "two");
                    return Response.Json(new { first = headers["X-First"], second = headers["X-Second"],
                        assignedFirst = assigned.Item1, assignedSecond = assigned.Item2 });
                }
            }
            """);
        Assert.Equal("one", result.GetProperty("first").GetString());
        Assert.Equal("two", result.GetProperty("second").GetString());
        Assert.Equal("one", result.GetProperty("assignedFirst").GetString());
        Assert.Equal("two", result.GetProperty("assignedSecond").GetString());
    }
}
