namespace Workers.Compiler.Tests;

public sealed class AsyncSwitchRegressionTests
{
    [Fact]
    public void ConcurrentEnumerationsKeepIndependentAsyncSwitchState() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int created;
            public static async Task<object> Run()
            {
                var sequence = Values();
                return await Task.WhenAll(ReadAsync(sequence), ReadAsync(sequence));
            }
            private static async Task<int> ReadAsync(IAsyncEnumerable<int> sequence)
            {
                var total = 0;
                await foreach (var value in sequence) total += value;
                return total;
            }
            private static async IAsyncEnumerable<int> Values()
            {
                var next = ++created;
                yield return next switch
                {
                    var value when await Task.FromResult(true) => value,
                    _ => 0
                };
            }
        }
        """);

    [Fact]
    public void AsyncSwitchesPreserveTasksReturnedByUnawaitedArms() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Task<int> original = Task.FromResult(42);
            public static async Task<object> Run()
            {
                var selector = 1;
                Task<int> value = selector switch
                {
                    0 when await Task.FromResult(true) => Task.FromResult(7),
                    _ => original
                };
                return new { same = value == original, result = await value };
            }
        }
        """);

    [Fact]
    public void UnselectedAwaitedArmsDoNotSuspendSynchronousPathsOrExceptions() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            public static async Task<object> Run()
            {
                var selected = SelectAsync(1);
                var failed = SelectAsync(2);
                var beforeAwait = trace;
                await selected;
                await failed;
                return new { beforeAwait, trace };
            }
            private static async Task SelectAsync(int selector)
            {
                try
                {
                    var value = selector switch
                    {
                        0 => await Task.FromResult(7),
                        1 => 42,
                        _ => throw new System.Exception("failure")
                    };
                    trace += "selected;";
                }
                catch (System.Exception) { trace += "caught;"; }
            }
        }
        """);

    [Fact]
    public void AsyncSwitchesKeepPatternCapturesIndependentBetweenEvaluations() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var readers = new List<Func<int>>();
                foreach (var input in new[] { 1, 2 })
                {
                    Func<int> read = input switch
                    {
                        var value when await Task.FromResult(value > 0) => () => value,
                        _ => () => 0
                    };
                    readers.Add(read);
                }
                return readers.Select(read => read()).ToArray();
            }
        }
        """);
}
