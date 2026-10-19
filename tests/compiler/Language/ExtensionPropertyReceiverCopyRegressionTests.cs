namespace Workers.Compiler.Tests;

public sealed class ExtensionPropertyReceiverCopyRegressionTests
{
    [Theory]
    [InlineData("pair.Value += 3;")]
    [InlineData("pair.Value++;")]
    [InlineData("++pair.Value;")]
    public void ReadModifyWritePassesAnIndependentReceiverToEachExtensionAccessor(string mutation) => DifferentialRunner.Equal($$"""
        public static class Extensions
        {
            public static int Observed;
            extension((int First, int Second) pair)
            {
                public int Value
                {
                    get { pair.First++; return pair.First; }
                    set { Observed = pair.First * 10 + value; }
                }
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var pair = (First: 1, Second: 2);
                {{mutation}}
                return new { first = pair.First, observed = Extensions.Observed };
            }
        }
        """);

    [Fact]
    public void PropertyPatternsPassValueReceiversByValue() => DifferentialRunner.Equal("""
        public static class Extensions
        {
            extension((int First, int Second) pair)
            {
                public int Value { get { pair.First++; return pair.First; } }
                public int Original => pair.First;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var pair = (First: 1, Second: 2);
                var matched = pair is { Value: 2, Original: 1 };
                return new { matched, first = pair.First };
            }
        }
        """);
}
