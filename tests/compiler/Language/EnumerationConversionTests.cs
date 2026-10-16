namespace Workers.Compiler.Tests;

public sealed class EnumerationConversionTests
{
    [Fact]
    public void TupleAndDictionaryEntryFormattingEvaluatesItsValueOnce() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            private static (int, int) Next() { calls++; return (calls, calls + 1); }
            private static KeyValuePair<string, int> Pair() { calls++; return new("key", calls); }
            public static object Run()
            {
                var tuple = $"{Next()}";
                var pair = $"{Pair()}";
                var explicitText = Pair().ToString();
                return new { tuple, pair, explicitText, calls };
            }
        }
        """);
    [Fact]
    public void ForeachBoxesDictionaryEntriesWithTheirValueFormatting() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var text = new List<string>();
                foreach (object value in new Dictionary<string, int> { ["one"] = 1 }) text.Add($"{value}");
                return text.ToArray();
            }
        }
        """);

    [Fact]
    public void ForeachSupportsStringCastsAndNullableTupleBoxing() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var text = new List<string>(); var caught = false;
                foreach (string? value in new object?[] { "ready", null }) text.Add(value ?? "nil");
                foreach (object? value in new (int, int)?[] { (1, 2), null }) text.Add($"{value}");
                try { foreach (string value in new object[] { new object() }) text.Add(value); }
                catch (InvalidCastException) { caught = true; }
                return new { text = text.ToArray(), caught };
            }
        }
        """);
    [Fact]
    public void ForeachObjectValuesKeepEnumRecordAndTupleFormatting() => DifferentialRunner.Equal("""
        public enum Status { Ready }
        public sealed record Row(int Value);
        public static class Test
        {
            public static object Run()
            {
                var text = new List<string>();
                foreach (object value in new[] { Status.Ready }) text.Add($"{value}");
                foreach (object value in new[] { new Row(3) }) text.Add($"{value}");
                foreach (object value in new[] { (1, 2) }) text.Add($"{value}");
                foreach (object? value in new Status?[] { null, Status.Ready }) text.Add($"{value}");
                return text.ToArray();
            }
        }
        """);

    [Fact]
    public void ForeachBoxedTuplesAreSnapshotsOfTheEnumeratedValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { (1, 2) }; var objects = new List<object>();
                foreach (object value in values) objects.Add(value);
                values[0].Item1 = 9;
                return new { text = $"{objects[0]}" };
            }
        }
        """);

    [Fact]
    public void ForeachReferenceConversionsCheckTheSelectedRuntimeType() => DifferentialRunner.Equal("""
        public sealed record Row(int Value);
        public static class Test
        {
            public static object Run()
            {
                var sum = 0; var caught = false;
                foreach (Row value in new object[] { new Row(2), new Row(3) }) sum += value.Value;
                try { foreach (Row value in new object[] { "bad" }) { sum += value.Value; } }
                catch (InvalidCastException) { caught = true; }
                return new { sum, caught };
            }
        }
        """);
}
