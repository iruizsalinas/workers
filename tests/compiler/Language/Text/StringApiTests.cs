namespace Workers.Compiler.Tests;

public sealed class StringApiTests
{
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
                    Response.Json(new
                    {
                        slug = Regex.IsMatch(request.Path, "^[a-zA-Z0-9_-]+$"),
                        length = Regex.IsMatch(request.Path, "^[1-9][0-9]{0,3}$"),
                        digits = Regex.IsMatch(request.Path, "^[0-9]{1,9}$")
                    });
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
            Assert.StartsWith("WRK120:", error.Message);
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
                        supplementary = string.Equals("𐐨", "𐐀", StringComparison.OrdinalIgnoreCase),
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
        Assert.Contains("for (const character of value)", module);
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
