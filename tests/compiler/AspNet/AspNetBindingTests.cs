namespace Workers.Compiler.Tests;

public sealed class AspNetBindingTests
{
    [Fact]
    public void BindsConstrainedRoutesAndQueryValues()
    {
        var script = TestCompiler.CompileAspNet(App("""
            app.MapGet("/users/{id:int}", (int id, string? filter, bool active) =>
                Results.Ok(new { id, filter, active }));
            """));

        Assert.Contains("(-?\\d+)", script);
        Assert.Contains("Number.parseInt(match[1], 10)", script);
        Assert.Contains("url.searchParams.get(\"filter\")", script);
        Assert.Contains("=== \"true\"", script);
    }

    [Theory]
    [InlineData("MapGet", "GET")]
    [InlineData("MapPost", "POST")]
    [InlineData("MapPut", "PUT")]
    [InlineData("MapDelete", "DELETE")]
    [InlineData("MapPatch", "PATCH")]
    public void SupportsStandardHttpMethods(string map, string method)
    {
        var script = TestCompiler.CompileAspNet(App($"app.{map}(\"/value\", () => Results.NoContent());"));
        Assert.Contains($"request.method === \"{method}\"", script);
    }

    private static string App(string routes) => $$"""
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        var builder = WebApplication.CreateSlimBuilder(args);
        var app = builder.Build();
        {{routes}}
        app.Run();
        """;
}
