using System.Text.Json;

namespace Workers.Compiler.Tests;

public sealed class CookieTests
{
    [Fact]
    public void ResponseCookieWritersShareHeadersAndRespectTheirMutability()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var source = await Http.FetchAsync("data:text/plain,source");
                    var rejected = false;
                    try { source.Cookies.Set("session", "value"); }
                    catch (Exception) { rejected = true; }
                    var response = new Response(source);
                    response.Headers.Append("set-cookie", "raw=1");
                    var cookies = response.Cookies;
                    cookies.Set("session", "first", new CookieOptions { Path = "/app" });
                    response.Cookies.Set("theme", "dark");
                    cookies.Delete("session", new CookieOptions { Path = "/app" });
                    return Response.Json(new
                    {
                        rejected,
                        cookies = response.Headers.GetSetCookie(),
                        sourceCookies = source.Headers.GetSetCookie(),
                        response.Status
                    });
                }
            }
            """);

        Assert.True(result.GetProperty("rejected").GetBoolean());
        Assert.Equal(
        [
            "raw=1",
            "session=first; Path=/app; HttpOnly; Secure; SameSite=Lax",
            "theme=dark; Path=/; HttpOnly; Secure; SameSite=Lax",
            "session=; Path=/app; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT; HttpOnly; Secure; SameSite=Lax"
        ], Strings(result.GetProperty("cookies")));
        Assert.Empty(Strings(result.GetProperty("sourceCookies")));
        Assert.Equal(200, result.GetProperty("status").GetInt32());
    }

    [Fact]
    public void NamedCookieArgumentsEvaluateTheReceiverAndValuesOnceInSourceOrder()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var response = new Response();
                    var events = new List<string>();
                    GetCookies(response, events).Set(value: Record(events, "value"), name: Record(events, "name"));
                    GetCookies(response, events).Delete(options: new CookieOptions { Path = Record(events, "/app") }, name: Record(events, "name"));
                    return Response.Json(new { events, cookies = response.Headers.GetSetCookie() });
                }
                private static ResponseCookies GetCookies(Response response, List<string> events) { events.Add("receiver"); return response.Cookies; }
                private static string Record(List<string> events, string value) { events.Add(value); return value; }
            }
            """);

        Assert.Equal(["receiver", "value", "name", "receiver", "/app", "name"], Strings(result.GetProperty("events")));
        Assert.Equal(
        [
            "name=value; Path=/; HttpOnly; Secure; SameSite=Lax",
            "name=; Path=/app; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT; HttpOnly; Secure; SameSite=Lax"
        ], Strings(result.GetProperty("cookies")));
    }

    [Fact]
    public void ExposesCookieWritesOnlyThroughTheResponseCookieApi()
    {
        Assert.Null(typeof(global::Workers.Headers).GetMethod("SetCookie"));
        Assert.Null(typeof(global::Workers.Headers).GetMethod("DeleteCookie"));
        Assert.Null(typeof(global::Workers.Cookies).GetMethod("Set"));
        Assert.Null(typeof(global::Workers.Cookies).GetMethod("Delete"));
        Assert.Empty(typeof(global::Workers.ResponseCookies).GetConstructors());
        Assert.Equal(["Delete", "Set"], typeof(global::Workers.ResponseCookies).GetMethods()
            .Where(method => method.DeclaringType == typeof(global::Workers.ResponseCookies))
            .Select(method => method.Name).Order().ToArray());
    }

    [Fact]
    public void RequestCookiesParseDecodeAndKeepDuplicates()
    {
        var result = JavaScriptRunner.FetchJson(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var incoming = new Request("https://worker.test/", new FetchOptions
                    {
                        Headers = new Headers { ["cookie"] = "session=a%20b; theme=\"dark\"; session=outer; flag; =empty; bad=%E0%A4%A" }
                    });
                    var cookies = incoming.Cookies;
                    var names = new List<string>();
                    foreach (var cookie in cookies) names.Add(cookie.Name);
                    return Response.Json(new
                    {
                        session = cookies.Get("session"),
                        sessions = cookies.GetAll("session"),
                        theme = cookies.Get("theme"),
                        bad = cookies.Get("bad"),
                        missing = cookies.Get("missing"),
                        hasFlag = cookies.Contains("flag"),
                        hasTheme = cookies.Contains("theme"),
                        names,
                        none = request.Cookies.Contains("session")
                    });
                }
            }
            """");

        Assert.Equal("a b", result.GetProperty("session").GetString());
        Assert.Equal(["a b", "outer"], Strings(result.GetProperty("sessions")));
        Assert.Equal("dark", result.GetProperty("theme").GetString());
        Assert.Equal("%E0%A4%A", result.GetProperty("bad").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("missing").ValueKind);
        Assert.False(result.GetProperty("hasFlag").GetBoolean());
        Assert.True(result.GetProperty("hasTheme").GetBoolean());
        Assert.Equal(["session", "theme", "session", "bad"], Strings(result.GetProperty("names")));
        Assert.False(result.GetProperty("none").GetBoolean());
    }

    [Fact]
    public void SetCookieUsesSecureDefaultsAndEncodesValues()
    {
        var cookies = SetCookies("""
            response.Cookies.Set("session", "a; Domain=evil.test\r\nx");
            response.Cookies.Set("prefs", "dark", new CookieOptions
                {
                    Path = "/app", Domain = "example.com", MaxAgeSeconds = 3600, Expires = new Date(0),
                    HttpOnly = false, SameSite = SameSite.Strict
            });
            response.Cookies.Set("__Host-id", "1", new CookieOptions { SameSite = SameSite.None, Partitioned = true });
            """);

        Assert.Equal(
        [
            "session=a%3B%20Domain%3Devil.test%0D%0Ax; Path=/; HttpOnly; Secure; SameSite=Lax",
            "prefs=dark; Path=/app; Domain=example.com; Max-Age=3600; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Secure; SameSite=Strict",
            "__Host-id=1; Path=/; HttpOnly; Secure; SameSite=None; Partitioned"
        ], cookies);
    }

    [Fact]
    public void DeleteCookieExpiresTheMatchingCookie()
    {
        var cookies = SetCookies("""
            response.Cookies.Delete("prefs", new CookieOptions { Path = "/app", MaxAgeSeconds = 60 });
            """);

        Assert.Equal(["prefs=; Path=/app; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT; HttpOnly; Secure; SameSite=Lax"], cookies);
    }

    [Fact]
    public void CookieOptionDefaultsAreVisibleToWorkerCode()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var options = new CookieOptions { Path = null };
                    return Response.Json(new { options.HttpOnly, options.Secure, lax = options.SameSite == SameSite.Lax, options.Partitioned, noPath = options.Path is null });
                }
            }
            """);

        var defaults = new global::Workers.CookieOptions();
        Assert.Equal(defaults.HttpOnly, result.GetProperty("httpOnly").GetBoolean());
        Assert.Equal(defaults.Secure, result.GetProperty("secure").GetBoolean());
        Assert.Equal(defaults.SameSite == global::Workers.SameSite.Lax, result.GetProperty("lax").GetBoolean());
        Assert.Equal(defaults.Partitioned, result.GetProperty("partitioned").GetBoolean());
        Assert.True(result.GetProperty("noPath").GetBoolean());
    }

    [Fact]
    public void StructuralDefaultsMatchTheCookieOptionInitializers()
    {
        var defaults = new global::Workers.CookieOptions();
        Assert.Equal(
            [("path", $"\"{defaults.Path}\""), ("httpOnly", Lower(defaults.HttpOnly)), ("secure", Lower(defaults.Secure)),
                ("sameSite", ((int)defaults.SameSite).ToString()), ("partitioned", Lower(defaults.Partitioned))],
            global::JavaScriptEmitter.StructuralDefaultProperties("Workers.CookieOptions"));
        Assert.Null(defaults.Domain);
        Assert.Null(defaults.Expires);
        Assert.Null(defaults.MaxAgeSeconds);
    }

    [Theory]
    [InlineData("""Set("bad name", "v")""", "Invalid cookie name")]
    [InlineData("""Set("a;b", "v")""", "Invalid cookie name")]
    [InlineData("""Set("", "v")""", "Invalid cookie name")]
    [InlineData("""Set("a", "v", new CookieOptions { Path = "/x; Domain=evil" })""", "Invalid cookie path")]
    [InlineData("""Set("a", "v", new CookieOptions { Path = "relative" })""", "Invalid cookie path")]
    [InlineData("""Set("a", "v", new CookieOptions { Domain = "evil.test; Secure" })""", "Invalid cookie domain")]
    [InlineData("""Set("a", "v", new CookieOptions { MaxAgeSeconds = -1 })""", "non-negative integer")]
    [InlineData("""Set("a", "v", new CookieOptions { Expires = new Date("not a date") })""", "not a valid date")]
    [InlineData("""Set("a", "v", new CookieOptions { SameSite = (SameSite)7 })""", "Invalid cookie SameSite")]
    [InlineData("""Set("a", "v", new CookieOptions { Secure = false, SameSite = SameSite.None })""", "requires Secure")]
    [InlineData("""Set("a", "v", new CookieOptions { Secure = false, Partitioned = true })""", "requires Secure")]
    [InlineData("""Set("__Secure-a", "v", new CookieOptions { Secure = false })""", "requires Secure")]
    [InlineData("""Set("__host-a", "v", new CookieOptions { Secure = false })""", "requires Secure")]
    [InlineData("""Set("__Host-a", "v", new CookieOptions { Domain = "example.com" })""", "requires Path=/ and no Domain")]
    [InlineData("""Set("__Host-a", "v", new CookieOptions { Path = "/app" })""", "requires Path=/ and no Domain")]
    [InlineData("""Set("__Http-a", "v", new CookieOptions { HttpOnly = false })""", "requires HttpOnly")]
    [InlineData("""Set("a", "".PadRight(4096, 'x'))""", "4096 byte limit")]
    public void InvalidCookiesAreRejectedInsteadOfSilentlyDropped(string call, string message)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    try
                    {
                        var response = new Response();
                        response.Cookies.{{call}};
                        return Response.Json(new { error = "", cookies = response.Headers.GetSetCookie() });
                    }
                    catch (Exception exception)
                    {
                        return Response.Json(new { error = exception.Message, cookies = new List<string>() });
                    }
                }
            }
            """);

        Assert.Contains(message, result.GetProperty("error").GetString());
    }

    private static string[] SetCookies(string response) => Strings(JavaScriptRunner.FetchJson($$"""
        using Workers;
        public static class Worker
        {
            [Fetch]
            public static Response Fetch(Request request, Env env, Context context)
            {
                var response = new Response();
                {{response}}
                return Response.Json(response.Headers.GetSetCookie());
            }
        }
        """));

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private static string Lower(bool value) => value ? "true" : "false";
}
