namespace Workers.Compiler.Tests;

public sealed class TextEmissionTests
{
    [Fact]
    public void RawInterpolatedStringsPreserveLiteralBracePairs() => DifferentialRunner.Equal(""""
        public static class Test
        {
            public static object Run()
            {
                var value = 7;
                return new
                {
                    raw = $$$"""{{literal}} {{{value}}} ${{marker}}""",
                    escaped = $"{{{{literal}}}} {value} ${{{{marker}}}}"
                };
            }
        }
        """");

    [Fact]
    public void EmitsJavascriptSafeLiteralValuesInsteadOfCSharpTokenText()
    {
        var module = Compile(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var text = """raw ` ${ value } text""";
                    var number = 1.5f;
                    return new Response($"{text}:{number}");
                }
            }
            """");

        Assert.Contains("let text = \"raw \\u0060 ${ value } text\";", module);
        Assert.Contains("let number = 1.5;", module);
        Assert.DoesNotContain("1.5f", module);
    }

    [Fact]
    public void EscapesTemplateTextAndUsesEmptyTextForNullInterpolation()
    {
        var module = Compile("""
            #nullable enable
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    string? value = null;
                    return new Response($"literal ${{ marker }} ` slash \\ {value}");
                }
            }
            """);

        Assert.Contains("\\` slash \\\\ ${(value) ?? \"\"}", module);
        Assert.Contains("(value) ?? \"\"", module);
    }

    [Fact]
    public void AllocatesLegalJavascriptNamesForEscapedCSharpIdentifiers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request @default, Env env, ExecutionContext ctx)
                {
                    var @delete = new Url(@default.Url).Pathname;
                    return new Response(@delete);
                }
            }
            """);

        Assert.Contains("function $workers$fetch($workers$user$default, env, ctx)", module);
        Assert.Contains("let $workers$user$delete = new URL($workers$notNull($workers$user$default).url).pathname;", module);
        Assert.DoesNotContain("@default", module);
    }
}
