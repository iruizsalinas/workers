namespace Workers.Compiler.Tests;

public sealed class NumericApiTests
{
    [Fact]
    public void EmitsStrictInvariantParsingAndFormatting()
    {
        var module = Compile("""
            using System.Globalization;
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    var signed = int.Parse(" -42 ");
                    var unsigned = uint.Parse("4294967295");
                    var single = float.Parse("1.25e2");
                    var number = double.Parse("-0.5");
                    var flag = bool.Parse("TrUe");
                    return Response.Json(new {
                        signed, unsigned, single, number, flag,
                        decimalText = signed.ToString(provider: CultureInfo.InvariantCulture, format: "D5"),
                        hex = signed.ToString("X", CultureInfo.InvariantCulture),
                        unsignedHex = unsigned.ToString("x8", CultureInfo.InvariantCulture),
                        fixedSingle = single.ToString("F1", CultureInfo.InvariantCulture),
                        fixedDouble = number.ToString("F3", CultureInfo.InvariantCulture)
                    });
                }
            }
            """);

        Assert.Contains("function $workers$numericParse(input, kind)", module);
        Assert.Contains("$workers$numericParse(\" -42 \", 0)", module);
        Assert.Contains("$workers$numericParse(\"4294967295\", 1)", module);
        Assert.Contains("$workers$numericParse(\"1.25e2\", 2)", module);
        Assert.Contains("$workers$numericParse(\"-0.5\", 3)", module);
        Assert.Contains("$workers$numericParse(\"TrUe\", 4)", module);
        Assert.Contains("function $workers$numericFormat(value, format, kind, precision)", module);
    }

    [Fact]
    public void EmitsCheckedMathAndFloat32Results()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) => Response.Json(new {
                    absolute = Math.Abs(-42), clamped = Math.Clamp(12, 0, 10),
                    rounded = Math.Round(2.5), precise = Math.Round(1.2345, 2),
                    floor = Math.Floor(1.9), ceiling = Math.Ceiling(1.1), truncated = Math.Truncate(-1.9),
                    power = Math.Pow(2, 8), root = Math.Sqrt(9), logarithm = Math.Log(8, 2),
                    exponential = Math.Exp(1), sine = Math.Sin(0.5), cosine = Math.Cos(0.5),
                    tangent = Math.Tan(0.5), atan = Math.Atan2(1, 1), sign = Math.Sign(-2.0),
                    floatRoot = MathF.Sqrt(9), floatRound = MathF.Round(2.5f),
                    pi = Math.PI, tau = Math.Tau, floatPi = MathF.PI
                });
            }
            """);

        Assert.Contains("$workers$mathAbsInt((-42) | 0)", module);
        Assert.Contains("$workers$mathClamp(12, 0, 10)", module);
        Assert.Contains("$workers$mathRound(2.5, 0, 15)", module);
        Assert.Contains("Math.log(8) / Math.log(2)", module);
        Assert.Contains("Math.fround(Math.sqrt(9))", module);
        Assert.Contains("Math.PI * 2", module);
    }

    [Theory]
    [InlineData("int.TryParse(\"1\", out var value)")]
    [InlineData("long.Parse(\"1\")")]
    [InlineData("decimal.Parse(\"1\")")]
    [InlineData("42.ToString(\"D4\")")]
    [InlineData("42.ToString(\"N\", CultureInfo.InvariantCulture)")]
    public void RejectsOutBasedWideOrCultureSensitiveNumericApis(string operation)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using System.Globalization;
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
