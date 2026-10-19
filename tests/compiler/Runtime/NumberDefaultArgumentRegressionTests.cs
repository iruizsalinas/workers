namespace Workers.Compiler.Tests;

public sealed class NumberDefaultArgumentRegressionTests
{
    [Fact]
    public void ParseIntUsesTheDeclaredDefaultRadix()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request) => Response.Json(new
                {
                    omitted = Numbers.ParseInt("0x10"),
                    named = Numbers.ParseInt(text: "0x10"),
                    decimalValue = Numbers.ParseInt("0x10", 10),
                    inferred = Numbers.ParseInt("0x10", 0),
                    hexadecimal = Numbers.ParseInt("0x10", 16),
                    partial = Numbers.ParseInt("12px")
                });
            }
            """);
        Assert.Equal(0, result.GetProperty("omitted").GetDouble());
        Assert.Equal(0, result.GetProperty("named").GetDouble());
        Assert.Equal(0, result.GetProperty("decimalValue").GetDouble());
        Assert.Equal(16, result.GetProperty("inferred").GetDouble());
        Assert.Equal(16, result.GetProperty("hexadecimal").GetDouble());
        Assert.Equal(12, result.GetProperty("partial").GetDouble());
    }
}
