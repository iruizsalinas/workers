using Workers;

namespace QueueProducer;

public static class Worker
{
    [FetchEvent]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        await environment.Queue("JOBS").SendJsonAsync(new
        {
            path = request.Path,
            queuedAt = DateTimeOffset.UtcNow
        });

        return Response.Json(new { queued = true });
    }
}
