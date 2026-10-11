using Workers;

namespace JsonApi;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(Request request)
    {
        return Task.FromResult(Response.Json(new
        {
            ok = true,
            path = new Url(request.Url).Pathname
        }));
    }
}
