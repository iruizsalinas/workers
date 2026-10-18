namespace Workers.Compiler.Tests;

public sealed class GenericNullReceiverRegressionTests
{
    [Theory]
    [InlineData("public int Read<T>(T value) => 7;")]
    [InlineData("public async System.Threading.Tasks.Task<int> Read<T>(T value) { await System.Threading.Tasks.Task.CompletedTask; return 7; }")]
    [InlineData("public System.Collections.Generic.IEnumerator<int> Read<T>(T value) { yield return 7; }")]
    public void GenericInstanceCallsCheckNullAfterEvaluatingArguments(string method) => DifferentialRunner.Equal($$"""
        public sealed class Item
        {
            {{method}}
        }
        public static class Test
        {
            private static int calls;
            private static int Input() { calls++; return 3; }
            public static object Run()
            {
                Item item = null;
                var caught = false;
                try { item.Read<int>(Input()); }
                catch (System.NullReferenceException) { caught = true; }
                return new { caught, calls };
            }
        }
        """);
}
