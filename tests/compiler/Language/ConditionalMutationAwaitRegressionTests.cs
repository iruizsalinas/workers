namespace Workers.Compiler.Tests;

public sealed class ConditionalMutationAwaitRegressionTests
{
    [Theory]
    [InlineData("holder.Value += missing?.Read(await Task.FromResult(1)) ?? 5;")]
    [InlineData("numbers[0] += missing?.Read(await Task.FromResult(1)) ?? 5;")]
    [InlineData("tuple.Item1 += missing?.Read(await Task.FromResult(1)) ?? 5;")]
    [InlineData("indexer[0] += missing?.Read(await Task.FromResult(1)) ?? 5;")]
    public void SkippedMutationAwaitsDoNotSuspendTheContainingMethod(string mutation) => DifferentialRunner.Equal($$"""
        public sealed class Box
        {
            public int Value;
            public int Read(int value) => value;
        }
        public sealed class Table
        {
            private int value = 3;
            public int this[int index] { get => value; set => this.value = value; }
        }
        public static class Test
        {
            private static string trace = "";
            private static int total;
            private static async Task Work()
            {
                Box missing = null;
                var holder = new Box { Value = 3 };
                var numbers = new[] { 3 };
                var tuple = (3, 4);
                var indexer = new Table();
                {{mutation}}
                total = holder.Value + numbers[0] + tuple.Item1 + indexer[0];
                trace += "done;";
            }
            public static async Task<object> Run()
            {
                var pending = Work();
                var before = trace;
                await pending;
                return new { before, after = trace, total };
            }
        }
        """);

    [Fact]
    public void BasePropertyMutationsDoNotSuspendForSkippedAwaits() => DifferentialRunner.Equal("""
        public class Base { public virtual int Value { get; set; } = 3; }
        public sealed class Derived : Base
        {
            public int Read(int value) => value;
            public async Task Update()
            {
                Derived missing = null;
                base.Value += missing?.Read(await Task.FromResult(1)) ?? 5;
                Test.Mark();
            }
        }
        public static class Test
        {
            private static string trace = "";
            public static void Mark() { trace += "done;"; }
            public static async Task<object> Run()
            {
                var value = new Derived();
                var pending = value.Update();
                var before = trace;
                await pending;
                return new { before, after = trace, value = value.Value };
            }
        }
        """);
}
