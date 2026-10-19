namespace Workers.Compiler.Tests;

public sealed class ArraySpanNamedArgumentRegressionTests
{
    [Theory]
    [InlineData("MemoryExtensions.Contains<int>(value: Value(), span: Items())")]
    [InlineData("MemoryExtensions.IndexOf<int>(value: Value(), span: Items())")]
    [InlineData("MemoryExtensions.LastIndexOf<int>(value: Value(), span: Items())")]
    [InlineData("MemoryExtensions.SequenceEqual<int>(other: Other(), span: Items())")]
    [InlineData("MemoryExtensions.StartsWith<int>(value: Other(), span: Items())")]
    [InlineData("MemoryExtensions.EndsWith<int>(value: Other(), span: Items())")]
    public void NamedArgumentsAreEvaluatedInSourceOrder(string expression) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static string trace = "";
            private static int[] Items() { trace += "items;"; return [1, 2, 3]; }
            private static int Value() { trace += "value;"; return 2; }
            private static int[] Other() { trace += "other;"; return [1, 2]; }
            public static object Run()
            {
                var result = {{expression}};
                return new { result, trace };
            }
        }
        """);

    [Fact]
    public void EarlierNamedArgumentsCanReplaceTheArrayUsedByLaterArguments() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                int[] items = [1, 2];
                int Replace() { items = [3, 4]; return 2; }
                var found = MemoryExtensions.Contains<int>(value: Replace(), span: items);
                return new { found, items };
            }
        }
        """);
}
