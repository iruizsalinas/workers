namespace Workers.Compiler.Tests;

public sealed class AsyncAnonymousMethodRegressionTests
{
    [Theory]
    [InlineData("async delegate { await Task.FromResult(0); return 7; }")]
    [InlineData("async delegate () { await Task.FromResult(0); return 7; }")]
    public void AsyncAnonymousMethodsEmitAsyncFunctions(string method) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static async Task<object> Run()
            {
                Func<Task<int>> read = {{method}};
                return new { value = await read() };
            }
        }
        """);

    [Fact]
    public void AsyncAnonymousMethodsPreserveParametersAndCapturedValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var offset = 2;
                Func<int, Task<int>> read = async delegate (int value)
                {
                    await Task.FromResult(0);
                    return value + offset;
                };
                return new { value = await read(5) };
            }
        }
        """);

    [Fact]
    public void AsyncAnonymousMethodsMayOmitUnusedParameters() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                Func<int, int, Task<int>> read = async delegate
                {
                    await Task.FromResult(0);
                    return 7;
                };
                return new { value = await read(1, 2) };
            }
        }
        """);
}
