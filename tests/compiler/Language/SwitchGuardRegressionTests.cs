namespace Workers.Compiler.Tests;

public sealed class SwitchGuardRegressionTests
{
    [Fact]
    public void ConsecutiveSwitchStatementsCanReuseGuardVariableNames() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var selector = 0;
                var total = 0;
                switch (selector)
                {
                    case 0 when int.TryParse("3", out var number): total += number; break;
                }
                switch (selector)
                {
                    case var number: total += number; break;
                }
                return total;
            }
        }
        """);

    [Fact]
    public void SwitchStatementInputVariablesRemainAvailableAfterTheStatement() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                switch (int.TryParse("7", out var number))
                {
                    case true: break;
                }
                return number;
            }
        }
        """);

    [Fact]
    public void SwitchStatementGuardsCanReuseOutVariableNames() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var selector = 0;
                switch (selector)
                {
                    case 0 when int.TryParse("bad", out var number): return number;
                    case var other when int.TryParse("7", out var number): return number;
                    default: return -1;
                }
            }
        }
        """);

    [Fact]
    public void SwitchStatementGuardsKeepPatternAndOutVariablesSeparate() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Func<string> saved;
            private static bool Save(Func<string> read) { saved = read; return false; }
            public static object Run()
            {
                object input = "first";
                switch (input)
                {
                    case string value when Save(() => value): return "unused";
                    case var other when int.TryParse("7", out var value):
                        return new { captured = saved(), number = value };
                    default: return "missing";
                }
            }
        }
        """);

    [Fact]
    public void SwitchInputOutVariablesRemainAvailableAfterTheExpression() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int Read(out string text) { text = "input"; return 0; }
            public static object Run()
            {
                var selected = Read(out var text) switch { _ => text };
                return new { selected, text };
            }
        }
        """);

    [Fact]
    public void AsyncGuardOutVariablesKeepIndependentCapturedValues() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var readers = new List<Func<int>>();
                foreach (var input in new[] { 1, 2 })
                {
                    Func<int> read = input switch
                    {
                        _ when int.TryParse(await Task.FromResult(input.ToString()), out var parsed) => () => parsed,
                        _ => () => -1
                    };
                    readers.Add(read);
                }
                return readers.Select(read => read()).ToArray();
            }
        }
        """);

    [Fact]
    public void SwitchExpressionGuardsCanDeclarePatternVariables() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                object input = "matched";
                var selector = 0;
                return selector switch { 0 when input is string text => text, _ => "missing" };
            }
        }
        """);

    [Fact]
    public void SwitchExpressionGuardsCanReuseOutVariableNames() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var selector = 0;
                return selector switch
                {
                    0 when int.TryParse("bad", out var number) => number,
                    _ when int.TryParse("7", out var number) => number,
                    _ => -1
                };
            }
        }
        """);

    [Fact]
    public void SwitchExpressionResultsCanDeclarePatternVariables() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                object input = "matched";
                var selector = 0;
                return selector switch { 0 => input is string text ? text : "missing", _ => "other" };
            }
        }
        """);
}
