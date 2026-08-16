namespace Workers.Compiler.Tests;

public sealed class AspNetRoutingTests
{
    [Fact]
    public void EmitsNativeRoutesFromTopLevelMinimalApi()
    {
        var script = TestCompiler.CompileAspNet("""
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            var builder = WebApplication.CreateSlimBuilder(args);
            var app = builder.Build();
            app.MapGet("/", () => "hello");
            app.MapPost("/items", (Item item) => Results.Created($"/items/{item.Name}", item));
            app.Run();
            record Item(string Name);
            """);

        Assert.Contains("export default { fetch }", script);
        Assert.Contains("request.method === \"GET\"", script);
        Assert.Contains("await request.json()", script);
        Assert.Contains("status: 201", script);
        Assert.DoesNotContain("WebApplication", script);
    }

    [Fact]
    public void EmitsNotFoundAndMethodNotAllowedSeparately()
    {
        var script = TestCompiler.CompileAspNet(Basic("app.MapGet(\"/items\", () => \"ok\");"));

        Assert.Contains("status: 405", script);
        Assert.Contains("headers: { allow:", script);
        Assert.Contains("status: 404", script);
    }

    private static string Basic(string routes) => $$"""
        using Microsoft.AspNetCore.Builder;
        var builder = WebApplication.CreateSlimBuilder(args);
        var app = builder.Build();
        {{routes}}
        app.Run();
        """;
}
