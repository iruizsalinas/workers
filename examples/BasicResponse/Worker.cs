using Workers;

namespace BasicResponse;

public static class Worker
{
    [FetchEvent]
    public static Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        return Task.FromResult(
            Response.Text("Hello from C# on Cloudflare Workers."));
    }
}
