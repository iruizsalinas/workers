namespace Workers.Compiler.Tests;

// Inferred anonymous members follow their source's camelCase JSON name; explicit names are kept.
// One anonymous shape has one JavaScript storage name, so the choice must not depend on source order.
public sealed class AnonymousTypeNamingTests
{
    private const string User = "public sealed class User { public string Name { get; } = \"user\"; public int Age { get; } = 3; }\n";

    private static string Fetch(string body) => JavaScriptRunner.FetchJson("using Workers;\n" + User +
        "public static class Worker { [Fetch] public static Response Fetch(Request request) { var user = new User(); " + body + " } }").GetRawText();

    [Fact]
    public void InferredOnlyShapesUseCamelCaseKeys() =>
        Assert.Equal("""{"name":"user","age":3}""", Fetch("return Response.Json(new { user.Name, user.Age });"));

    [Fact]
    public void ExplicitSpellingsWinRegardlessOfSourceOrder()
    {
        var inferredFirst = Fetch("""var a = new { user.Name }; var b = new { Name = "explicit" }; return Response.Json(new[] { a, b });""");
        var explicitFirst = Fetch("""var b = new { Name = "explicit" }; var a = new { user.Name }; return Response.Json(new[] { a, b });""");
        Assert.Equal("""[{"Name":"user"},{"Name":"explicit"}]""", inferredFirst);
        Assert.Equal(inferredFirst, explicitFirst);
    }

    [Fact]
    public void SharedShapesStayReadableThroughEitherSpelling() => DifferentialRunner.Equal("""
        public sealed class User { public string Name { get; } = "user"; }
        public static class Test
        {
            public static object Run()
            {
                var user = new User();
                var flag = user.Name.Length > 2;
                var picked = flag ? new { user.Name } : new { Name = "other" };
                var items = new[] { new { Name = "explicit" }, new { user.Name } };
                return picked.Name + "|" + string.Join(",", items.Select(item => item.Name));
            }
        }
        """);
}
