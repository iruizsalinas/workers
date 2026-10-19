namespace Workers.Compiler.Tests;

public sealed class AdapterOutStorageRegressionTests
{
    [Theory]
    [InlineData("values[0]")]
    [InlineData("values[^1]")]
    [InlineData("pair.Item1")]
    [InlineData("shared")]
    public void TryMethodsWriteThroughOrdinaryOutStorage(string target) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static int shared;
            public static object Run()
            {
                var values = new[] { 1 };
                var pair = (1, 2);
                var found = int.TryParse("7", out {{target}});
                return new { found, element = values[0], field = pair.Item1, shared };
            }
        }
        """);

    [Fact]
    public void OutStorageIsValidatedBeforeTheTryMethodRuns() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var queue = new Queue<int>();
                queue.Enqueue(7);
                var values = new[] { 1 };
                var caught = false;
                try { queue.TryDequeue(out values[2]); }
                catch (IndexOutOfRangeException) { caught = true; }
                return new { caught, count = queue.Count };
            }
        }
        """);
}
