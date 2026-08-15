namespace Workers.Compiler.Tests;

public sealed class TextTests
{
    [Fact]
    public void EmitsJavascriptSafeLiteralValuesInsteadOfCSharpTokenText()
    {
        var module = Compile(""""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    var text = """raw ` ${ value } text""";
                    var number = 1.5f;
                    return Response.Text($"{text}:{number}");
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
                public static Response Fetch(Request request, Env env, Context ctx)
                {
                    string? value = null;
                    return Response.Text($"literal ${{ marker }} ` slash \\ {value}");
                }
            }
            """);

        Assert.Contains("\\` slash \\\\ ${value ?? \"\"}", module);
        Assert.Contains("value ?? \"\"", module);
    }

    [Fact]
    public void AllocatesLegalJavascriptNamesForEscapedCSharpIdentifiers()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request @default, Env env, Context ctx)
                {
                    var @delete = @default.Path;
                    return Response.Text(@delete);
                }
            }
            """);

        Assert.Contains("function $workers$fetch($workers$user$default, env, ctx)", module);
        Assert.Contains("let $workers$user$delete = new URL($workers$user$default.url).pathname;", module);
        Assert.DoesNotContain("@default", module);
    }

}
