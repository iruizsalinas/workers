namespace Workers.Compiler.Tests;

public sealed class AspNetCompatibilityTests
{
    [Fact]
    public void SupportsBindingAttributesHeadersAndRequestMetadata()
    {
        var script = TestCompiler.CompileAspNet(App("""
            app.MapGet("/items/{id:int}", (
                [FromRoute] int id,
                [FromQuery(Name = "q")] string? query,
                [FromHeader(Name = "x-token")] string token,
                HttpRequest request) =>
                Results.Ok(new { id, query, token, request.Method, request.Path }));
            """, "using Microsoft.AspNetCore.Mvc;"));

        Assert.Contains("request.headers.get(\"x-token\")", script);
        Assert.Contains("url.searchParams.get(\"q\")", script);
        Assert.Contains("request.method", script);
        Assert.Contains("url.pathname", script);
    }

    [Fact]
    public void SupportsSourceDeclaredMethodGroups()
    {
        var script = TestCompiler.CompileAspNet("""
            using Microsoft.AspNetCore.Builder;
            using Microsoft.AspNetCore.Http;
            var builder = WebApplication.CreateSlimBuilder(args);
            var app = builder.Build();
            app.MapGet("/items/{id:int}", GetItem);
            app.Run();
            static IResult GetItem(int id) => Results.Ok(new { id });
            """);

        Assert.Contains("Number.parseInt(match[1], 10)", script);
        Assert.Contains("Response.json({ id: p0 }", script);
    }

    [Fact]
    public void RejectsMiddlewareInsteadOfIgnoringIt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => TestCompiler.CompileAspNet(App("""
            app.Use(async (context, next) => await next());
            app.MapGet("/", () => "ok");
            """)));

        Assert.StartsWith("WRK203:", exception.Message);
    }

    [Fact]
    public void EmitsBadRequestForMissingRequiredSimpleValues()
    {
        var script = TestCompiler.CompileAspNet(App("app.MapGet(\"/\", (int page) => page);"));
        Assert.Contains("title: \"Bad Request\", status: 400", script);
    }

    private static string App(string routes, string extraUsing = "") => $$"""
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        {{extraUsing}}
        var builder = WebApplication.CreateSlimBuilder(args);
        var app = builder.Build();
        {{routes}}
        app.Run();
        """;
}
