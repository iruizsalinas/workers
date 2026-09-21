namespace Workers.Compiler.Tests;

public sealed class CollectionControlFlowTests
{
    [Fact]
    public void EnumeratesStringsAsUtf16Characters()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new List<int>();
                    foreach (var character in "😀")
                        values.Add(character + 0);
                    return Response.Json(values);
                }
            }
            """);

        Assert.Contains("for (const character of $workers$linqValues(", module);
    }

    [Fact]
    public void UsesNativeSetSemanticsForHashSets()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var methods = new HashSet<string> { "GET", "HEAD" };
                    methods.Add("OPTIONS");
                    return Response.Json(new
                    {
                        allowed = methods.Contains(request.Method),
                        count = methods.Count
                    });
                }
            }
            """);

        Assert.Contains("let methods = new Set([\"GET\", \"HEAD\"]);", module);
        Assert.Contains("setAdd(methods, \"OPTIONS\")", module);
        Assert.Contains("allowed: methods.has(request.method)", module);
        Assert.Contains("count: methods.size", module);
    }

    [Fact]
    public void RejectsHashSetsThatNeedClrValueEquality()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new HashSet<Item> { new Item(1) };
                    return Response.Json(values.Contains(new Item(1)));
                }
            }
            public sealed record Item(int Id);
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

    [Fact]
    public void ValidatesTaskDelayAndPreservesInfiniteDelay()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await Task.Delay(-1);
                    return Response.Text("unreachable");
                }
            }
            """);

        Assert.Contains("if (milliseconds < -1 || milliseconds > 4294967294)", module);
        Assert.Contains("if (milliseconds === -1) return new Promise(() => {})", module);
        Assert.Contains("return scheduler.wait(milliseconds)", module);
    }

    [Fact]
    public void RunsIndependentTasksInParallel()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var responses = await Task.WhenAll(
                        Http.FetchAsync("https://one.example"),
                        Http.FetchAsync("https://two.example"));
                    var pending = new List<Task<Response>> { Http.FetchAsync("https://three.example") };
                    var more = await Task.WhenAll(pending);
                    var single = await Task.WhenAll(Http.FetchAsync("https://four.example"));
                    return Response.Json(new { first = responses[0].Status, second = responses[1].Status });
                }
            }
            """);

        Assert.Contains("await Promise.all([fetch(\"https://one.example\"), fetch(\"https://two.example\")])", module);
        Assert.Contains("await Promise.all(pending)", module);
        Assert.Contains("await Promise.all([fetch(\"https://four.example\")])", module);
    }

    [Fact]
    public void SupportsControlFlowInsideAsyncCallbacks()
    {
        var module = Compile("""
            using Workers;
            [DurableObject("Gate")]
            public sealed class Gate
            {
                private readonly DurableObjectState _state;
                public Gate(DurableObjectState state, Env env) => _state = state;

                public Task ReserveAsync(string owner) => _state.Storage.TransactionAsync(async storage =>
                {
                    var current = await storage.GetAsync<string>("owner");
                    if (current is not null)
                        return;
                    await storage.PutAsync("owner", owner);
                });
            }
            """);

        Assert.Contains("storage => {\n  let current = await storage.get(\"owner\")", module);
        Assert.Contains("if (current != null)", module);
        Assert.Contains("await storage.put(\"owner\", owner)", module);
    }

    [Fact]
    public void UsesObjectSemanticsForStringDictionaries()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var values = new Dictionary<string, int> { ["one"] = 1, ["two"] = 2 };
                    var total = 0;
                    foreach (var entry in values) total += entry.Value;
                    return Response.Json(new { count = values.Count, total, first = values["one"], values });
                }
            }
            """);

        Assert.Contains("let values = Object.assign(Object.create(null), { [\"one\"]: 1, [\"two\"]: 2 });", module);
        Assert.Contains("for (const entry of Object.entries(values))", module);
        Assert.Contains("count: Object.keys(values).length", module);
        Assert.Contains("first: $workers$dictionaryIndex(values, \"one\")", module);
    }

}
