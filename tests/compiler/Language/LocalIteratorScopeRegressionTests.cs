namespace Workers.Compiler.Tests;

public sealed class LocalIteratorScopeRegressionTests
{
    [Theory]
    [InlineData("box?.Add(await Task.FromResult(0)) ?? 0")]
    [InlineData("box.Id switch { 1 => await Task.FromResult(1), _ => await Task.FromResult(2) }")]
    public void ConcurrentLocalIteratorsKeepIndependentExpressionTemporaries(string expression) => DifferentialRunner.Equal($$"""
        public sealed class Box
        {
            public int Id { get; set; }
            public int Add(int value) => Id + value;
        }
        public static class Test
        {
            private static int created;
            public static async Task<object> Run()
            {
                async IAsyncEnumerable<int> Values()
                {
                    var box = new Box { Id = ++created };
                    yield return {{expression}};
                }
                var sequence = Values();
                return await Task.WhenAll(ReadAsync(sequence), ReadAsync(sequence));
            }
            private static async Task<int> ReadAsync(IAsyncEnumerable<int> sequence)
            {
                var total = 0;
                await foreach (var value in sequence) total += value;
                return total;
            }
        }
        """);
}
