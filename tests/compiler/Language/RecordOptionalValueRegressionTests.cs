namespace Workers.Compiler.Tests;

public sealed class RecordOptionalValueRegressionTests
{
    [Theory]
    [InlineData("System.Index", "row.Value.Value")]
    [InlineData("System.Range", "row.Value.Start.Value")]
    [InlineData("KeyValuePair<int, int>", "row.Value.Key")]
    public void PositionalRecordsCreateOmittedValueTypeDefaults(string type, string read) => DifferentialRunner.Equal($$"""
        public sealed record Row({{type}} Value = default);
        public static class Test
        {
            public static object Run()
            {
                var row = new Row();
                return {{read}};
            }
        }
        """);
}
