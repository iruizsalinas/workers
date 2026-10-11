using Workers;

namespace QueueProducer;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env env,
        ExecutionContext ctx)
    {
        await env.Queue("JOBS").SendAsync(new
        {
            path = new Url(request.Url).Pathname,
            queuedAt = new Date()
        });

        return Response.Json(new { queued = true });
    }
}
