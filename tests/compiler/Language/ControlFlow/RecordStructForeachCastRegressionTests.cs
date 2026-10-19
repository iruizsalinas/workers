namespace Workers.Compiler.Tests;

public sealed class RecordStructForeachCastRegressionTests
{
    [Fact]
    public void AsyncForeachCastsBoxedRecordsToNullableIterationVariables() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            private static async IAsyncEnumerable<object?> Source()
            {
                await Task.FromResult(0);
                yield return null;
                yield return new Point(3, 4);
            }
            public static async Task<object> Run()
            {
                var values = new List<Point?>();
                await foreach (Point? value in Source()) values.Add(value);
                return values.Select(value => value.HasValue ? value.Value.X : -1).ToArray();
            }
        }
        """);

    [Fact]
    public void ForeachRejectsNullForNonNullableRecordStructs() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var caught = false;
                try { foreach (Point value in new object?[] { null }) { } }
                catch (NullReferenceException) { caught = true; }
                return caught;
            }
        }
        """);

    [Theory]
    [InlineData("Point", "new object[] { new Point(1, 2), new Point(3, 4) }")]
    [InlineData("Point?", "new object?[] { null, new Point(3, 4) }")]
    public void ForeachCastsBoxedRecordsToValueIterationVariables(string iterationType, string source) => DifferentialRunner.Equal($$"""
        public record struct Point(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var values = new List<Point?>();
                foreach ({{iterationType}} value in {{source}}) values.Add(value);
                return values.Select(value => value.HasValue ? value.Value.X : -1).ToArray();
            }
        }
        """);

    [Fact]
    public void ForeachRejectsWrongRecordTypes() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public record struct Other(int X, int Y);
        public static class Test
        {
            public static object Run()
            {
                var caught = false;
                try { foreach (Point value in new object[] { new Other(1, 2) }) { } }
                catch (InvalidCastException) { caught = true; }
                return caught;
            }
        }
        """);
}
