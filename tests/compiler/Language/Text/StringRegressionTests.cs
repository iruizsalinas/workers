namespace Workers.Compiler.Tests;

public sealed class StringRegressionTests
{
    [Fact]
    public void OrdinalComparisonModesUseBoundConstantValues()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private const StringComparison Ordinal = StringComparison.OrdinalIgnoreCase;
                public static object Run() => new {
                    equal = "a".Equals("A", Test.Ordinal),
                    contains = "Ada".Contains("ADA", comparisonType: Test.Ordinal),
                    literal = "a".Equals("A", (StringComparison)4)
                };
            }
            """);
    }

    [Fact]
    public void SplitReadsOptionsFromTheBoundNamedArgument()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private const StringSplitOptions Options = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;
                public static object Run() {
                    const string text = " a , , b ";
                    return new {
                        character = text.Split(options: Options, separator: ','),
                        sequence = text.Split(options: Options, separator: ","),
                        empty = text.Split(options: StringSplitOptions.None, separator: ',')
                    };
                }
            }
            """);
    }

    [Fact]
    public void JoinHandlesSingleExpandedArgument()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() => new {
                    single = string.Join("-", "hello"),
                    explicitArray = string.Join("-", new string[] { "hello" }),
                    empty = string.Join("-", new string[] { }),
                    nullValue = string.Join("-", (string)null),
                    named = string.Join(value: new string[] { "hello" }, separator: "-"),
                    characterSeparator = string.Join('-', "hello")
                };
            }
            """);
    }

    [Fact]
    public void JoinEvaluatesSeparatorBeforeValues()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private static string trace = "";
                private static string Separator() { trace += "s"; return "-"; }
                private static string[] Values() { trace += "v"; return new string[] { "a", "b" }; }
                public static object Run() {
                    var joined = string.Join(Separator(), Values());
                    return new { joined, trace };
                }
            }
            """);
    }
}
