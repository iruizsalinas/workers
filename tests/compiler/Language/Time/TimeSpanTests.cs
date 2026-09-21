namespace Workers.Compiler.Tests;

public sealed class TimeSpanTests
{
    [Fact]
    public void LowersConstructionFactoriesComponentsAndTotals()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var value = new TimeSpan(1, 2, 3, 4, 5);
                    var shortValue = new TimeSpan(2, 3, 4);
                    var factory = TimeSpan.FromDays(1.5).Add(TimeSpan.FromMinutes(2));
                    return Response.Json(new {
                        value.Days, value.Hours, value.Minutes, value.Seconds, value.Milliseconds,
                        value.TotalDays, value.TotalHours, value.TotalMinutes,
                        value.TotalSeconds, value.TotalMilliseconds, shortValue, factory,
                        TimeSpan.Zero, TimeSpan.MinValue, TimeSpan.MaxValue
                    });
                }
            }
            """);

        Assert.Contains("function $workers$timeSpan(milliseconds)", module);
        Assert.Contains("$workers$timeSpan(", module);
        Assert.Contains("* 86400000", module);
        Assert.Contains("* 60000", module);
        Assert.Contains("Math.trunc((value) / 86400000)", module);
        Assert.Contains("Math.trunc(value) % 1000", module);
        Assert.Contains("$workers$timeSpanLimit", module);
        Assert.Contains("Math.trunc(milliseconds * 10000) / 10000", module);
    }

    [Fact]
    public void LowersArithmeticComparisonAndDateInteraction()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var first = TimeSpan.FromHours(2);
                    var second = TimeSpan.FromMinutes(30);
                    var combined = first + second;
                    combined -= TimeSpan.FromMinutes(5);
                    var start = DateTimeOffset.UnixEpoch;
                    var end = start.Add(combined);
                    return Response.Json(new {
                        combined, negated = -second, duration = (-second).Duration(),
                        difference = end - start, shifted = end - second,
                        ordered = first > second, equal = first == second,
                        compare = first.CompareTo(second), staticCompare = TimeSpan.Compare(first, second),
                        instanceEquals = first.Equals(second), staticEquals = TimeSpan.Equals(first, second)
                    });
                }
            }
            """);

        Assert.Contains("$workers$timeSpan((first) + (second))", module);
        Assert.Contains("$workers$timeSpanNegate(second)", module);
        Assert.Contains("$workers$timeSpanDuration(", module);
        Assert.Contains("$workers$dateTimeAddMilliseconds(start, combined)", module);
        Assert.Contains("new Date(end).getTime() - new Date(start).getTime()", module);
        Assert.Contains("$workers$timeSpanCompare(first, second)", module);
    }

    [Theory]
    [InlineData("new TimeSpan(1L)")]
    [InlineData("TimeSpan.FromTicks(10)")]
    [InlineData("TimeSpan.FromMicroseconds(1)")]
    [InlineData("TimeSpan.Zero.Ticks")]
    public void RejectsApisThatNeedSubMillisecondPrecision(string expression)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json({{expression}});
            }
            """));

        Assert.StartsWith("WRK105:", error.Message);
    }
}

