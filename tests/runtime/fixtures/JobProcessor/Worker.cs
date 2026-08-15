using System.Text.Json;
using Workers;

namespace JobProcessor;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Path == "/jobs" && request.Method == "POST")
            return await CreateJobAsync(request, environment);

        if (request.Path.StartsWith("/jobs/") && request.Method == "GET")
        {
            var id = request.Path.Substring("/jobs/".Length);
            var job = await environment.D1("DB")
                .Prepare("SELECT id, status, callback_url AS callbackUrl, attempts FROM jobs WHERE id = ?")
                .Bind(id)
                .FirstAsync<JobStatus>();
            return job is null ? Error("Not found", 404) : Response.Json(job);
        }

        return Error("Not found", 404);
    }

    [Queue]
    public static async Task ConsumeAsync(QueueMessageBatch<Job> batch, Env environment, Context context)
    {
        foreach (var message in batch)
        {
            try
            {
                await ProcessJobAsync(message.Body, environment);
                message.Ack();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Job {message.Id} failed: {exception.Message}");
                await environment.D1("DB")
                    .Prepare("UPDATE jobs SET status = 'failed' WHERE id = ?")
                    .Bind(message.Body.Id)
                    .RunAsync();
                if (message.Attempts < 3)
                    message.Retry(new QueueRetryOptions { DelaySeconds = 30 * message.Attempts });
                else
                    message.Ack();
            }
        }
    }

    [Scheduled]
    public static void Cleanup(ScheduledEvent scheduled, Env environment, Context context)
    {
        Console.WriteLine($"Scheduled cleanup {scheduled.ScheduledTime:O}");
        context.WaitUntil(CleanupAsync(environment));
    }

    private static async Task<Response> CreateJobAsync(Request request, Env environment)
    {
        var input = await request.JsonAsync<CreateJobInput>();
        if (input is null || !input.CallbackUrl.StartsWith("https://"))
            return Error("Invalid callbackUrl", 400);

        var job = new Job(
            Guid.NewGuid().ToString(),
            input.CallbackUrl,
            input.Payload,
            DateTimeOffset.UtcNow.ToString("O"));
        await environment.D1("DB")
            .Prepare("INSERT INTO jobs (id, status, callback_url, payload, attempts, created_at) VALUES (?, 'queued', ?, ?, 0, ?)")
            .Bind(job.Id, job.CallbackUrl, job.Payload.ToString(), job.CreatedAt)
            .RunAsync();
        await environment.Queue("JOBS").SendJsonAsync(job);
        return Response.Json(new { id = job.Id, status = "queued" }, 202);
    }

    private static async Task ProcessJobAsync(Job job, Env environment)
    {
        await environment.D1("DB")
            .Prepare("UPDATE jobs SET status = 'processing', attempts = attempts + 1 WHERE id = ?")
            .Bind(job.Id)
            .RunAsync();
        var headers = new Headers()
            .Set("content-type", "application/json")
            .Set("x-job-id", job.Id);
        var response = await Http.FetchAsync(job.CallbackUrl, new FetchOptions
        {
            Method = "POST",
            Headers = headers,
            Body = Body.Json(job.Payload)
        });
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Callback failed with {response.Status}");

        await environment.D1("DB")
            .Prepare("UPDATE jobs SET status = 'completed', completed_at = ? WHERE id = ?")
            .Bind(DateTimeOffset.UtcNow.ToString("O"), job.Id)
            .RunAsync();
    }

    private static async Task CleanupAsync(Env environment)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7).ToString("O");
        await environment.D1("DB")
            .Prepare("DELETE FROM jobs WHERE status = 'completed' AND completed_at < ?")
            .Bind(cutoff)
            .RunAsync();
    }

    private static Response Error(string message, int status) => Response.Json(new { error = message }, status);
}

public sealed record CreateJobInput(string CallbackUrl, JsonElement Payload);
public sealed record Job(string Id, string CallbackUrl, JsonElement Payload, string CreatedAt);
public sealed record JobStatus(string Id, string Status, string CallbackUrl, int Attempts);
