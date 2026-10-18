namespace Workers.Compiler.Tests;

public sealed class ExceptionFilterRegressionTests
{
    [Fact]
    public void SeparateFiltersCanDeclareOutVariablesWithTheSameName() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                try { throw new System.Exception("7"); }
                catch (System.Exception) when (int.TryParse("bad", out var value)) { return value; }
                catch (System.Exception error) when (int.TryParse(error.Message, out var value)) { return value; }
            }
        }
        """);

    [Fact]
    public void SeparateFiltersKeepIndependentCapturedPatternVariables() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static System.Func<string> saved;
            private static bool Save(System.Func<string> read) { saved = read; return false; }
            public static object Run()
            {
                try { throw new System.Exception("first"); }
                catch (System.Exception error) when (error.Message is var value && Save(() => value)) { return "unused"; }
                catch (System.Exception error) when (error.Message is var value)
                {
                    value = "second";
                    return new { saved = saved(), value };
                }
            }
        }
        """);
}
