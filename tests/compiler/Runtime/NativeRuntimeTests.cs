namespace Workers.Compiler.Tests;

public sealed class NativeRuntimeTests
{
    [Fact]
    public void JsonAndRuntimeOperationsUseNativeJavaScriptWithoutCompatibilityHelpers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var value = Json.Parse("{\"name\":\"Ada\"}");
                    var date = new Date(Date.Utc(2024, 1, 29));
                    var expression = new RegExp("^.$", "u");
                    var controller = new AbortController();
                    controller.Abort("done");
                    return Response.Json(new {
                        name = value["name"].AsString(), json = Json.Stringify(value),
                        date = date.ToIsoString(), timestamp = date.GetTime(),
                        matched = expression.Test("😀"), controller.Signal.Aborted
                    });
                }
            }
            """);

        Assert.Contains("JSON.parse(", module);
        Assert.Contains("JSON.stringify(value)", module);
        Assert.Contains("new Date(Date.UTC(2024, 1, 29))", module);
        Assert.Contains("date.toISOString()", module);
        Assert.Contains("date.getTime()", module);
        Assert.Contains("new RegExp(\"^.$\", \"u\")", module);
        Assert.Contains("controller.abort(\"done\")", module);
        Assert.Contains("controller.signal.aborted", module);
        Assert.Contains("$workers$jsonExpect(value[\"name\"], \"string\")", module);
        Assert.DoesNotContain("function $workers$date", module);
        Assert.DoesNotContain("function $workers$regex", module);
    }

    [Theory]
    [InlineData("System.Text.Json.JsonSerializer.Serialize(1)")]
    [InlineData("System.DateTimeOffset.UtcNow.ToString(\"O\")")]
    [InlineData("System.Guid.NewGuid().ToString()")]
    [InlineData("System.Text.RegularExpressions.Regex.IsMatch(\"x\", \"x\")")]
    [InlineData("System.Uri.EscapeDataString(\"x\")")]
    [InlineData("System.Convert.ToBase64String(new byte[] { 1 })")]
    [InlineData("new System.Text.StringBuilder()")]
    [InlineData("System.Math.Round(2.5)")]
    [InlineData("System.Random.Shared.NextDouble()")]
    [InlineData("System.Threading.Tasks.Task.Delay(1)")]
    [InlineData("new System.Threading.CancellationTokenSource()")]
    [InlineData("new System.TimeSpan(1, 2, 3)")]
    [InlineData("new System.DateOnly(2026, 10, 8)")]
    [InlineData("new System.TimeOnly(12, 30)")]
    public void RejectsRemovedDotNetRuntimeApis(string expression)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json({{expression}});
            }
            """));
        Assert.Contains("WRK105", error.Message);
    }

    [Fact]
    public void SchedulerWaitReceivesTheNativeSignal()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await Timers.WaitAsync(1, request.Signal);
                    return Response.Text("ok");
                }
            }
            """);
        Assert.Contains("scheduler.wait(", module);
        Assert.Contains("signal:", module);
        Assert.DoesNotContain("cancellationDelay", module);
    }

    [Theory]
    [InlineData("JsonPropertyName(\"legacy\")")]
    [InlineData("JsonIgnore")]
    [InlineData("JsonRequired")]
    public void RejectsLegacyJsonAttributesRatherThanIgnoringThem(string attribute)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public sealed record User([property: System.Text.Json.Serialization.{{attribute}}] string Name);
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(new User("Ada"));
            }
            """));
        Assert.Contains("WRK119", error.Message);
    }

    [Fact]
    public void RejectsLegacyTypesInsideTypedJsonContracts()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public sealed record Event(System.DateTimeOffset At);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context) =>
                    Response.Json(await request.JsonAsync<Event>());
            }
            """));
        Assert.Contains("WRK119", error.Message);
    }
}
