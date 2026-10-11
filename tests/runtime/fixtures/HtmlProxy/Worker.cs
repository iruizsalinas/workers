using Workers;

namespace HtmlProxy;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
    {
        if (request.Method != "GET")
            return new Response("Only GET is supported") { Status = 405, Headers = { ["allow"] = "GET" } };

        var cache = CacheStorage.Default;
        var cacheKey = request.Url;
        var cached = await cache.MatchAsync(cacheKey);
        if (cached is not null)
            return new Response(cached.Body, cached) { Headers = { ["x-edge-cache"] = "HIT" } };

        var origin = environment.Variable("ORIGIN_BASE");
        var url = new Url(request.Url);
        var originResponse = await Http.FetchAsync(origin + url.Pathname + url.Search);
        var contentType = originResponse.Headers.Get("content-type") ?? "";
        if (!contentType.Contains("text/html"))
            return originResponse;

        var transformed = new HtmlRewriter()
            .On("a[href]", new LinkRewriter(origin))
            .On("body", new BannerHandler(environment.KV("CONTENT")))
            .Transform(originResponse);

        var response = new Response(transformed.Body!) { Headers = { ["cache-control"] = "public, max-age=60", ["x-edge-cache"] = "MISS" }};
        context.WaitUntil(cache.PutAsync(cacheKey, response.Clone()));
        return response;
    }
}

public sealed class LinkRewriter : HtmlElementHandler
{
    private readonly string _origin;

    public LinkRewriter(string origin)
    {
        _origin = origin;
    }

    public override Task ElementAsync(HtmlElement element)
    {
        var href = element.GetAttribute("href");
        if (href is not null && href.StartsWith('/'))
            element.SetAttribute("href", new Url(href, _origin).ToString());
        return Task.CompletedTask;
    }
}

public sealed class BannerHandler : HtmlElementHandler
{
    private readonly KVNamespace _content;

    public BannerHandler(KVNamespace content)
    {
        _content = content;
    }

    public override async Task ElementAsync(HtmlElement element)
    {
        var banner = await _content.GetAsync("banner");
        if (banner is not null)
            element.Prepend($"<aside class=\"edge-banner\">{banner}</aside>", new HtmlContentOptions { Html = true });
    }
}
