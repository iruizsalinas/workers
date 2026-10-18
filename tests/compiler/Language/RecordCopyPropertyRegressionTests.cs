namespace Workers.Compiler.Tests;

public sealed class RecordCopyPropertyRegressionTests
{
    [Theory]
    [InlineData("public int Value => 7;")]
    [InlineData("public int Value { get => 7; init { Test.Mark(); } }")]
    public void CustomRecordCopiesDoNotAssignComputedPositionalProperties(string property) => DifferentialRunner.Equal($$"""
        public sealed record Item(int Value)
        {
            {{property}}
            private Item(Item source) { }
        }
        public static class Test
        {
            private static int calls;
            public static void Mark() { calls++; }
            public static object Run()
            {
                var original = new Item(3);
                var copy = original with { };
                return new { original = original.Value, copied = copy.Value, calls };
            }
        }
        """);

    [Fact]
    public void CustomRecordCopiesLeaveStaticAutoPropertiesInStaticStorage() => DifferentialRunner.Equal("""
        public sealed record Item
        {
            public static int Count { get; set; } = 5;
            public int Value { get; init; }
            public Item(int value) { Value = value; }
            private Item(Item source) { Value = source.Value; }
        }
        public static class Test
        {
            public static object Run()
            {
                var original = new Item(3);
                var copied = original with { };
                return new { original = original.Value, copied = copied.Value, count = Item.Count };
            }
        }
        """);

    [Fact]
    public void CustomRecordCopiesSupportExplicitPositionalFields() => DifferentialRunner.Equal("""
        public sealed record Item(int Value)
        {
            public int Value = Value;
            private Item(Item source) { Value = source.Value; }
        }
        public static class Test
        {
            public static object Run()
            {
                var original = new Item(3);
                var copy = original with { };
                return new { original = original.Value, copied = copy.Value };
            }
        }
        """);
}
