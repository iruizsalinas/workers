namespace Workers.Compiler.Tests;

public sealed class IdentifierRegressionTests
{
    [Fact]
    public void QueryRangeVariablesCannotCaptureNativeGlobals()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() => (from Number in new[] { 1, 2 }
                    let Math = Number + 1
                    where Math > 1
                    select Number * Math).ToArray();
            }
            """);
    }

    [Fact]
    public void StrictJavascriptBindingNamesRemainValidCSharpIdentifiers()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    int eval = 2, arguments = 3;
                    Func<int, int> add = arguments => arguments + eval;
                    return add(arguments);
                }
            }
            """);
    }

    [Fact]
    public void UserLocalsDoNotShadowCompilerRuntimeGlobals()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                private static int Product(int Math) => Math * 3;
                public static object Run() {
                    int Math = 2;
                    int Number = 3;
                    int Object = 4;
                    int Array = 5;
                    int undefined = 6;
                    var values = new int[Array];
                    return new { product = Math * Number, nested = Product(Math), count = values.Length,
                        sum = Object + undefined };
                }
            }
            """);
    }
}
