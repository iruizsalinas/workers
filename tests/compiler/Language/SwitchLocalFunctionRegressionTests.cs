namespace Workers.Compiler.Tests;

public sealed class SwitchLocalFunctionRegressionTests
{
    [Fact]
    public void LocalFunctionsAreVisibleAcrossSwitchSections() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run() => new[] { Read(0), Read(1), Read(2) };
            private static int Read(int input)
            {
                switch (input)
                {
                    case 0: return Shared();
                    case 1:
                        int Shared() => input + 10;
                        return Shared();
                    default: return Shared();
                }
            }
        }
        """);

    [Fact]
    public void CaseGuardsCanCallFunctionsDeclaredInOtherSections() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run() => new[] { Read(-1), Read(2) };
            private static int Read(int input)
            {
                switch (input)
                {
                    case var item when Accept(item): return Convert(item);
                    case var item:
                        bool Accept(int value) => value > 0;
                        T Convert<T>(T value) => value;
                        return Convert(item - 1);
                }
            }
        }
        """);

    [Fact]
    public void SwitchLocalFunctionDelegatesRetainPerIterationCaptures() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var callbacks = new List<Func<int>>();
                for (int index = 0; index < 3; index++)
                {
                    var captured = index;
                    switch (index)
                    {
                        case 0: callbacks.Add(Read); break;
                        case 1:
                            int Read() => captured;
                            callbacks.Add(Read);
                            break;
                        default: callbacks.Add(Read); break;
                    }
                }
                return new { values = callbacks.Select(callback => callback()).ToArray(), distinct = callbacks[0] != callbacks[1] };
            }
        }
        """);
}
