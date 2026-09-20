namespace Workers.Compiler.Tests;

public sealed class DateTimeTests
{
    [Fact]
    public void UsesSymbolsAndReceiverForDateTimeOffsetRoundTripFormatting()
    {
        var module = Compile("""
            using Workers;
            using Clock = System.DateTimeOffset;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var captured = Clock.UtcNow;
                    return Response.Text(captured.ToString("O"));
                }
            }
            """);

        Assert.Contains("let captured = new Date();", module);
        Assert.Contains("new Date(captured).toISOString()", module);
        Assert.Contains("\"$1\" + \"0000+00:00\"", module);
        Assert.DoesNotContain("new Date().toISOString()", module);
    }

    [Fact]
    public void RejectsUnsupportedDateTimeOffsetFormattingInsteadOfChangingMeaning()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text(DateTimeOffset.UtcNow.ToString());
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }

    [Fact]
    public void LowersUtcCalendarConstructionComponentsAndArithmetic()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var value = new DateTimeOffset(2024, 2, 29, 23, 58, 57, 123, TimeSpan.Zero);
                    var next = value.AddHours(1).AddMinutes(2).AddMilliseconds(3);
                    return Response.Json(new
                    {
                        value.Year, value.Month, value.Day, value.DayOfWeek,
                        value.Hour, value.Minute, value.Second, value.Millisecond,
                        date = value.Date,
                        next
                    });
                }
            }
            """);

        Assert.Contains("function $workers$dateTimeOffset(year, month, day, hour, minute, second, millisecond = 0)", module);
        Assert.Contains("$workers$dateTimeOffset(2024, 2, 29, 23, 58, 57, 123)", module);
        Assert.Contains("new Date(value).getUTCFullYear()", module);
        Assert.Contains("new Date(value).getUTCMonth() + 1", module);
        Assert.Contains("new Date(value).getUTCDay()", module);
        Assert.Contains("new Date(new Date(value).setUTCHours(0, 0, 0, 0))", module);
        Assert.Contains("* 3600000", module);
        Assert.Contains("* 60000", module);
    }

    [Fact]
    public void DateTimeOffsetComparisonsUseInstantValuesAndPreserveEvaluationOrder()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var first = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    var second = new DateTimeOffset(2024, 1, 2, 0, 0, 0, TimeSpan.Zero);
                    return Response.Json(new { equal = first == second, ordered = first < second });
                }
            }
            """);

        Assert.Contains(".getTime() === new Date(", module);
        Assert.Contains(".getTime() < new Date(", module);
        Assert.Equal(2, module.Split(")(first, second)").Length - 1);
    }

    [Fact]
    public void RejectsDateTimeOffsetConstructorsWithNonZeroOffsets()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var value = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
                    return Response.Json(value);
                }
            }
            """));

        Assert.StartsWith("WRK108:", error.Message);
    }

    [Fact]
    public void LowersCalendarClampingAndUnixConversions()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var value = new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero);
                    var previous = DateTimeOffset.FromUnixTimeMilliseconds(-1);
                    var epoch = DateTimeOffset.FromUnixTimeSeconds(0);
                    return Response.Json(new
                    {
                        month = value.AddMonths(1),
                        year = value.AddYears(1),
                        previousSeconds = previous.ToUnixTimeSeconds(),
                        epochMilliseconds = epoch.ToUnixTimeMilliseconds()
                    });
                }
            }
            """);

        Assert.Contains("function $workers$dateTimeAddMonths(input, months)", module);
        Assert.Contains("$workers$dateTimeAddMonths(value, 1)", module);
        Assert.Contains("$workers$dateTimeAddMonths(value, (1) * 12)", module);
        Assert.Contains("$workers$dateTimeFromUnixTime((-1) | 0, false)", module);
        Assert.Contains("$workers$dateTimeFromUnixTime(0, true)", module);
        Assert.Contains("Math.floor(new Date(previous).getTime() / 1000)", module);
    }

    [Fact]
    public void MakesDateTimeOffsetDateComposableAsDateTime()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var first = new DateTimeOffset(2024, 2, 29, 12, 30, 15, 123, TimeSpan.Zero).Date;
                    var second = new DateTimeOffset(2024, 2, 29, 18, 0, 0, TimeSpan.Zero).Date;
                    return Response.Json(new
                    {
                        first.Year, first.Month, first.Day, first.DayOfWeek,
                        first.Hour, first.Minute, first.Second, first.Millisecond,
                        nested = first.Date,
                        equal = first == second,
                        text = first.ToString("O")
                    });
                }
            }
            """);

        Assert.Contains("new Date(first).getUTCFullYear()", module);
        Assert.Contains("new Date(new Date(first).setUTCHours(0, 0, 0, 0))", module);
        Assert.Contains(".getTime() === new Date(", module);
        Assert.Contains("replace(/(\\.\\d{3})Z$/, \"$1\" + \"0000\")", module);
        Assert.DoesNotContain("\"0000+00:00\")", module.Split("text:")[1]);
    }

}
