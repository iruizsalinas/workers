namespace Workers.Compiler.Tests;

public sealed class IndexerArgumentCopyRegressionTests
{
    [Theory]
    [InlineData("table[key] += 1;")]
    [InlineData("table[key]++;")]
    [InlineData("table[key] &= 1;")]
    public void CompoundIndexerCallsGiveEachAccessorItsOwnTupleArgument(string mutation) => DifferentialRunner.Equal($$"""
        public sealed class Table
        {
            public int Recorded;
            public int this[(int, int) key]
            {
                get { key.Item1 = 9; return 3; }
                set { Recorded = key.Item1 * 10 + value; }
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var table = new Table();
                var key = (1, 2);
                {{mutation}}
                return new { recorded = table.Recorded, first = key.Item1 };
            }
        }
        """);
}
