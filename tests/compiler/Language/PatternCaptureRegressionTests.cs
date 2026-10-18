namespace Workers.Compiler.Tests;

public sealed class PatternCaptureRegressionTests
{
    [Fact]
    public void SwitchExpressionArmsKeepIndependentPatternCaptures() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static System.Func<int> saved;
            private static bool Save(System.Func<int> read) { saved = read; return false; }
            public static object Run()
            {
                var input = 3;
                var selected = input switch
                {
                    var value when Save(() => value) => 0,
                    var value => ++value
                };
                return new { selected, captured = saved() };
            }
        }
        """);

    [Fact]
    public void SwitchStatementSectionsKeepIndependentPatternCaptures() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static System.Func<int> saved;
            private static bool Save(System.Func<int> read) { saved = read; return false; }
            public static object Run()
            {
                var input = 3;
                switch (input)
                {
                    case var value when Save(() => value): return "unused";
                    case var value:
                        ++value;
                        return new { selected = value, captured = saved() };
                }
            }
        }
        """);
}
