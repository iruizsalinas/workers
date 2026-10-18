namespace Workers.Compiler.Tests;

public sealed class LoopPatternCaptureRegressionTests
{
    [Theory]
    [InlineData("while (++next is var value && value <= 3)")]
    [InlineData("while (Next(out var value))")]
    [InlineData("for (; ++next is var value && value <= 3;)")]
    [InlineData("for (; ++next is var value && value <= 3; value += 10)")]
    public void ConditionCapturesKeepTheirIterationValues(string loop) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static int next;
            private static bool Next(out int value) { value = ++next; return value <= 3; }
            public static object Run()
            {
                var callbacks = new List<Func<int>>();
                {{loop}} callbacks.Add(() => value);
                return callbacks.Select(callback => callback()).ToArray();
            }
        }
        """);

    [Fact]
    public void ForConditionCapturesPreserveIncrementorsAndNestedContinues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var next = 0;
                var callbacks = new List<Func<int>>();
                var trace = "";
                for (var i = 0; ++next is var value && value <= 3; value += 10, i++)
                {
                    var inner = 0;
                    while (inner++ < 2)
                    {
                        if (inner == 1) continue;
                        callbacks.Add(() => value);
                    }
                    try
                    {
                        if (value == 2) continue;
                        callbacks.Add(() => value * 10 + i);
                    }
                    finally { trace += value; }
                }
                return new { values = callbacks.Select(callback => callback()).ToArray(), trace };
            }
        }
        """);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DoConditionCapturesKeepTheirIterationValues(bool useContinue) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static List<Func<int>> callbacks = new();
            private static bool Save(Func<int> callback) { callbacks.Add(callback); return true; }
            public static object Run()
            {
                var next = 0;
                do { next++; {{(useContinue ? "if (next == 2) continue;" : "")}} }
                while (next is var value && Save(() => value) && next < 3);
                return callbacks.Select(callback => callback()).ToArray();
            }
        }
        """);

    [Fact]
    public void ForInitializerOutCapturesRemainSharedWhileConditionCapturesAreFresh() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var next = 0;
                var callbacks = new List<Func<int>>();
                for (var ready = int.TryParse("5", out var parsed); ++next is var value && next <= 3; parsed++)
                    callbacks.Add(() => parsed * 10 + value);
                return callbacks.Select(callback => callback()).ToArray();
            }
        }
        """);
}
