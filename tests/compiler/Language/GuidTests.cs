namespace Workers.Compiler.Tests;

public sealed class GuidTests
{
    [Fact]
    public void EmitsCanonicalParsingFormattingAndEquality()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var value = Guid.Parse("{00112233-4455-6677-8899-AABBCCDDEEFF}");
                    var same = Guid.Parse("00112233445566778899aabbccddeeff");
                    return Response.Json(new {
                        value,
                        empty = Guid.Empty,
                        equal = value == same,
                        unequal = value != Guid.Empty,
                        instanceEqual = value.Equals(same),
                        compact = value.ToString("N"),
                        braces = value.ToString("B"),
                        parentheses = value.ToString("P")
                    });
                }
            }
            """);

        Assert.Contains("function $workers$guidParse(input)", module);
        Assert.Contains("function $workers$guidFormat(value, format)", module);
        Assert.Contains("\"00000000-0000-0000-0000-000000000000\"", module);
        Assert.Contains("value === same", module);
        Assert.Contains("value !== \"00000000-0000-0000-0000-000000000000\"", module);
    }

    [Fact]
    public void NewGuidAndDefaultFormattingNeedNoHelper()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Text(Guid.NewGuid().ToString());
            }
            """);

        Assert.Contains("globalThis.crypto.randomUUID()", module);
        Assert.DoesNotContain("function $workers$guid", module);
    }

    [Theory]
    [InlineData("Guid.TryParse(\"00112233-4455-6677-8899-aabbccddeeff\", out var value)")]
    [InlineData("Guid.Parse(\"00112233-4455-6677-8899-aabbccddeeff\").ToString(\"X\")")]
    [InlineData("Guid.Parse(\"00112233-4455-6677-8899-aabbccddeeff\").CompareTo(Guid.Empty)")]
    [InlineData("new Guid(\"00112233-4455-6677-8899-aabbccddeeff\")")]
    public void RejectsGuidApisOutsideTheFocusedProfile(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json({{operation}});
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }
}
