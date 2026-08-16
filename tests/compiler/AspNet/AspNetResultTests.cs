namespace Workers.Compiler.Tests;

public sealed class AspNetResultTests
{
    [Theory]
    [InlineData("Results.Ok(new { value = 1 })", "status: 200")]
    [InlineData("Results.BadRequest(new { error = \"bad\" })", "status: 400")]
    [InlineData("Results.NotFound()", "status: 404")]
    [InlineData("Results.NoContent()", "status: 204")]
    [InlineData("Results.Conflict(new { id = 1 })", "status: 409")]
    [InlineData("Results.Unauthorized()", "status: 401")]
    [InlineData("Results.Text(\"hello\")", "text/plain")]
    [InlineData("Results.Json(new { value = 1 }, statusCode: 202)", "Response.json({ value: 1 }, { status: 202 })")]
    public void LowersCommonResultHelpers(string result, string expected)
    {
        var script = TestCompiler.CompileAspNet(App($"app.MapGet(\"/\", () => {result});"));
        Assert.Contains(expected, script);
    }

    [Fact]
    public void PreservesConditionalResultBranches()
    {
        var script = TestCompiler.CompileAspNet(App("""
            app.MapDelete("/items/{id:int}", (int id) =>
                id == 1 ? Results.NoContent() : Results.NotFound(new { id }));
            """));

        Assert.Contains("? new Response(null, { status: 204 }) : Response.json", script);
    }

    private static string App(string route) => $$"""
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        var builder = WebApplication.CreateSlimBuilder(args);
        var app = builder.Build();
        {{route}}
        app.Run();
        """;
}
