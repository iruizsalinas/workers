namespace Workers.Compiler.Tests;

public sealed class TupleOrdinalAliasRegressionTests
{
    [Fact]
    public void NamedTupleFieldsSupportOrdinalReadsAndWrites() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var pair = (First: 1, Second: 2);
                pair.Item1 = 9;
                return new { named = pair.First, ordinal = pair.Item1, second = pair.Item2 };
            }
        }
        """);
}
