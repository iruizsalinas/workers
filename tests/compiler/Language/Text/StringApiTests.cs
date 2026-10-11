using System.Text.Json;

namespace Workers.Compiler.Tests;

public sealed class StringApiTests
{
    [Fact]
    public void StringMembersBehaveLikeTheirJavaScriptEquivalents()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    List<string> values = ["one", "two"];
                    var text = "  Alpha-Beta  ";
                    return Response.Json(new {
                        nullOrEmpty = string.IsNullOrEmpty(null), white = string.IsNullOrWhiteSpace(" \t"),
                        joined = string.Join(",", values), concatenated = string.Concat(values),
                        joinedParams = string.Join("-", "a", "b"), concatenatedValues = string.Concat("a", null, "b"),
                        equal = string.Equals("Alpha", "alpha", StringComparison.OrdinalIgnoreCase),
                        contains = text.Contains("alpha", StringComparison.OrdinalIgnoreCase),
                        starts = text.StartsWith("  alpha", StringComparison.OrdinalIgnoreCase),
                        ends = text.EndsWith("  ", StringComparison.Ordinal),
                        index = text.IndexOf("beta", StringComparison.OrdinalIgnoreCase),
                        last = text.LastIndexOf("a", StringComparison.OrdinalIgnoreCase),
                        removed = text.Remove(0, 2), inserted = text.Insert(2, "new"),
                        left = "x".PadLeft(3, '0'), right = "x".PadRight(3),
                        characters = "ab".ToCharArray(), substring = "hello".Substring(1, 3),
                        trimmed = "\t x \n".Trim(), upper = "straße".ToUpperInvariant(),
                        replaced = "a-b-c".Replace("-", "+"),
                        pieces = " one , , two ".Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    });
                }
            }
            """);

        Assert.True(result.GetProperty("nullOrEmpty").GetBoolean());
        Assert.True(result.GetProperty("white").GetBoolean());
        Assert.Equal("one,two", result.GetProperty("joined").GetString());
        Assert.Equal("onetwo", result.GetProperty("concatenated").GetString());
        Assert.Equal("a-b", result.GetProperty("joinedParams").GetString());
        Assert.Equal("ab", result.GetProperty("concatenatedValues").GetString());
        Assert.True(result.GetProperty("equal").GetBoolean());
        Assert.True(result.GetProperty("contains").GetBoolean());
        Assert.True(result.GetProperty("starts").GetBoolean());
        Assert.True(result.GetProperty("ends").GetBoolean());
        Assert.Equal(8, result.GetProperty("index").GetInt32());
        Assert.Equal(11, result.GetProperty("last").GetInt32());
        Assert.Equal("Alpha-Beta  ", result.GetProperty("removed").GetString());
        Assert.Equal("  newAlpha-Beta  ", result.GetProperty("inserted").GetString());
        Assert.Equal("00x", result.GetProperty("left").GetString());
        Assert.Equal("x  ", result.GetProperty("right").GetString());
        Assert.Equal(["a", "b"], Strings(result.GetProperty("characters")));
        Assert.Equal("ell", result.GetProperty("substring").GetString());
        Assert.Equal("x", result.GetProperty("trimmed").GetString());
        // JavaScript casing applies full mappings.
        Assert.Equal("STRASSE", result.GetProperty("upper").GetString());
        Assert.Equal("a+b+c", result.GetProperty("replaced").GetString());
        Assert.Equal(["one", "two"], Strings(result.GetProperty("pieces")));
    }

    [Fact]
    public void StringEqualityTreatsNullLikeCSharp()
    {
        var result = JavaScriptRunner.FetchJson("""
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
                        bothNull = string.Equals(missing, null),
                        oneNull = string.Equals(missing, "a", StringComparison.OrdinalIgnoreCase),
                        instance = "a".Equals(missing)
                    });
                }
            }
            """);

        Assert.True(result.GetProperty("bothNull").GetBoolean());
        Assert.False(result.GetProperty("oneNull").GetBoolean());
        Assert.False(result.GetProperty("instance").GetBoolean());
    }

    [Theory]
    [InlineData("value.Equals(\"a\", StringComparison.CurrentCulture)")]
    [InlineData("value.Contains(\"a\", StringComparison.InvariantCultureIgnoreCase)")]
    [InlineData("value.Replace(\"a\", \"b\", StringComparison.OrdinalIgnoreCase)")]
    [InlineData("value.Split(',', 2)")]
    [InlineData("float.Parse(value)")]
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

    private static string[] Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!).ToArray();
}
