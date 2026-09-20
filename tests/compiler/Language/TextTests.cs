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

    [Fact]
    public void ValidatesStringArgumentsAndSubstringRanges()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    string? missing = null;
                    var value = "hello";
                    return Response.Json(new {
                        contains = value.Contains(missing!), starts = value.StartsWith(missing!),
                        ends = value.EndsWith(missing!), substring = value.Substring(-1),
                        replaced = value.Replace("", "x")
                    });
                }
            }
            """);

        Assert.Contains("function $workers$stringContains(source, value)", module);
        Assert.Contains("function $workers$stringSubstring(source, start, length)", module);
        Assert.Contains("start < 0 || start > source.length", module);
        Assert.Contains("if (oldValue.length === 0)", module);
    }

    [Fact]
    public void UsesClrWhitespaceRulesForTrim()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Text("\u0085 value \uFEFF".Trim());
            }
            """);

        Assert.Contains("stringTrim(", module);
        Assert.Contains("\\u0085", module);
        Assert.DoesNotContain(".trim()", module);
    }

    [Fact]
    public void AcceptsOnlyLiteralRegexPatternsFromTheCompatibleSubset()
    {
        var module = Compile("""
            using System.Text.RegularExpressions;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(Regex.IsMatch(request.Path, "^[a-zA-Z0-9_-]+$"));
            }
            """);
        Assert.Contains("new RegExp(", module);

        foreach (var pattern in new[] { "dynamic", "unicode" })
        {
            var source = pattern == "dynamic"
                ? "var pattern = request.Path; return Response.Json(Regex.IsMatch(request.Path, pattern));"
                : "return Response.Json(Regex.IsMatch(request.Path, @\"\\d+\"));";
            var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
                using System.Text.RegularExpressions;
                using Workers;
                public static class Worker
                {
                    [Fetch]
                    public static Response Fetch(Request request, Env env, Context context)
                    {
                        {{source}}
                    }
                }
                """));
            Assert.StartsWith("WRK105:", error.Message);
        }
    }

    [Fact]
    public void EmitsInvariantStringQueriesAndTransformations()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<string> values = ["one", "two"];
                    var text = "  Alpha-Beta  ";
                    return Response.Json(new {
                        empty = string.Empty, nullOrEmpty = string.IsNullOrEmpty(null),
                        white = string.IsNullOrWhiteSpace(" \t"),
                        joined = string.Join(",", values), concatenated = string.Concat(values),
                        joinedParams = string.Join("-", "a", "b"),
                        concatenatedValues = string.Concat("a", null, "b"),
                        equal = string.Equals("Alpha", "alpha", StringComparison.OrdinalIgnoreCase),
                        contains = text.Contains("alpha", StringComparison.OrdinalIgnoreCase),
                        starts = text.StartsWith("  alpha", StringComparison.OrdinalIgnoreCase),
                        ends = text.EndsWith("  ", StringComparison.Ordinal),
                        index = text.IndexOf("beta", StringComparison.OrdinalIgnoreCase),
                        last = text.LastIndexOf("a", StringComparison.OrdinalIgnoreCase),
                        removed = text.Remove(0, 2), inserted = text.Insert(2, "new"),
                        left = "x".PadLeft(3, '0'), right = "x".PadRight(3),
                        characters = "😀".ToCharArray(),
                        slice = text.ToCharArray(2, 5),
                        pieces = " one , , two ".Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    });
                }
            }
            """);

        Assert.Contains("$workers$stringIsNullOrEmpty(null)", module);
        Assert.Contains("$workers$stringIsNullOrWhiteSpace(", module);
        Assert.Contains("$workers$stringJoin(\",\", values)", module);
        Assert.Contains("$workers$stringOrdinal(", module);
        Assert.Contains("$workers$stringRemove(", module);
        Assert.Contains("$workers$stringInsert(", module);
        Assert.Contains("$workers$stringPad(", module);
        Assert.Contains("$workers$stringToCharArray(", module);
        Assert.Contains("$workers$stringSplit(", module);
    }

    [Fact]
    public void KeepsStaticStringEqualsNullSafeButRejectsNullInstanceReceivers()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    string? missing = null;
                    return Response.Json(new
                    {
                        staticResult = string.Equals(missing, null),
                        instanceResult = missing!.Equals(null),
                        ordinalResult = missing!.Equals(null, StringComparison.Ordinal)
                    });
                }
            }
            """);

        Assert.Contains("stringOrdinal(missing, null, false, 0", module);
        Assert.Equal(2, module.Split("stringOrdinal(missing, null, false, 6", StringSplitOptions.None).Length - 1);
        Assert.Contains("if (source == null) throw new TypeError", module);
        Assert.Contains("if (value == null) return false", module);
    }

    [Theory]
    [InlineData("value.Equals(\"a\", StringComparison.CurrentCulture)")]
    [InlineData("value.Contains(\"a\", StringComparison.InvariantCultureIgnoreCase)")]
    [InlineData("value.Replace(\"a\", \"b\", StringComparison.OrdinalIgnoreCase)")]
    [InlineData("value.Split(\",\", StringSplitOptions.None)")]
    public void RejectsCultureSensitiveOrBroadStringOverloads(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var value = "alpha";
                    return Response.Json({{operation}});
                }
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

}
