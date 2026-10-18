namespace Workers.Compiler.Tests;

public sealed class OutArgumentValueCopyRegressionTests
{
    [Theory]
    [InlineData("TryGetValue")]
    [InlineData("Remove")]
    public void DictionaryTryCallsCopyValueArgumentsBeforeEvaluatingOutReceivers(string method) => DifferentialRunner.Equal($$"""
        public sealed class Holder { public int Value; }
        public static class Test
        {
            private static (int, int) key = (1, 2);
            private static Holder holder = new Holder();
            private static Holder Output() { key.Item1 = 9; return holder; }
            public static object Run()
            {
                var values = new Dictionary<(int, int), int> { [key] = 7 };
                var found = values.{{method}}(key, out Output().Value);
                return new { found, value = holder.Value, first = key.Item1, count = values.Count };
            }
        }
        """);
}
