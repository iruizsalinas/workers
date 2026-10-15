namespace Workers.Compiler.Tests;

public sealed class ForeachConversionTests
{
    [Fact]
    public void ForeachConvertsElementsToTheIterationVariableType() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                int[] values = [16777217, 16777219];
                var rounded = new List<float>();
                foreach (float value in values) rounded.Add(value);
                var codes = new List<int>();
                foreach (int code in "AZ") codes.Add(code);
                var wrapped = new List<byte>();
                foreach (byte value in new[] { 300, -1 }) wrapped.Add(value);
                return new { rounded, codes, wrapped };
            }
        }
        """);

    [Fact]
    public void AwaitForeachConvertsElementsToTheIterationVariableType() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var rounded = new List<float>();
                await foreach (float value in Values()) rounded.Add(value);
                return rounded;
            }
            private static async IAsyncEnumerable<int> Values()
            {
                await Task.FromResult(0);
                yield return 16777217;
                yield return 16777219;
            }
        }
        """);

    [Fact]
    public void ForeachPreservesNullableConversionsAndPerIterationCaptures() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int disposed;
            public static object Run()
            {
                var optional = new List<float?>();
                foreach (float? value in new int?[] { null, 16777217 }) optional.Add(value);
                var widened = new List<long>();
                foreach (long value in new[] { 1, int.MaxValue }) widened.Add(value);
                var captures = new List<Func<int>>();
                foreach (int code in "AZ") captures.Add(() => code);
                var caught = false;
                try { foreach (int value in Values()) { } }
                catch (System.Exception) { caught = true; }
                return new { optional, widened, captures = captures.Select(read => read()).ToArray(), caught, disposed };
            }
            private static IEnumerable<int?> Values()
            {
                try { yield return 1; yield return null; }
                finally { disposed++; }
            }
        }
        """);

    [Fact]
    public void RejectsCheckedNarrowingForeachConversionsInsteadOfWrapping()
    {
        var compilation = global::WorkerCompiler.CreateCompilation([
            Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request)
                {
                    foreach (byte value in new[] { 300 }) { }
                    return new Response("ok");
                }
            }
            """)
        ], [typeof(global::Workers.Response).Assembly.Location]);
        var error = Assert.Throws<NotSupportedException>(() => global::WorkerCompiler.Compile(
            compilation.WithOptions(compilation.Options.WithOverflowChecks(true))));
        Assert.Contains("WRK108", error.Message);
    }
}
