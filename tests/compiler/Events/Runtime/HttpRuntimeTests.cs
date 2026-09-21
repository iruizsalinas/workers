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
                        hasSignal = request.Signal is not null
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
                    Response.Text(status: 404, body: "missing");
            }
            """);

        Assert.Contains("new Response($workers$arg2, { status: $workers$arg1 })", module);
        Assert.Contains(")(404, \"missing\")", module);
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
                    return GetResponse(events).WithHeader(
                        value: GetValue(events),
                        name: GetName(events));
                }

                private static Response GetResponse(List<string> events) { events.Add("receiver"); return Response.Empty(); }
                private static string GetValue(List<string> events) { events.Add("value"); return "set"; }
                private static string GetName(List<string> events) { events.Add("name"); return "x-order"; }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) =>", module);
        Assert.Contains("$workers$withHeader($workers$receiver, $workers$arg2, $workers$arg1)", module);
        Assert.Matches(
            @"\)\(\$workers\$cs\$Worker\$GetResponse\$\d+\(events\), \$workers\$cs\$Worker\$GetValue\$\d+\(events\), \$workers\$cs\$Worker\$GetName\$\d+\(events\)\)",
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
                    Response.FromBody(Body.Text("missing"), 404, "Not Found");
            }
            """);

        Assert.Contains("new Response($workers$body?.body ?? $workers$body, { status: 404, statusText: \"Not Found\" ?? undefined })", module);
        Assert.Contains(")(\"missing\")", module);
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
                    Console.WriteLine(tail.Events.Count.ToString());
            }
            """);

        Assert.Contains("console.log(String(tail.length))", module);
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
                    Console.WriteLine(tail[0].Event!.Request!.Headers.Get("content-type"));

                [Scheduled]
                public static void Scheduled(ScheduledEvent scheduled) =>
                    Console.WriteLine($"{scheduled.Type}:{scheduled.Schedule}");
            }
            """);

        Assert.Contains("new Headers($workers$sequenceIndex(tail, 0).event.request.headers).get(\"content-type\")", module);
        Assert.Contains("scheduled.scheduledTime", module);
        Assert.DoesNotContain("scheduled.type", module);
    }

    [Fact]
    public void PreservesJsonResponseOptionsAndStatusText()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { ok = true }, 201, new { headers = new { location = "/items/1" } }, "Created");
            }
            """);

        Assert.Contains("...({ headers: { location: \"/items/1\" } } ?? {})", module);
        Assert.Contains("status: 201, statusText: \"Created\" ?? undefined", module);
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
                    return Response.Empty();
                }
            }
            """);

        Assert.Contains(".close($workers$arg2, $workers$arg1)", module);
        Assert.Contains(", \"done\", 1000)", module);
        Assert.Contains(".put($workers$arg2$2, $workers$arg1$2)", module);
        Assert.Contains(", \"hello\", \"message\")", module);
    }

}
