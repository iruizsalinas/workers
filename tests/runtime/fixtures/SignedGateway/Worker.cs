using System.Text;
using Workers;

namespace SignedGateway;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        var origin = request.Headers.Get("origin") ?? "*";
        if (request.Method == "OPTIONS")
            return Cors(Response.Empty(204), origin);

        if (!await VerifyAsync(request, environment.Secret("SIGNING_SECRET")))
            return Cors(Response.Json(new { error = "Invalid signature" }, 401), origin);

        try
        {
            return Cors(await ProxyAsync(request, environment, context), origin);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return Cors(Response.Json(new { error = "Bad gateway" }, 502), origin);
        }
    }

    private static Url Canonicalize(Url input)
    {
        var result = new Url(input.ToString());
        foreach (var name in result.QueryParameters.Names())
            if (name.StartsWith("utm_") || name == "fbclid" || name == "gclid")
                result.QueryParameters.Delete(name);
        if (!result.QueryParameters.Contains("lang"))
            result.QueryParameters.Set("lang", "en");
        result.QueryParameters.Sort();
        return result;
    }

    private static async Task<bool> VerifyAsync(Request request, string secret)
    {
        var value = request.Headers.Get("x-signature");
        if (value is null)
            return false;
        if (value.StartsWith("sha256="))
            value = value.Substring(7);
        if (value.Length == 0 || value.Length % 2 != 0)
            return false;

        try
        {
            var url = Canonicalize(request.Url);
            var prefix = Encoding.UTF8.GetBytes($"{request.Method}\n{url.Path}{url.Query}\n");
            var body = request.Method == "GET" || request.Method == "HEAD" ? [] : await request.Clone().BytesAsync();
            byte[] payload = [.. prefix, .. body];
            return await Crypto.VerifyHmacSha256Async(secret, Convert.FromHexString(value), payload);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<Response> ProxyAsync(Request request, Env environment, Context context)
    {
        var canonical = Canonicalize(request.Url);
        var upstream = new Url(environment.Variable("ORIGIN"));
        upstream.Path = canonical.Path;
        upstream.Query = canonical.Query;

        var headers = request.Headers.Clone();
        headers.Delete("host");
        headers.Delete("x-signature");
        headers.Set("x-request-id", Guid.NewGuid().ToString());
        headers.Set("x-original-host", canonical.Host);

        var cacheKey = new Request(canonical.ToString(), new FetchOptions { Method = "GET" });
        if (request.Method == "GET")
        {
            var hit = await CacheStorage.Default.MatchAsync(cacheKey);
            if (hit is not null)
                return hit.Clone().WithHeader("x-edge-cache", "HIT");
        }

        var controller = new AbortController();
        var timeout = Timers.SetTimeout(() => controller.Abort("Origin timeout"), TimeSpan.FromMilliseconds(3000));
        Response response;
        try
        {
            response = await Http.FetchAsync(upstream.ToString(), new FetchOptions
            {
                Method = request.Method,
                Headers = headers,
                Body = request.Method == "GET" || request.Method == "HEAD" ? null : request.Body,
                Redirect = RedirectMode.Manual,
                Signal = controller.Signal
            });
        }
        finally
        {
            Timers.ClearTimeout(timeout);
        }

        if (!response.Headers.Contains("etag") && !response.Body.IsEmpty)
        {
            var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, response.Clone().Body);
            response = response.WithHeader("etag", $"\"{Convert.ToHexString(digest)}\"");
        }
        var etag = response.Headers.Get("etag");
        if (etag is not null && request.Headers.Get("if-none-match") == etag)
            return Response.Empty(304).WithHeader("etag", etag);

        response = response.WithHeader("x-edge-cache", "MISS");
        if (request.Method == "GET" && response.Status == 200 && !response.Headers.Contains("set-cookie"))
        {
            var cached = response.Clone().WithHeader("cache-control", "public, max-age=30");
            context.WaitUntil(CacheStorage.Default.PutAsync(cacheKey, cached.Clone()));
            return cached;
        }
        return response;
    }

    private static Response Cors(Response response, string origin) => response
        .WithHeader("access-control-allow-origin", origin)
        .WithHeader("access-control-allow-methods", "GET, HEAD, POST, PUT, PATCH, DELETE, OPTIONS")
        .WithHeader("access-control-allow-headers", "content-type, authorization, x-signature")
        .WithHeader("access-control-max-age", "86400")
        .AppendHeader("vary", "Origin");
}
