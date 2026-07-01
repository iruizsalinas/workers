using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed partial class RequestTests
{
    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void RequestFactoriesUseExpectedMethod(string method)
    {
        var request = method switch
        {
            "GET" => Request.Get("https://example.com/value"),
            "HEAD" => Request.Head("https://example.com/value"),
            "POST" => Request.Post("https://example.com/value", Body.Text("value")),
            "PUT" => Request.Put("https://example.com/value", Body.Text("value")),
            "PATCH" => Request.Patch("https://example.com/value", Body.Text("value")),
            "DELETE" => Request.Delete("https://example.com/value"),
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(method, request.Method);
    }

    [Fact]
    public void CreateNormalizesMethodAndSetsBodyContentType()
    {
        var request = Request.Create(
            "https://example.com/value",
            "patch",
            Body.Json(new { ok = true }));

        Assert.Equal("PATCH", request.Method);
        Assert.Equal("application/json", request.Headers.Get("content-type"));
    }

    [Fact]
    public void CreateKeepsExplicitContentType()
    {
        var headers = new Headers()
            .Set("content-type", "application/merge-patch+json");

        var request = Request.Patch(
            "https://example.com/value",
            Body.Json(new { ok = true }),
            headers);

        Assert.Equal("application/merge-patch+json", request.Headers.Get("content-type"));
    }

    [Fact]
    public void CreateDoesNotMutateProvidedHeaders()
    {
        var headers = new Headers()
            .Set("x-test", "one");

        var request = Request.Post(
            "https://example.com/value",
            Body.Json(new { ok = true }),
            headers);

        headers.Set("x-test", "changed");

        Assert.Null(headers.Get("content-type"));
        Assert.Equal("application/json", request.Headers.Get("content-type"));
        Assert.Equal("one", request.Headers.Get("x-test"));
    }

    [Fact]
    public void ConstructorClonesProvidedHeaders()
    {
        var headers = new Headers()
            .Set("x-test", "one");

        var request = new Request(
            new Uri("https://example.com/value"),
            "GET",
            headers);

        headers.Set("x-test", "changed");

        Assert.Equal("one", request.Headers.Get("x-test"));
    }

    [Fact]
    public void WithHeaderSetsHeaderOnRequest()
    {
        var request = Request.Get("https://example.com/value")
            .WithHeader("x-test", "one");

        Assert.Equal("one", request.Headers.Get("x-test"));
    }

    [Fact]
    public void CloneCreatesIndependentRequestHeaders()
    {
        var original = Request.Post(
                "https://example.com/value",
                Body.Text("hello"))
            .WithHeader("x-test", "one");

        var clone = original.Clone();
        original.WithHeader("x-test", "changed");
        clone.WithHeader("x-clone", "yes");

        Assert.Equal("changed", original.Headers.Get("x-test"));
        Assert.Null(original.Headers.Get("x-clone"));
        Assert.Equal("one", clone.Headers.Get("x-test"));
        Assert.Equal("yes", clone.Headers.Get("x-clone"));
        Assert.Equal(original.Url, clone.Url);
        Assert.Equal("hello", clone.Text());
    }

    [Fact]
    public void HeaderHelpersAppendAndRemoveRequestHeaders()
    {
        var original = Request.Post(
                "https://example.com/value",
                Body.Text("hello"))
            .WithHeader("x-test", "one")
            .AppendHeader("x-repeat", "a")
            .AppendHeader("x-repeat", "b");

        var withoutRepeat = original.WithoutHeader("x-repeat");

        Assert.Equal(["a", "b"], original.Headers.GetAll("x-repeat"));
        Assert.Equal("one", withoutRepeat.Headers.Get("x-test"));
        Assert.Null(withoutRepeat.Headers.Get("x-repeat"));
        Assert.Equal("POST", withoutRepeat.Method);
        Assert.Equal("hello", withoutRepeat.Text());
        Assert.Equal(["a", "b"], original.Headers.GetAll("x-repeat"));
    }

    [Fact]
    public void RequestReadsBodyAsBytes()
    {
        var request = Request.Post(
            "https://example.com/value",
            Body.FromBytes([1, 2, 3]));

        Assert.Equal([1, 2, 3], request.Bytes().ToArray());
    }

    [Fact]
    public void WithVariantsReturnUpdatedRequests()
    {
        var original = Request.Post(
                "https://example.com/old",
                Body.Text("old"))
            .WithHeader("x-test", "one");

        var moved = original.WithUrl("https://example.com/new");
        var changedMethod = original.WithMethod("put");
        var changedBody = original.WithBody(Body.Text("new"));
        var replacementHeaders = new Headers().Set("x-replacement", "ok");
        var changedHeaders = original.WithHeaders(replacementHeaders);
        replacementHeaders.Set("x-replacement", "changed");
        original.Headers.Set("x-test", "changed");

        Assert.Equal("https://example.com/new", moved.Url.ToString());
        Assert.Equal("POST", moved.Method);
        Assert.Equal("one", moved.Headers.Get("x-test"));

        Assert.Equal("PUT", changedMethod.Method);
        Assert.Equal("old", changedMethod.Body.AsText());

        Assert.Equal("POST", changedBody.Method);
        Assert.Equal("new", changedBody.Body.AsText());
        Assert.Equal("text/plain; charset=utf-8", changedBody.Headers.Get("content-type"));

        Assert.Equal("ok", changedHeaders.Headers.Get("x-replacement"));
        Assert.Null(changedHeaders.Headers.Get("x-test"));
        Assert.Equal("text/plain; charset=utf-8", changedHeaders.Headers.Get("content-type"));
    }

    [Fact]
    public void UrlHelpersExposeAndRewriteRequestUrlParts()
    {
        var original = Request.Post(
                "https://api.example.com:8443/old/path?q=one",
                Body.Text("body"))
            .WithHeader("x-test", "one");

        Assert.Equal("https", original.Scheme);
        Assert.Equal("api.example.com", original.Host);
        Assert.Equal("api.example.com:8443", original.Authority);
        Assert.Equal("https://api.example.com:8443", original.Origin);
        Assert.Equal("/old/path", original.Path);
        Assert.Equal("/old/path?q=one", original.PathAndQuery);

        var path = original.WithPath("/new/path");
        var query = path.WithQuery("?tag=a&tag=b");
        var cleared = query.WithQuery(null);
        var pathAndQuery = original.WithPathAndQuery("/next?x=1");

        Assert.Equal("https://api.example.com:8443/new/path?q=one", path.Url.ToString());
        Assert.Equal("https://api.example.com:8443/new/path?tag=a&tag=b", query.Url.ToString());
        Assert.Equal(["a", "b"], query.QueryParameters.GetAll("tag"));
        Assert.Equal("https://api.example.com:8443/new/path", cleared.Url.ToString());
        Assert.Equal("https://api.example.com:8443/next?x=1", pathAndQuery.Url.ToString());
        Assert.Equal("POST", pathAndQuery.Method);
        Assert.Equal("one", pathAndQuery.Headers.Get("x-test"));
        Assert.Equal("body", pathAndQuery.Text());

        Assert.Throws<ArgumentException>(() => original.WithPath("relative"));
        Assert.Throws<ArgumentException>(() => original.WithPathAndQuery("relative?x=1"));
    }

    [Fact]
    public void QueryMutationHelpersUpdateIndividualParameters()
    {
        var original = Request.Post(
                "https://api.example.com/search?q=old&tag=a&tag=b&empty=",
                Body.Text("body"))
            .WithHeader("x-test", "one");

        var replaced = original.WithQueryParameter("q", "Ada Lovelace");
        var appended = replaced.AppendQueryParameter("tag", "c+d");
        var removed = appended.RemoveQueryParameter("empty");
        var cleared = removed.RemoveQueryParameter("tag").RemoveQueryParameter("q");

        Assert.Equal("old", original.QueryParameters.Get("q"));
        Assert.Equal("https://api.example.com/search?tag=a&tag=b&empty=&q=Ada+Lovelace", replaced.Url.ToString());
        Assert.Equal("Ada Lovelace", replaced.QueryParameters.Get("q"));
        Assert.Equal("https://api.example.com/search?tag=a&tag=b&empty=&q=Ada+Lovelace&tag=c%2Bd", appended.Url.ToString());
        Assert.Equal(["a", "b", "c+d"], appended.QueryParameters.GetAll("tag"));
        Assert.Equal("https://api.example.com/search?tag=a&tag=b&q=Ada+Lovelace&tag=c%2Bd", removed.Url.ToString());
        Assert.Equal("https://api.example.com/search", cleared.Url.ToString());
        Assert.Equal("POST", appended.Method);
        Assert.Equal("one", appended.Headers.Get("x-test"));
        Assert.Equal("body", appended.Text());

        Assert.Throws<ArgumentException>(() => original.WithQueryParameter("", "value"));
        Assert.Throws<ArgumentException>(() => original.AppendQueryParameter(" ", "value"));
        Assert.Throws<ArgumentException>(() => original.RemoveQueryParameter(""));
    }

    [Fact]
    public void WithBodyConveniencesUpdateBodyOwnedContentType()
    {
        var json = Request.Post(
                "https://example.com/value",
                Body.Text("old"))
            .WithJson(new JsonPayload { ClientId = "frontend" });
        var bytes = json.WithBytes([1, 2, 3], "application/custom");
        var empty = bytes.WithBody(Body.Empty);

        Assert.Equal("application/json", json.Headers.Get("content-type"));
        Assert.Equal("""{"clientId":"frontend"}""", json.Text());
        Assert.Equal("application/custom", bytes.Headers.Get("content-type"));
        Assert.Equal([1, 2, 3], bytes.Bytes().ToArray());
        Assert.True(empty.Body.IsEmpty);
        Assert.False(empty.Headers.Contains("content-type"));
    }

    [Fact]
    public void WithBodyConveniencesPreserveCustomContentType()
    {
        var request = Request.Post(
                "https://example.com/value",
                Body.Json(new { ok = true }),
                Headers.Create(("content-type", "application/problem+json")))
            .WithText("bad request")
            .WithJson(new JsonPayload { ClientId = "frontend" });

        Assert.Equal("application/problem+json", request.Headers.Get("content-type"));
        Assert.Equal("""{"clientId":"frontend"}""", request.Text());
    }

    [Fact]
    public void RequestBuilderCreatesConfiguredRequest()
    {
        var request = Request.Builder("https://example.com/old")
            .WithUrl("https://example.com/new?q=1")
            .WithMethod("post")
            .WithHeader("x-test", "one")
            .AppendHeader("x-repeat", "a")
            .AppendHeader("x-repeat", "b")
            .WithJson(new { ok = true })
            .Build();

        Assert.Equal("https://example.com/new?q=1", request.Url.ToString());
        Assert.Equal("POST", request.Method);
        Assert.Equal("1", request.QueryParameters.Get("q"));
        Assert.Equal("one", request.Headers.Get("x-test"));
        Assert.Equal(["a", "b"], request.Headers.GetAll("x-repeat"));
        Assert.Equal("application/json", request.Headers.Get("content-type"));
        Assert.True(request.Json<JsonElement>().GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void RequestBuilderRewritesUrlParts()
    {
        var request = Request.Builder("https://api.example.com/old?q=old")
            .WithPath("/new")
            .WithQuery("?q=1&tag=a")
            .Build();
        var pathAndQuery = Request.Builder("https://api.example.com/old?q=old")
            .WithPathAndQuery("/next?x=1")
            .Build();
        var cleared = Request.Builder("https://api.example.com/old?q=old")
            .WithQuery("")
            .Build();

        Assert.Equal("https://api.example.com/new?q=1&tag=a", request.Url.ToString());
        Assert.Equal("1", request.QueryParameters.Get("q"));
        Assert.Equal("https://api.example.com/next?x=1", pathAndQuery.Url.ToString());
        Assert.Equal("https://api.example.com/old", cleared.Url.ToString());

        Assert.Throws<ArgumentException>(() => Request.Builder("https://api.example.com").WithPath("relative"));
        Assert.Throws<ArgumentException>(() => Request.Builder("https://api.example.com").WithPathAndQuery("relative?x=1"));
    }

    [Fact]
    public void RequestBuilderMutatesIndividualQueryParameters()
    {
        var request = Request.Builder("https://api.example.com/search?q=old&tag=a")
            .WithQueryParameter("q", "new value")
            .AppendQueryParameter("tag", "b")
            .RemoveQueryParameter("missing")
            .Build();
        var withoutTags = Request.Builder(request.Url.ToString())
            .RemoveQueryParameter("tag")
            .Build();

        Assert.Equal("https://api.example.com/search?tag=a&q=new+value&tag=b", request.Url.ToString());
        Assert.Equal("new value", request.QueryParameters.Get("q"));
        Assert.Equal(["a", "b"], request.QueryParameters.GetAll("tag"));
        Assert.Equal("https://api.example.com/search?q=new+value", withoutTags.Url.ToString());

        Assert.Throws<ArgumentException>(() => Request.Builder("https://api.example.com").WithQueryParameter("", "value"));
        Assert.Throws<ArgumentException>(() => Request.Builder("https://api.example.com").AppendQueryParameter(" ", "value"));
        Assert.Throws<ArgumentException>(() => Request.Builder("https://api.example.com").RemoveQueryParameter(""));
    }
}
