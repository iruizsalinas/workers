using Workers;

namespace HtmlProxy;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Method != "GET")
            return Response.Text("Only GET is supported", 405).WithHeader("allow", "GET");

        var cache = CacheStorage.Default;
        var cacheKey = request.Url.ToString();
        var cached = await cache.MatchAsync(cacheKey);
        if (cached is not null)
            return cached.WithHeader("x-edge-cache", "HIT");

        var origin = environment.Variable("ORIGIN_BASE");
        var originResponse = await Http.FetchAsync(origin + request.PathAndQuery);
        var contentType = originResponse.Headers.Get("content-type") ?? "";
        if (!contentType.Contains("text/html"))
            return originResponse;

        var transformed = new HtmlRewriter()
            .On("a[href]", new LinkRewriter(origin))
            .On("body", new BannerHandler(environment.Kv("CONTENT")))
            .Transform(originResponse);

        var response = Response.FromStream(transformed.BodyStream()!)
            .WithHeader("cache-control", "public, max-age=60")
            .WithHeader("x-edge-cache", "MISS");
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

    public override ValueTask ElementAsync(HtmlElement element)
    {
        var href = element.GetAttribute("href");
        if (href is not null && href.StartsWith('/'))
            element.SetAttribute("href", new Uri(new Uri(_origin), href).ToString());
        return ValueTask.CompletedTask;
    }
}

public sealed class BannerHandler : HtmlElementHandler
{
    private readonly IKvNamespace _content;

    public BannerHandler(IKvNamespace content)
    {
        _content = content;
    }

    public override async ValueTask ElementAsync(HtmlElement element)
    {
        var banner = await _content.GetTextAsync("banner");
        if (banner is not null)
            element.Prepend($"<aside class=\"edge-banner\">{banner}</aside>", HtmlContentOptions.Html);
    }
}
