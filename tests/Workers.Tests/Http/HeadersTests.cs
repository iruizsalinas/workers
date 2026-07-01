using Xunit;

namespace Workers.Tests;

public sealed class HeadersTests
{
    [Fact]
    public void SetReplacesExistingHeaderCaseInsensitively()
    {
        var headers = new Headers()
            .Append("content-type", "text/plain")
            .Set("Content-Type", "application/json");

        Assert.Equal("application/json", headers.Get("CONTENT-TYPE"));
        Assert.Single(headers.GetAll("content-type"));
    }

    [Fact]
    public void AppendPreservesMultipleValues()
    {
        var headers = new Headers()
            .Append("set-cookie", "a=1")
            .Append("Set-Cookie", "b=2");

        Assert.Equal(["a=1", "b=2"], headers.GetAll("SET-cookie"));
    }

    [Fact]
    public void GetAllReturnsSnapshot()
    {
        var headers = new Headers()
            .Append("set-cookie", "a=1");

        var values = headers.GetAll("set-cookie");
        headers.Append("set-cookie", "b=2");

        Assert.Equal(["a=1"], values);
        Assert.Equal(["a=1", "b=2"], headers.GetAll("set-cookie"));
    }

    [Fact]
    public void TryGetAndGetRequiredReadHeadersCaseInsensitively()
    {
        var headers = new Headers()
            .Append("accept", "text/html")
            .Append("Accept", "application/json");

        Assert.True(headers.TryGet("ACCEPT", out var value));
        Assert.Equal("text/html, application/json", value);
        Assert.Equal("text/html, application/json", headers.GetRequired("accept"));

        Assert.False(headers.TryGet("missing", out var missing));
        Assert.Null(missing);
        Assert.Throws<WorkersException>(() => headers.GetRequired("missing"));
    }

    [Fact]
    public void CreateBuildsHeadersFromTuples()
    {
        var headers = Headers.Create(
            ("x-test", "one"),
            ("set-cookie", "a=1"),
            ("set-cookie", "b=2"));

        Assert.Equal("one", headers.GetRequired("X-Test"));
        Assert.Equal(["a=1", "b=2"], headers.GetAll("Set-Cookie"));
    }

    [Fact]
    public void CloneCreatesIndependentHeaderCollection()
    {
        var original = Headers.Create(("x-test", "one"), ("set-cookie", "a=1"));

        var clone = original.Clone();
        original.Set("x-test", "changed");
        clone.Append("set-cookie", "b=2");

        Assert.Equal("changed", original.Get("x-test"));
        Assert.Equal(["a=1"], original.GetAll("set-cookie"));
        Assert.Equal("one", clone.Get("x-test"));
        Assert.Equal(["a=1", "b=2"], clone.GetAll("set-cookie"));
    }

    [Fact]
    public void RejectsInvalidHeaderNames()
    {
        var headers = new Headers();

        Assert.Throws<ArgumentException>(() => headers.Set("bad name", "value"));
        Assert.Throws<ArgumentException>(() => headers.Append("bad name", "value"));
        Assert.Throws<ArgumentException>(() => headers.Get("bad name"));
        Assert.Throws<ArgumentException>(() => headers.GetAll("bad name"));
        Assert.Throws<ArgumentException>(() => headers.Contains("bad name"));
        Assert.Throws<ArgumentException>(() => headers.Delete("bad name"));
    }

    [Fact]
    public void RejectsInvalidHeaderValues()
    {
        var headers = new Headers();

        Assert.Throws<ArgumentException>(() => headers.Set("x-test", "bad\nvalue"));
        Assert.Throws<ArgumentException>(() => headers.Append("x-test", "bad\rvalue"));
        Assert.Throws<ArgumentException>(() => headers.Set("x-test", "bad\0value"));
    }
}
