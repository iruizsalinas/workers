namespace Workers.Compiler.Tests;

public sealed class UsingDeclarationCaptureRegressionTests
{
    [Theory]
    [InlineData(false, "Open(item, out var value)")]
    [InlineData(true, "Open(item, out var value)")]
    [InlineData(false, "item is var value ? new Lease() : null")]
    [InlineData(true, "item is var value ? new Lease() : null")]
    public void UsingDeclarationLocalsAreCapturedWithinTheirBlock(bool asynchronous, string initializer) => DifferentialRunner.Equal($$"""
        public sealed class Lease : IDisposable, IAsyncDisposable
        {
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
        public static class Test
        {
            private static Lease Open(int input, out int value) { value = input; return new Lease(); }
            public static {{(asynchronous ? "async Task<object>" : "object")}} Run()
            {
                var callbacks = new List<Func<int>>();
                foreach (var item in new[] { 1, 2, 3 })
                {
                    {{(asynchronous ? "await " : "")}}using var lease = {{initializer}};
                    callbacks.Add(() => value);
                }
                return callbacks.Select(callback => callback()).ToArray();
            }
        }
        """);
}
