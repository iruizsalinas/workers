namespace Workers.Compiler.Tests;

public sealed class DateOnlyTests
{
    private static string Worker(string body) => $$"""
        using System.Globalization;
        using System.Text.Json;
        using Workers;
        public static class Worker
        {
            [Fetch]
            public static Response Fetch(Request request, Env env, Context context)
            {
                {{body}}
            }
        }
        """;

    [Fact]
    public void RepresentsValuesAsTheirJsonStrings()
    {
        var module = Compile(Worker("""
            var date = new DateOnly(2026, 10, 8);
            var time = new TimeOnly(9, 30);
            return Response.Json(new { date, time, DateOnly.MinValue, TimeOnly.MaxValue, same = date == DateOnly.MaxValue, before = time < TimeOnly.MaxValue });
            """));

        Assert.Contains("$workers$dateOnlyCreate(2026, 10, 8)", module);
        Assert.Contains("$workers$timeOnlyCreate(9, 30)", module);
        Assert.Contains("\"0001-01-01\"", module);
        Assert.Contains("\"23:59:59.9999999\"", module);
        Assert.Contains("(date === \"9999-12-31\")", module);
        Assert.DoesNotContain("function $workers$dateFormat", module);
    }

    [Fact]
    public void ReadsJsonWithTheSystemTextJsonGrammar()
    {
        var module = Compile(Worker("""
            var date = JsonSerializer.Deserialize<DateOnly>("\"2026-10-08\"");
            var time = JsonSerializer.Deserialize<TimeOnly>("\"8:30\"");
            return Response.Json(new { date, time });
            """));

        Assert.Contains("2026-10-08", module);
        Assert.Contains("), 0, 9)", module);
        Assert.Contains("), 0, 10)", module);
        Assert.Contains("value.length >= 3 && value.length <= 16", module);
    }

    [Fact]
    public void CompilesExactParsingFormatsToAnchoredPatterns()
    {
        var module = Compile(Worker("""
            var date = DateOnly.ParseExact("08/10/2026", "dd/MM/yyyy", CultureInfo.InvariantCulture);
            var time = TimeOnly.ParseExact("08:30:00.1234567", "O");
            var parsed = DateOnly.TryParseExact("2026-10-08", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result);
            return Response.Json(new { date, time, parsed, result });
            """));

        Assert.Contains("/^([0-9]{2})\\/([0-9]{2})\\/([0-9]{4})$/, [\"d\", \"M\", \"y\"]", module);
        Assert.Contains("/^([0-9]{2}):([0-9]{2}):([0-9]{2})\\.([0-9]{7})$/, [\"H\", \"m\", \"s\", \"f\"]", module);
        Assert.Contains("/^([0-9]{4})\\-([0-9]{2})\\-([0-9]{2})$/", module);
    }

    [Theory]
    [InlineData("return Response.Text(new DateOnly(2026, 1, 1).ToString());", "depends on the current culture")]
    [InlineData("return Response.Text($\"{new TimeOnly(9, 0)}\");", "depends on the current culture")]
    [InlineData("return Response.Text(new DateOnly(2026, 1, 1).ToString(\"yyyy-MM-dd\"));", "depends on the current culture")]
    [InlineData("return Response.Text(new DateOnly(2026, 1, 1).ToString(\"HH:mm\", CultureInfo.InvariantCulture));", "is not supported for 'System.DateOnly'")]
    [InlineData("return Response.Text(new TimeOnly(9, 0).ToString(\"yyyy\", CultureInfo.InvariantCulture));", "is not supported for 'System.TimeOnly'")]
    [InlineData("return Response.Json(DateOnly.Parse(\"2026-01-01\"));", "DateOnly.ParseExact")]
    [InlineData("return Response.Json(DateOnly.ParseExact(\"2026-01-01\", \"yyyy-MM-dd\"));", "pass CultureInfo.InvariantCulture")]
    [InlineData("return Response.Json(DateOnly.ParseExact(\"2026-01-01\", \"yyyy-M-d\", CultureInfo.InvariantCulture));", "is not supported for parsing")]
    [InlineData("return Response.Json(TimeOnly.ParseExact(\"9 PM\", \"h tt\", CultureInfo.InvariantCulture));", "is not supported for parsing")]
    [InlineData("var format = \"yyyy\"; return Response.Json(DateOnly.ParseExact(\"2026\", format, CultureInfo.InvariantCulture));", "needs a constant format")]
    [InlineData("return Response.Json(DateOnly.ParseExact(\"2026-01-01\", \"yyyy-MM-dd\", CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces));", "DateTimeStyles.None")]
    public void RejectsCultureDependentAndUnsupportedFormats(string body, string message)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile(Worker(body)));
        Assert.Contains(message, error.Message);
    }
}
