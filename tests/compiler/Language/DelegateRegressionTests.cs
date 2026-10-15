namespace Workers.Compiler.Tests;

public sealed class DelegateRegressionTests
{
    [Fact]
    public void InstanceMethodGroupConversionRejectsNullReceiversImmediately() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            private static Box Current() { calls++; return null; }
            public static object Run()
            {
                var caught = false;
                try { Func<int> read = Current().Read; }
                catch (System.Exception) { caught = true; }
                return new { caught, calls };
            }
        }
        public sealed class Box { public int Read() => 1; }
        """);

    [Fact]
    public void InstanceCallsEvaluateArgumentsBeforeCheckingForNull() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            private static Box Current() { trace += "receiver;"; return null; }
            private static int Argument() { trace += "argument;"; return 1; }
            public static object Run()
            {
                var caught = false;
                try { var result = Current().Read(Argument()); }
                catch (System.Exception) { caught = true; }
                return new { caught, trace };
            }
        }
        public sealed class Box { public int Read(int value) => value; }
        """);
}
