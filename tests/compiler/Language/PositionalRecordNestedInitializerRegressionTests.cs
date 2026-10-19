namespace Workers.Compiler.Tests;

public sealed class PositionalRecordNestedInitializerRegressionTests
{
    [Theory]
    [InlineData("sealed record")]
    [InlineData("record struct")]
    [InlineData("readonly record struct")]
    public void PositionalRecordsSupportNestedCollectionInitializers(string declaration) => DifferentialRunner.Equal($$"""
        public {{declaration}} Bag(List<int> Values);
        public static class Test
        {
            public static object Run()
            {
                var values = new List<int> { 1 };
                var bag = new Bag(values) { Values = { 7, 9 } };
                return new { values = bag.Values.ToArray(), same = bag.Values == values };
            }
        }
        """);

    [Fact]
    public void PositionalRecordsInitializeExistingNestedObjects() => DifferentialRunner.Equal("""
        public sealed class Child { public int Value; }
        public sealed record Box(Child Child);
        public static class Test
        {
            public static object Run()
            {
                var child = new Child();
                var box = new Box(child) { Child = { Value = 7 } };
                return new { value = child.Value, same = box.Child == child };
            }
        }
        """);

    [Fact]
    public void AwaitedNestedInitializersUseTheConstructedRecord() => DifferentialRunner.Equal("""
        public sealed record Bag(List<int> Values);
        public static class Test
        {
            private static List<int> values = new List<int> { 1 };
            private static async Task<int> Next()
            {
                await Task.FromResult(0);
                values = new List<int> { 9 };
                return 7;
            }
            public static async Task<object> Run()
            {
                var bag = new Bag(values) { Values = { await Next() } };
                return new { stored = bag.Values.ToArray(), current = values.ToArray() };
            }
        }
        """);
}
