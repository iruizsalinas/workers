namespace Workers.Compiler.Tests;

public sealed class HttpRuntimeTests
{
    [Fact]
    public void ExposesNativeRequestAndUrlMetadata()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var url = request.Url;
                    return Response.Json(new
                    {
                        url.Origin,
                        url.Protocol,
                        url.Host,
                        url.Hostname,
                        url.Port,
                        url.Username,
                        url.Password,
                        url.Fragment,
                        request.Redirect,
                        hasSignal = request.Signal != null
                    });
                }
            }
            """);

        Assert.Contains("let url = new URL(request.url)", module);
        Assert.Contains("origin: url.origin", module);
        Assert.Contains("protocol: url.protocol", module);
        Assert.Contains("hostname: url.hostname", module);
        Assert.Contains("fragment: url.hash", module);
        Assert.Contains("redirect: request.redirect", module);
        Assert.Contains("hasSignal: request.signal != null", module);
    }

    [Fact]
    public void PreservesNamedResponseArgumentMeaning()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    new Response(body: "missing") { Status = 404 };
            }
            """);

        Assert.Contains("new Response(\"missing\", { status: 404 })", module);
    }

    [Fact]
    public void PreservesNamedResponseInstanceEvaluationOrder()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var events = new List<string>();
                    GetResponse(events).Headers.Set(
                        value: GetValue(events),
                        name: GetName(events));
                    return Response.Json(events);
                }

                private static Response GetResponse(List<string> events) { events.Add("receiver"); return new Response(); }
                private static string GetValue(List<string> events) { events.Add("value"); return "set"; }
                private static string GetName(List<string> events) { events.Add("name"); return "x-order"; }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) =>", module);
        Assert.Contains("$workers$receiver.set($workers$arg2, $workers$arg1)", module);
        Assert.Matches(
            @"\)\(\$workers\$cs\$Worker\$GetResponse\$\d+\(events\).headers, \$workers\$cs\$Worker\$GetValue\$\d+\(events\), \$workers\$cs\$Worker\$GetName\$\d+\(events\)\)",
            module);
    }

    [Fact]
    public void PreservesResponseInitializationOptions()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    new Response("missing") { Status = 404, StatusText = "Not Found" };
            }
            """);

        Assert.Contains("new Response(\"missing\", { status: 404, statusText: \"Not Found\" })", module);
    }

    [Fact]
    public void TreatsTailEventsAsTheNativeEventArray()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Tail]
                public static void Tail(TailEvent tail, Env env, Context context) =>
                    Log.Info(tail.Count.ToString());
            }
            """);

        Assert.Contains("console.info(String(tail.length))", module);
        Assert.DoesNotContain("tail.events", module);
    }

    [Fact]
    public void AdaptsModuleEventValuesToTheFacadeTypes()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Tail]
                public static void Tail(TailEvent tail) =>
                    Log.Info(tail[0].Event!.Request!.Headers.Get("content-type"));

                [Scheduled]
                public static void Scheduled(ScheduledEvent scheduled) =>
                    Log.Info(scheduled.ScheduledTime.ToString());
            }
            """);

        Assert.Contains("new Headers($workers$sequenceIndex(tail, 0).event.request.headers).get(\"content-type\")", module);
        Assert.Contains("scheduled.scheduledTime", module);
        Assert.DoesNotContain("scheduled.type", module);
    }

    [Fact]
    public void PassesJsonResponseStatusAndStatusTextToResponseJson()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var response = Response.Json(new { ok = true }, status: 201, statusText: "Created");
                    return Response.Json(new { response.Status, response.StatusText, text = await response.TextAsync() });
                }
            }
            """);

        Assert.Equal(201, result.GetProperty("status").GetInt32());
        Assert.Equal("Created", result.GetProperty("statusText").GetString());
        Assert.Equal("{\"ok\":true}", result.GetProperty("text").GetString());
    }

    [Fact]
    public void PreservesNamedBindingArgumentMeaning()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var socket = WebSocketPair.Create().Server;
                    socket.Close(reason: "done", code: 1000);
                    env.Kv("DATA").PutTextAsync(value: "hello", key: "message");
                    return new Response();
                }
            }
            """);

        Assert.Contains(".close($workers$arg2, $workers$arg1)", module);
        Assert.Contains(", \"done\", 1000)", module);
        Assert.Contains(".put($workers$arg2$2, $workers$arg1$2)", module);
        Assert.Contains(", \"hello\", \"message\")", module);
    }

}
