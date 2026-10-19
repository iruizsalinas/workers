namespace Workers.Compiler.Tests;

public sealed class RecordParamsRegressionTests
{
    [Theory]
    [InlineData("sealed record")]
    [InlineData("readonly record struct")]
    [InlineData("record struct")]
    [InlineData("record")]
    public void PositionalRecordsPackExpandedParamsAndPreserveExplicitArrays(string kind) => DifferentialRunner.Equal($$"""
        public {{kind}} Batch(string Name = "default", params int[] Values);
        public static class Test
        {
            private static string trace = "";
            private static int Next(int value) { trace += value; return value; }
            public static object Run()
            {
                int[] values = [3, 4];
                var expanded = new Batch("batch", Next(1), Next(2));
                var empty = new Batch("empty");
                var defaults = new Batch(Values: []);
                var normal = new Batch(Values: values, Name: "normal");
                normal.Values[0] = 9;
                return new { name = expanded.Name, items = expanded.Values, empty = empty.Values.Length,
                    defaultName = defaults.Name, original = values, normal = normal.Name, trace };
            }
        }
        """);

    [Theory]
    [InlineData("Name()")]
    [InlineData("await Task.FromResult(Name())")]
    public void RecordParamsEvaluateArgumentsBeforeInitializers(string initializer) => DifferentialRunner.Equal($$"""
        public sealed record Batch(string Name, params int[] Values);
        public static class Test
        {
            private static string trace = "";
            private static int Next(int value) { trace += value; return value; }
            private static string Name() { trace += "initializer"; return "changed"; }
            public static async Task<object> Run()
            {
                var batch = new Batch("start", Next(1), Next(2)) { Name = {{initializer}} };
                return new { name = batch.Name, items = batch.Values, trace };
            }
        }
        """);
}
