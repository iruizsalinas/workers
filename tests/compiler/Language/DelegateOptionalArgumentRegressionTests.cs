namespace Workers.Compiler.Tests;

public sealed class DelegateOptionalArgumentRegressionTests
{
    [Theory]
    [InlineData("(int value = 7) => value")]
    [InlineData("(int value = 7) => { return value; }")]
    public void InferredLambdasHonorOmittedOptionalArguments(string lambda) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            public static object Run()
            {
                var read = {{lambda}};
                return new { omitted = read(), supplied = read(3) };
            }
        }
        """);

    [Fact]
    public void DelegateCallsUseTheDelegateDefaultsRatherThanTheTargetMethodDefaults() => DifferentialRunner.Equal("""
        public delegate int Read(int value = 9);
        public static class Test
        {
            private static int Echo(int value = 4) => value;
            public static object Run()
            {
                Read method = Echo;
                Read lambda = (int value = 7) => value;
                return new { method = method(), lambda = lambda(), explicitValue = method(3) };
            }
        }
        """);

    [Theory]
    [InlineData("read()")]
    [InlineData("read(first: 3)")]
    [InlineData("read(second: 3)")]
    public void DelegateCallsFillAllOmittedOptionalArguments(string invocation) => DifferentialRunner.Equal($$"""
        public delegate int Read(int first = 1, int second = 2);
        public static class Test
        {
            private static int Pair(int first, int second) => first * 10 + second;
            public static object Run()
            {
                Read read = Pair;
                return {{invocation}};
            }
        }
        """);

    [Fact]
    public void DelegateOptionalTupleDefaultsCreateTheDefaultValue() => DifferentialRunner.Equal("""
        public delegate int Read((int, int) pair = default);
        public static class Test
        {
            private static int First((int, int) pair) => pair.Item1;
            public static object Run()
            {
                Read read = First;
                return read();
            }
        }
        """);

    [Fact]
    public void NamedCallsFillOmittedTupleDefaultsWithValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int Pair(int first = 1, (int, int) pair = default, int last = 2) => first + pair.Item1 + last;
            public static object Run() => Pair(last: 3);
        }
        """);
}
