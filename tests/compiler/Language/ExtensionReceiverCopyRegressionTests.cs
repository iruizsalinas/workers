namespace Workers.Compiler.Tests;

public sealed class ExtensionReceiverCopyRegressionTests
{
    [Theory]
    [InlineData("pair.Change(9)")]
    [InlineData("pair.Change<int>(replacement: 9)")]
    [InlineData("Extensions.Change(pair, 9)")]
    public void TupleExtensionReceiversArePassedByValue(string invocation) => DifferentialRunner.Equal($$"""
        public static class Extensions
        {
            public static T Change<T>(this (T First, int Second) value, T replacement)
            {
                value.First = replacement;
                return value.First;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var pair = (First: 1, Second: 2);
                var changed = {{invocation}};
                return new { changed, first = pair.First, second = pair.Second };
            }
        }
        """);
}
