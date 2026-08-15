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

}
