namespace Workers.Compiler.Tests;

public sealed class PatternValueCopyRegressionTests
{
    [Theory]
    [InlineData("var copy")]
    [InlineData("{ } copy")]
    [InlineData("(var left, var right) copy")]
    public void TuplePatternVariablesHaveIndependentValueStorage(string pattern) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                var pair = (First: 1, Second: 2);
                if (pair is {{pattern}}) copy.First = 9;
                return new { first = pair.First, copied = copy.First };
            }
        }
        """);

    [Fact]
    public void SwitchSubjectsKeepTheirTupleValueWhenGuardsMutateTheOriginal() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var pair = (1, 2);
                bool Change() { pair.Item1 = 9; return false; }
                var expression = pair switch { _ when Change() => 0, (1, 2) => 1, _ => 2 };
                pair = (1, 2);
                var statement = 0;
                switch (pair)
                {
                    case var ignored when Change(): break;
                    case (1, 2): statement = 1; break;
                    default: statement = 2; break;
                }
                return new { expression, statement, first = pair.Item1 };
            }
        }
        """);
}
