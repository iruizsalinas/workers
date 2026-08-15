namespace Workers.Compiler.Tests;

public sealed class ArithmeticTests
{
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
