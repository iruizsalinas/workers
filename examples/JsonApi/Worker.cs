using Workers;

namespace JsonApi;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        return Task.FromResult(Response.Json(new
        {
            ok = true,
            path = request.Path
        }));
    }
}
