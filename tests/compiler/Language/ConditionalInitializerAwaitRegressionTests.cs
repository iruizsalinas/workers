namespace Workers.Compiler.Tests;

public sealed class ConditionalInitializerAwaitRegressionTests
{
    [Theory]
    [InlineData("new Box { Value = missing?.Read(await Task.FromResult(1)) }")]
    [InlineData("new Box() with { Value = missing?.Read(await Task.FromResult(1)) }")]
    [InlineData("new Holder { Items = { [0] = { Value = missing?.Read(await Task.FromResult(1)) } } }")]
    public void SkippedInitializerAwaitsDoNotSuspendTheContainingMethod(string expression) => DifferentialRunner.Equal($$"""
        public sealed record Box
        {
            public int? Value { get; set; }
            public int Read(int value) => value;
        }
        public sealed class Holder { public Items Items { get; } = new Items(); }
        public sealed class Items
        {
            private Box item = new Box();
            public Box this[int index] => item;
        }
        public static class Test
        {
            private static string trace = "";
            private static async Task Work()
            {
                Box missing = null;
                var result = {{expression}};
                trace += "done;";
            }
            public static async Task<object> Run()
            {
                var pending = Work();
                var before = trace;
                await pending;
                return new { before, after = trace };
            }
        }
        """);

    [Theory]
    [InlineData("new Box { Value = missing?.Read(await Task.FromResult(1)) }")]
    [InlineData("new Box() with { Value = missing?.Read(await Task.FromResult(1)) }")]
    public void SynchronousInitializerFailuresReachTheContainingCatchWithoutSuspending(string expression) => DifferentialRunner.Equal($$"""
        public sealed record Box
        {
            public int? Value { get => null; set => throw new InvalidOperationException("failure"); }
            public int Read(int value) => value;
        }
        public static class Test
        {
            private static string trace = "";
            private static async Task Work()
            {
                Box missing = null;
                try { var result = {{expression}}; }
                catch (InvalidOperationException) { trace += "caught;"; }
                trace += "done;";
            }
            public static async Task<object> Run()
            {
                var pending = Work();
                var before = trace;
                await pending;
                return new { before, after = trace };
            }
        }
        """);
}
