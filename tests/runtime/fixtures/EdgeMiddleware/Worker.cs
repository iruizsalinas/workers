using Workers;

namespace EdgeMiddleware;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        var started = Performance.Now();
        var metadata = await environment.Version("VERSION").GetAsync();
        var allowed = await environment.RateLimiter("RATE").LimitAsync(RateLimitKey(request));

        if (!allowed.Success)
            return Record(environment, request, metadata, Response.Json(new { error = "Rate limit exceeded" }, 429), "rate_limited", started)
                .WithHeader("retry-after", "60");

        if (request.Path == "/api/version")
            return Record(environment, request, metadata,
                Response.Json(new { id = metadata.Id, tag = metadata.Tag, createdAt = metadata.Timestamp }), "version", started);

        if (request.Path == "/api/echo" && request.Method == "POST")
            return Record(environment, request, metadata,
                Response.Json(new { body = await request.JsonAsync<object>(), version = metadata.Id, requestId = Guid.NewGuid() }),
                "api_echo", started);

        if (request.Method != "GET" && request.Method != "HEAD")
            return Record(environment, request, metadata, Response.Text("Method not allowed", 405), "method_rejected", started)
                .WithHeader("allow", "GET, HEAD");

        var asset = await environment.Assets("ASSETS").FetchAsync(request);
        var response = asset.Clone()
            .WithHeader("x-content-type-options", "nosniff")
            .WithHeader("referrer-policy", "strict-origin-when-cross-origin")
            .WithHeader("permissions-policy", "camera=(), microphone=(), geolocation=()")
            .WithHeader("cross-origin-opener-policy", "same-origin")
            .WithHeader("x-worker-version", metadata.Id);
        if (metadata.Tag is not null)
            response = response.WithHeader("x-worker-version-tag", metadata.Tag);
        if (asset.Status >= 200 && asset.Status < 300)
            response = response.AppendHeader("cache-control", "public, max-age=300");

        return Record(environment, request, metadata, response,
            asset.Status == 404 ? "asset_missing" : "asset_served", started);
    }

    private static string RateLimitKey(Request request)
    {
        var user = request.Headers.Get("x-user-id");
        return user is not null && user.Length != 0 ? $"user:{user}:{request.Path}" : $"anonymous:{request.Path}";
    }

    private static Response Record(
        Env environment, Request request, VersionMetadata metadata, Response response, string eventName, double started)
    {
        environment.Analytics("METRICS").WriteDataPoint(new AnalyticsEngineDataPoint(
            [metadata.Id], [response.Status, Performance.Now() - started], [eventName, metadata.Tag ?? "", request.Method, request.Path]));
        return response;
    }
}
