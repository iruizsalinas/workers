namespace Workers.Compiler.Tests;

public sealed class AsyncConditionalAccessTests
{
    [Fact]
    public void ConcurrentEnumerationsKeepIndependentAwaitedReceivers() => DifferentialRunner.Equal("""
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
                var box = new Box { Id = ++created };
                yield return box?.Add(await Task.FromResult(0)) ?? 0;
            }
        }
        public sealed class Box
        {
            public int Id { get; set; }
            public int Add(int value) => Id + value;
        }
        """);

    [Fact]
    public void AsyncEntrypointsAndInstanceMethodsDeclareTheirOwnTemporaries()
    {
        var actual = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request)
                {
                    var box = new Box();
                    var entrypoint = box?.Add(await Task.FromResult(2));
                    return Response.Json(new { entrypoint, method = await box.ReadAsync() });
                }
            }
            public sealed class Box
            {
                public int Add(int value) => value + 10;
                public async Task<int?> ReadAsync() => this?.Add(await Task.FromResult(3));
            }
            """);
        Assert.Equal(12, actual.GetProperty("entrypoint").GetInt32());
        Assert.Equal(13, actual.GetProperty("method").GetInt32());
    }

    [Fact]
    public void ConditionalCallsSupportAwaitedArgumentsAndSkipNullBranches() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            private static Box missing = null;
            public static async Task<object> Run()
            {
                var receiver = new Box();
                var selected = receiver?.Add(Mark(1), await Next(), Mark(3));
                var skipped = missing?.Add(Mark(4), await Next(), Mark(5));
                var nested = receiver?.Child?.Add(Mark(6), await Next(), Mark(7));
                return new { selected, skipped, nested, trace };
            }
            private static int Mark(int value) { trace += value; return value; }
            private static async Task<int> Next() { trace += "await;"; return await Task.FromResult(2); }
        }
        public sealed class Box
        {
            public Box Child { get; set; }
            public int Add(int first, int second, int third) => first + second + third;
        }
        """);

    [Fact]
    public void ConditionalCallsPreserveReturnedTasksAndAsyncLambdaScopes() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Task<int> original = Task.FromResult(42);
            public static Task<int> Original => original;
            private static string trace = "";
            public static async Task<object> Run()
            {
                var receiver = new Box();
                var selected = receiver?.Choose(await Task.FromResult(1));
                Func<Task<int?>> expression = async () => receiver?.Add(await Task.FromResult(2));
                Func<Task<int?>> block = async () => { return receiver?.Add(await Task.FromResult(3)); };
                var expressionValue = await expression();
                var blockValue = await block();
                var skipped = SkipAsync();
                var beforeAwait = trace;
                await skipped;
                return new { same = selected == original, value = await selected,
                    expressionValue, blockValue, beforeAwait };
            }
            private static async Task SkipAsync()
            {
                Box missing = null;
                var unused = missing?.Add(await Task.FromResult(1));
                trace += "done";
            }
        }
        public sealed class Box
        {
            public Task<int> Choose(int value) => Test.Original;
            public int Add(int value) => value + 10;
        }
        """);
}
