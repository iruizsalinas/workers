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
}
