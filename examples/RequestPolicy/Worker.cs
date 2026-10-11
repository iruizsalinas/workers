using Workers;

namespace RequestPolicy;

public static class Worker
{
    private const int MaxBodyBytes = 4096;
    private static readonly Router Routes = new Router()
        .Post("/v1/readings", ForwardAsync)
        .Fallback((request, route) => Error("Not found", 404))
        .OnError((error, request, route) => Error(error.Message, error.Status));

    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env environment, Context context) =>
        Routes.HandleAsync(request, environment, context);

    private static async Task<Response> ForwardAsync(Request request, RouteContext route)
    {
        var contentType = request.Headers.Get("content-type");
        if (contentType is null || !contentType.Split(';')[0].Trim().Equals("application/json", StringComparison.OrdinalIgnoreCase))
            throw new HttpError(415, "Content-Type must be application/json");

        var input = await request.JsonAsync<ReadingInput>(maxBytes: MaxBodyBytes);

        if (!IsValid(input))
            throw new HttpError(400, "Invalid reading");

        var upstream = new Url(route.Env.Variable("ORIGIN"));
        upstream.Path = "/v1/readings";

        var headers = new Headers(request.Headers);
        headers.Delete("authorization");
        headers.Delete("cookie");
        headers.Delete("content-length");
        headers.Set("content-type", "application/json");
        headers.Set("x-request-id", Crypto.RandomUuid());

        try
        {
            var response = await Http.FetchAsync(upstream.ToString(), new FetchOptions
            {
                Method = "POST",
                Headers = headers,
                Body = Json.Stringify(input),
                Redirect = RedirectMode.Manual,
                Signal = AbortSignal.Timeout(3000)
            });
            return new Response(response)
            {
                Headers = { ["x-policy"] = "validated", ["x-upstream-host"] = upstream.Hostname }
            };
        }
        catch (Exception exception)
        {
            Log.Error(exception.Message);
            return Error("Upstream unavailable", 502);
        }
    }

    private static bool IsValid(ReadingInput? input)
    {
        if (input is null || !new RegExp("^[a-z0-9-]{3,32}$").Test(input.DeviceId))
            return false;
        if (input.Value < -100 || input.Value > 1000 || input.Tags.Count > 8)
            return false;

        var tags = new HashSet<string>();
        foreach (var tag in input.Tags)
            if (tag.Length == 0 || tag.Length > 24 || !tags.Add(tag))
                return false;
        return true;
    }

    private static Response Error(string message, int status) =>
        Response.Json(new { error = message, requestId = Crypto.RandomUuid() }, status: status);
}

public sealed record ReadingInput(string DeviceId, double Value, IReadOnlyList<string> Tags);
