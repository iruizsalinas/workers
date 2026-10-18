namespace Workers.Compiler.Tests;

public sealed class SetterValueCopyRegressionTests
{
    [Theory]
    [InlineData("box.Value")]
    [InlineData("box[0]")]
    [InlineData("Box.StaticValue")]
    [InlineData("contract.Value")]
    public void SetterParameterMutationsDoNotChangeTheAssignmentResult(string target) => DifferentialRunner.Equal($$"""
        public interface IBox
        {
            (int, int) Value { set { value.Item1 = 9; Test.Mark(value.Item1); } }
        }
        public sealed class DefaultBox : IBox { }
        public sealed class Box
        {
            public (int, int) Value { set { value.Item1 = 9; Test.Mark(value.Item1); } }
            public (int, int) this[int index] { set { value.Item1 = 9; Test.Mark(value.Item1); } }
            public static (int, int) StaticValue { set { value.Item1 = 9; Test.Mark(value.Item1); } }
        }
        public static class Test
        {
            private static int observed;
            public static void Mark(int value) { observed = value; }
            public static object Run()
            {
                var box = new Box();
                IBox contract = new DefaultBox();
                var input = (1, 2);
                var result = {{target}} = input;
                return new { input = input.Item1, result = result.Item1, observed };
            }
        }
        """);

    [Theory]
    [InlineData("box.Value")]
    [InlineData("box[0]")]
    public void SetterMutationsDoNotChangeDeconstructionResults(string target) => DifferentialRunner.Equal($$"""
        public sealed class Box
        {
            public (int, int) Value { set { value.Item1 = 9; } }
            public (int, int) this[int index] { set { value.Item1 = 9; } }
        }
        public static class Test
        {
            public static object Run()
            {
                var box = new Box();
                var input = (1, 2);
                var ignored = 0;
                var result = ({{target}}, ignored) = (input, 0);
                return new { input = input.Item1, result = result.Item1.Item1 };
            }
        }
        """);

    [Fact]
    public void DefaultInterfacePropertiesForwardBothAccessors() => DifferentialRunner.Equal("""
        public interface IBox
        {
            int Read();
            void Write(int value);
            int Value { get => Read(); set => Write(value); }
        }
        public sealed class Box : IBox
        {
            private int value;
            public int Read() => value;
            public void Write(int input) { value = input; }
        }
        public static class Test
        {
            public static object Run()
            {
                IBox box = new Box();
                box.Value = 4;
                box.Value += 3;
                return box.Value;
            }
        }
        """);
}
