namespace Workers.Compiler.Tests;

public sealed class ArithmeticTests
{
    [Fact]
    public void ChecksIntegerRemainderByZero()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text($"{Remainder(7, 2)}");

                private static int Remainder(int left, int right) => left % right;
            }
            """);

        Assert.Contains("function $workers$integerRemainder(left, right, unsigned)", module);
        Assert.Contains("if (right === 0) throw new RangeError", module);
        Assert.Contains("$workers$integerRemainder(left, right, false)", module);
    }

    [Fact]
    public void RejectsUserDefinedOperators()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Json(new { equal = new Key(1) == new Key(1) });
            }
            public sealed record Key(int Value);
            """));

        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("Key.operator ==", error.Message);
    }

    [Fact]
    public void PreservesShortCircuitBooleanOperators()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { both = true && false, either = true || false });
            }
            """);

        Assert.Contains("both: true && false", module);
        Assert.Contains("either: true || false", module);
    }

    [Fact]
    public void PreservesInt32ArithmeticAndDivisionSemantics()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text($"{Calculate(7, 2)}");

                private static int Calculate(int left, int right) =>
                    (left / right) + (left * right);
            }
            """);

        Assert.Contains("function $workers$integerDivide(left, right, unsigned)", module);
        Assert.Contains("$workers$integerDivide(left, right, false)", module);
        Assert.Contains("Math.imul(left, right)", module);
        Assert.Contains("| 0", module);
    }

    [Fact]
    public void PreservesNullAsEmptyForStringConcatenation()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text(Join(null, "value"));

                private static string Join(string? left, string? right) => left + right;
            }
            """);

        Assert.Contains("return (left ?? \"\") + (right ?? \"\");", module);
    }


    [Fact]
    public void RoundsSinglePrecisionArithmeticLikeCSharp()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text($"{Add(0.1f, 0.2f)}");

                private static float Add(float left, float right) => left + right;
            }
            """);

        Assert.Contains("return Math.fround(left + right);", module);
    }

    [Fact]
    public void PreservesUnaryNumericSemantics()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Json(new { integer = Negate(int.MinValue), single = NegateSingle(0.1f) });

                private static int Negate(int value) => -value;
                private static float NegateSingle(float value) => -value;
            }
            """);

        Assert.Contains("return (-value) | 0;", module);
        Assert.Contains("return Math.fround(-value);", module);
    }

    [Fact]
    public void RejectsUnsupportedWideUnaryArithmetic()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) => Response.Text(Negate(1u).ToString());
                private static long Negate(uint value) => -value;
            }
            """));

        Assert.Contains("WRK108", error.Message);
    }

    [Fact]
    public void PromotesCharactersToUtf16CodeUnitsForArithmetic()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Json(Calculate('C', 'A'));

                private static object Calculate(char value, char other) => new
                {
                    literalAdd = 'A' + 1,
                    literalDifference = 'B' - 'A',
                    variableAdd = value + 5,
                    variableDifference = value - other
                };
            }
            """);

        Assert.Contains("(\"A\").charCodeAt(0) + 1", module);
        Assert.Contains("(\"B\").charCodeAt(0) - (\"A\").charCodeAt(0)", module);
        Assert.Contains("(value).charCodeAt(0) + 5", module);
        Assert.Contains("(value).charCodeAt(0) - (other).charCodeAt(0)", module);
    }

    [Fact]
    public void ContinuesToRejectCharacterCasts()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx) =>
                    Response.Text(((char)65).ToString());
            }
            """));

        Assert.Contains("WRK101", error.Message);
    }
}
