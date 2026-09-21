namespace Workers.Compiler.Tests;

public sealed class DateTimeApiTests
{
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

    [Fact]
    public void UsesTheFormattedTypeForRoundTripInterpolation()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var offset = new DateTimeOffset(2024, 2, 29, 12, 0, 0, TimeSpan.Zero);
                    var date = offset.Date;
                    return Response.Json(new { offset = $"{offset:O}", date = $"{date:O}" });
                }
            }
            """);

        var projection = module.Split("return Response.json")[1];
        Assert.Contains("offset).toISOString().replace(/(\\.\\d{3})Z$/, \"$1\" + \"0000+00:00\")", projection);
        Assert.Contains("date).toISOString().replace(/(\\.\\d{3})Z$/, \"$1\" + \"0000\")", projection);
    }

    [Fact]
    public void LowersCalendarQueriesComparisonsAndUtcConversions()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var first = new DateTimeOffset(2024, 12, 31, 23, 0, 0, TimeSpan.Zero);
                    var second = first.AddHours(1);
                    var date = first.DateTime;
                    return Response.Json(new
                    {
                        offsetDay = first.DayOfYear,
                        dateDay = date.DayOfYear,
                        leap = DateTime.IsLeapYear(2024),
                        days = DateTime.DaysInMonth(2024, 2),
                        offsetCompare = DateTimeOffset.Compare(first, second),
                        dateCompare = DateTime.Compare(date, second.DateTime),
                        instanceCompare = first.CompareTo(second),
                        equal = first.Equals(second),
                        epoch = DateTimeOffset.UnixEpoch,
                        unspecified = first.DateTime,
                        utc = first.ToUniversalTime()
                    });
                }
            }
            """);

        Assert.Contains("$workers$dateTimeDayOfYear(first)", module);
        Assert.Contains("$workers$dateTimeIsLeapYear(2024)", module);
        Assert.Contains("$workers$dateTimeDaysInMonth(2024, 2)", module);
        Assert.Contains("$workers$dateTimeCompare(first, second)", module);
        Assert.Contains("$workers$dateTimeCompare(first, second) === 0", module);
        Assert.Contains("epoch: new Date(0)", module);
        Assert.Contains("unspecified: new Date(first)", module);
        Assert.Contains("utc: new Date(first)", module);
    }

    [Fact]
    public void EmitsOnlyRequestedDateTimeQueryHelpers()
    {
        var compareModule = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var first = DateTimeOffset.UnixEpoch;
                    return Response.Json(first.CompareTo(first));
                }
            }
            """);

        Assert.Contains("function $workers$dateTimeCompare(left, right)", compareModule);
        Assert.DoesNotContain("function $workers$dateTimeDayOfYear", compareModule);
        Assert.DoesNotContain("function $workers$dateTimeIsLeapYear", compareModule);
        Assert.DoesNotContain("function $workers$dateTimeDaysInMonth", compareModule);

        var daysModule = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Json(DateTime.DaysInMonth(2024, 2));
            }
            """);

        Assert.Contains("function $workers$dateTimeDaysInMonth(year, month)", daysModule);
        Assert.Contains("function $workers$dateTimeIsLeapYear(year)", daysModule);
        Assert.DoesNotContain("function $workers$dateTimeCompare", daysModule);
        Assert.DoesNotContain("function $workers$dateTimeDayOfYear", daysModule);
    }

}
