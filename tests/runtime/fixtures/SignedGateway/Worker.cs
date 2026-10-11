using Workers;

namespace SignedGateway;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
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

    private static Url Canonicalize(string input)
    {
        var result = new Url(input);
        foreach (var name in result.SearchParams.Keys())
            if (name.StartsWith("utm_") || name == "fbclid" || name == "gclid")
                result.SearchParams.Delete(name);
        if (!result.SearchParams.Has("lang"))
            result.SearchParams.Set("lang", "en");
        result.SearchParams.Sort();
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
            var prefix = new TextEncoder().Encode($"{request.Method}\n{url.Pathname}{url.Search}\n");
            var body = request.Method == "GET" || request.Method == "HEAD" ? [] : await request.Clone().BytesAsync();
            byte[] payload = [.. prefix, .. body];
            return await Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, secret, Hex.Decode(value), payload);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<Response> ProxyAsync(Request request, Env environment, ExecutionContext context)
    {
        var canonical = Canonicalize(request.Url);
        var upstream = new Url(environment.Variable("ORIGIN"));
        upstream.Pathname = canonical.Pathname;
        upstream.Search = canonical.Search;

        var headers = new Headers(request.Headers);
        headers.Delete("host");
        headers.Delete("x-signature");
        headers.Set("x-request-id", Crypto.RandomUuid());
        headers.Set("x-original-host", canonical.Host);

        var cacheKey = new Request(canonical.ToString(), new RequestInit { Method = "GET" });
        if (request.Method == "GET")
        {
            var hit = await CacheStorage.Default.MatchAsync(cacheKey);
            if (hit is not null)
                return new Response(hit.Body, hit) { Headers = { ["x-edge-cache"] = "HIT" } };
        }

        var upstreamResponse = await Http.FetchAsync(upstream.ToString(), new RequestInit
        {
            Method = request.Method,
            Headers = headers,
            Body = request.Method == "GET" || request.Method == "HEAD" ? null : request.Body!,
            Redirect = RedirectMode.Manual,
            Signal = AbortSignal.Timeout(3000)
        });
        var response = new Response(upstreamResponse.Body, upstreamResponse);

        if (!response.Headers.Has("etag") && response.Body is not null)
        {
            var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, response.Clone().Body!);
            response.Headers["etag"] = $"\"{Hex.Encode(digest)}\"";
        }
        var etag = response.Headers.Get("etag");
        if (etag is not null && request.Headers.Get("if-none-match") == etag)
            return new Response { Status = 304, Headers = { ["etag"] = etag } };

        response.Headers["x-edge-cache"] = "MISS";
        if (request.Method == "GET" && response.Status == 200 && !response.Headers.Has("set-cookie"))
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
        var result = new Response(response.Body, response) { Headers = { ["access-control-allow-origin"] = origin, ["access-control-allow-methods"] = "GET, HEAD, POST, PUT, PATCH, DELETE, OPTIONS", ["access-control-allow-headers"] = "content-type, authorization, x-signature", ["access-control-max-age"] = "86400" }};
        result.Headers.Append("vary", "Origin");
        return result;
    }
}
