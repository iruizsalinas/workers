using Xunit;

namespace Workers.Tests;

public sealed class CorsTests
{
    [Fact]
    public void EmptyCorsDoesNotModifyHeaders()
    {
        var headers = new Headers().Set("x-test", "ok");

        Cors.Empty.ApplyTo(headers);

        Assert.Single(headers);
        Assert.Equal("ok", headers.Get("x-test"));
    }

    [Fact]
    public void CorsAppliesConfiguredHeaders()
    {
        var cors = new Cors()
            .WithCredentials()
            .WithMaxAge(600)
            .WithOrigins(["https://app.example", "https://admin.example"])
            .WithMethods(["GET", "POST"])
            .WithAllowedHeaders(["authorization", "content-type"])
            .WithExposedHeaders(["x-request-id"]);

        var response = cors.ApplyTo(Response.Empty(204));

        Assert.Equal("true", response.Headers.Get("access-control-allow-credentials"));
        Assert.Equal("600", response.Headers.Get("access-control-max-age"));
        Assert.Equal("https://app.example,https://admin.example", response.Headers.Get("access-control-allow-origin"));
        Assert.Equal("GET,POST", response.Headers.Get("access-control-allow-methods"));
        Assert.Equal("authorization,content-type", response.Headers.Get("access-control-allow-headers"));
        Assert.Equal("x-request-id", response.Headers.Get("access-control-expose-headers"));
    }

    [Fact]
    public void ResponseAppliesCorsHeaders()
    {
        var cors = new Cors()
            .WithOrigins(["https://app.example"])
            .WithMethods(["GET"]);

        var response = Response.Empty(204).WithCors(cors);

        Assert.Equal("https://app.example", response.Headers.Get("access-control-allow-origin"));
        Assert.Equal("GET", response.Headers.Get("access-control-allow-methods"));
    }

    [Fact]
    public void ResponseBuilderAppliesCorsHeaders()
    {
        var cors = new Cors()
            .WithCredentials()
            .WithAllowedHeaders(["authorization"]);

        var response = Response.Builder(204)
            .WithCors(cors)
            .Build();

        Assert.Equal("true", response.Headers.Get("access-control-allow-credentials"));
        Assert.Equal("authorization", response.Headers.Get("access-control-allow-headers"));
    }

    [Fact]
    public void CorsBuilderDoesNotMutatePreviousInstance()
    {
        var empty = new Cors();
        var configured = empty.WithOrigins(["https://app.example"]);

        Assert.Empty(empty.Origins);
        Assert.Equal(["https://app.example"], configured.Origins);
    }

    [Fact]
    public void CorsRejectsNegativeMaxAge()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Cors().WithMaxAge(-1));
    }
}
