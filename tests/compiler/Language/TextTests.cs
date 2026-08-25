namespace Workers.Compiler.Tests;

public sealed class TextTests
{
    [Fact]
    public void UsesStrictIntegerHexAndUriConversions()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(new
                {
                    number = int.Parse("42"),
                    bytes = Convert.FromHexString("00ff"),
                    hex = Convert.ToHexString(Convert.FromHexString("00ff")),
                    escaped = Uri.EscapeDataString("!*'()")
                });
            }
            """);

        Assert.Contains("intParse(\"42\")", module);
        Assert.Contains("hexDecode(\"00ff\")", module);
        Assert.Contains(".join(\"\").toUpperCase()", module);
        Assert.Contains("escapeDataString(", module);
        Assert.Contains("if (!/^[+-]?\\d+$/.test(value))", module);
        Assert.Contains("if (value.length % 2 !== 0 || !/^[0-9a-f]*$/i.test(value))", module);
        Assert.Contains("encodeURIComponent(value).replace(/[!'()*]/g", module);
    }

    [Fact]
    public void UsesRoundTripFormattingForInterpolatedDateTimes()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var timestamp = DateTimeOffset.UtcNow;
                    return Response.Json(new
                    {
                        interpolated = $"{timestamp:O}",
                        explicitFormat = timestamp.ToString("O")
                    });
                }
            }
            """);

        Assert.Equal(2, module.Split("0000+00:00", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void UsesNativeUtf8Base64UriAndStringOperations()
    {
        var module = Compile("""
            using System.Text;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var bytes = Encoding.UTF8.GetBytes("hello");
                    var encoded = Convert.ToBase64String(bytes);
                    var decoded = TextCodec.DecodeUtf8(Convert.FromBase64String(encoded), fatal: true);
                    var escaped = Uri.EscapeDataString(decoded.Replace("+", " "));
                    var upper = escaped.ToUpperInvariant();
                    var separator = upper.IndexOf("%", StringComparison.Ordinal);
                    var forwarded = request.WithUrl(new Url("/accepted", request.Url.Origin));
                    return Response.Json(new { decoded, upper, separator, forwarded = forwarded.Url.Path });
                }
            }
            """);

        Assert.Contains("base64Encode(bytes)", module);
        Assert.Contains("new TextDecoder(\"utf-8\", { fatal: true, ignoreBOM: false })", module);
        Assert.Contains("replaceAll(\"\\u002B\", \" \")", module);
        Assert.Contains("encodeURIComponent(", module);
        Assert.Contains("escaped.toUpperCase()", module);
        Assert.Contains("upper.indexOf(\"%\")", module);
        Assert.Contains("new Request(new URL(\"/accepted\", new URL(request.url).origin), request)", module);
    }

    [Fact]
    public void EmitsJavascriptSafeLiteralValuesInsteadOfCSharpTokenText()
    {
        var module = Compile(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var text = """raw ` ${ value } text""";
                    var number = 1.5f;
                    return Response.Text($"{text}:{number}");
                }
            }
            """");

        Assert.Contains("let text = \"raw \\u0060 ${ value } text\";", module);
        Assert.Contains("let number = 1.5;", module);
        Assert.DoesNotContain("1.5f", module);
    }

    [Fact]
    public void EscapesTemplateTextAndUsesEmptyTextForNullInterpolation()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    string? value = null;
                    return Response.Text($"literal ${{ marker }} ` slash \\ {value}");
                }
            }
            """);

        Assert.Contains("\\` slash \\\\ ${value ?? \"\"}", module);
        Assert.Contains("value ?? \"\"", module);
    }

    [Fact]
    public void AllocatesLegalJavascriptNamesForEscapedCSharpIdentifiers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request @default, Env env, Context ctx)
                {
                    var @delete = @default.Path;
                    return Response.Text(@delete);
                }
            }
            """);

        Assert.Contains("function $workers$fetch($workers$user$default, env, ctx)", module);
        Assert.Contains("let $workers$user$delete = new URL($workers$user$default.url).pathname;", module);
        Assert.DoesNotContain("@default", module);
    }

}
