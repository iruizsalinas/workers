using Workers;

namespace CorsHeaders;

public static class Worker
{
    private static readonly Cors Cors = new Cors()
        .WithOrigins(["*"])
        .WithMethods(["GET", "POST", "OPTIONS"])
        .WithAllowedHeaders(["content-type"]);

    [FetchEvent]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Method == "OPTIONS")
            return Task.FromResult(Response.Empty(204).WithCors(Cors));

        return Task.FromResult(
            Response.Json(new { ok = true })
                .WithHeader("x-example", "cors")
                .WithCors(Cors));
    }
}
