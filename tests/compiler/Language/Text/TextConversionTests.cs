namespace Workers.Compiler.Tests;

public sealed class TextConversionTests
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

        Assert.Contains("numericParse(\"42\", 0)", module);
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
    public void UsesClrFormattingForBooleanToString()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var enabled = true;
                    return Response.Text(enabled.ToString());
                }
            }
            """);

        Assert.Contains("(enabled ? \"True\" : \"False\")", module);
        Assert.DoesNotContain("String(enabled)", module);
    }

    [Fact]
    public void ResolvesVerbatimFrameworkMemberNamesSemantically()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var text = "value";
                    string? optional = text;
                    return Response.Json(new
                    {
                        length = text.@Length,
                        contains = text.@Contains("alu"),
                        optionalLength = optional?.@Length
                    });
                }
            }
            """);

        Assert.Contains("length: text.length", module);
        Assert.Contains("stringContains(text, \"alu\")", module);
        Assert.Contains("optionalLength: optional?.length", module);
        Assert.DoesNotContain("@Length", module);
        Assert.DoesNotContain("@Contains", module);
    }

    [Fact]
    public void UsesJsonElementValueKindFormattingForToString()
    {
        var module = Compile("""
            using System.Text.Json;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var value = await request.JsonAsync<JsonElement>();
                    return Response.Text(value.ToString());
                }
            }
            """);

        Assert.Contains("jsonElementToString(value)", module);
        Assert.Contains("if (typeof value === \"string\") return value;", module);
        Assert.Contains("if (typeof value === \"boolean\") return value ? \"True\" : \"False\";", module);
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
        Assert.Contains("stringReplace(decoded, \"\\u002B\", \" \")", module);
        Assert.Contains("encodeURIComponent(", module);
        Assert.Contains("escaped.toUpperCase()", module);
        Assert.Contains("$workers$stringOrdinal(upper, \"%\", false, 4", module);
        Assert.Contains("new Request(new URL(\"/accepted\", new URL(request.url).origin), request)", module);
    }

}
