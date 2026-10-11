using Workers;

namespace SignedGateway;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        var origin = request.Headers.Get("origin") ?? "*";
        if (request.Method == "OPTIONS")
            return Cors(new Response { Status = 204 }, origin);

        if (!await VerifyAsync(request, environment.Secret("SIGNING_SECRET")))
            return Cors(Response.Json(new { error = "Invalid signature" }, status: 401), origin);

        try
        {
            return Cors(await ProxyAsync(request, environment, context), origin);
        }
        catch (Exception exception)
        {
            Log.Error(exception.Message);
            return Cors(Response.Json(new { error = "Bad gateway" }, status: 502), origin);
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
            var prefix = TextCodec.EncodeUtf8($"{request.Method}\n{url.Path}{url.Query}\n");
            var body = request.Method == "GET" || request.Method == "HEAD" ? [] : await request.Clone().BytesAsync();
            byte[] payload = [.. prefix, .. body];
            return await Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, secret, TextCodec.DecodeHex(value), payload);
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

        var headers = new Headers(request.Headers);
        headers.Delete("host");
        headers.Delete("x-signature");
        headers.Set("x-request-id", Crypto.RandomUuid());
        headers.Set("x-original-host", canonical.Host);

        var cacheKey = new Request(canonical.ToString(), new FetchOptions { Method = "GET" });
        if (request.Method == "GET")
        {
            var hit = await CacheStorage.Default.MatchAsync(cacheKey);
            if (hit is not null)
                return new Response(hit) { Headers = { ["x-edge-cache"] = "HIT" } };
        }

        var upstreamResponse = await Http.FetchAsync(upstream.ToString(), new FetchOptions
        {
            Method = request.Method,
            Headers = headers,
            Body = request.Method == "GET" || request.Method == "HEAD" ? null : request.Body!,
            Redirect = RedirectMode.Manual,
            Signal = AbortSignal.Timeout(3000)
        });
        var response = new Response(upstreamResponse);

        if (!response.Headers.Contains("etag") && response.Body is not null)
        {
            var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, response.Clone().Body!);
            response.Headers["etag"] = $"\"{TextCodec.EncodeHex(digest)}\"";
        }
        var etag = response.Headers.Get("etag");
        if (etag is not null && request.Headers.Get("if-none-match") == etag)
            return new Response { Status = 304, Headers = { ["etag"] = etag } };

        response.Headers["x-edge-cache"] = "MISS";
        if (request.Method == "GET" && response.Status == 200 && !response.Headers.Contains("set-cookie"))
        {
            var cached = response.Clone();
            cached.Headers["cache-control"] = "public, max-age=30";
            context.WaitUntil(CacheStorage.Default.PutAsync(cacheKey, cached.Clone()));
            return cached;
        }
        return response;
    }

    private static Response Cors(Response response, string origin)
    {
        var result = new Response(response) { Headers = { ["access-control-allow-origin"] = origin, ["access-control-allow-methods"] = "GET, HEAD, POST, PUT, PATCH, DELETE, OPTIONS", ["access-control-allow-headers"] = "content-type, authorization, x-signature", ["access-control-max-age"] = "86400" }};
        result.Headers.Append("vary", "Origin");
        return result;
    }
}
