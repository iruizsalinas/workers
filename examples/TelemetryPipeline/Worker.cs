using Workers;

namespace TelemetryPipeline;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env env)
    {
        if (request.Method != "GET" || new Url(request.Url).Pathname != "/health")
            return new Response("Not found") { Status = 404 };

        var row = await env.D1("DB")
            .Prepare("SELECT COUNT(*) AS pending FROM readings WHERE processed_at IS NULL")
            .FirstAsync<HealthRow>();
        return Response.Json(new { ok = true, pending = row?.Pending ?? 0 });
    }

    [Scheduled]
    public static void Scheduled(ScheduledController scheduled, Env env, ExecutionContext ctx) =>
        ctx.WaitUntil(EnqueueDueSensorsAsync(env, scheduled.ScheduledTime.GetTime()));

    [Queue]
    public static async Task QueueAsync(MessageBatch<SensorJob> batch, Env env)
    {
        foreach (var message in batch.Messages)
        {
            try
            {
                await ProcessAsync(env, message.Body);
                message.Ack();
            }
            catch (Exception exception)
            {
                Log.Error($"Sensor job failed: {exception.Message}");
                if (message.Attempts < 4)
                    message.Retry(new QueueRetryOptions { DelaySeconds = 30 * message.Attempts });
                else
                    message.Ack();
            }
        }
    }

    private static async Task EnqueueDueSensorsAsync(Env env, double scheduledTime)
    {
        var due = await env.D1("DB")
            .Prepare("SELECT sensor_id AS sensorId FROM sensors WHERE next_read_at <= ? LIMIT 50")
            .Bind(scheduledTime)
            .AllAsync<SensorRow>();

        var jobs = new List<MessageSendRequest<SensorJob>>();
        foreach (var sensor in due.Results)
            jobs.Add(new MessageSendRequest<SensorJob> { Body = new SensorJob(sensor.SensorId, Crypto.RandomUuid()) });
        if (jobs.Count != 0)
            await env.Queue("READINGS").SendBatchAsync(jobs);
    }

    private static async Task ProcessAsync(Env env, SensorJob job)
    {
        var gate = env.DurableObject("RATE_GATE").GetByName("outbound-api");
        var lease = await gate.InvokeAsync<Lease>("reserve", job.RequestId);
        if (lease is null || !lease.Allowed)
            throw new InvalidOperationException("Outbound API is busy");

        var options = new RequestInit { Signal = AbortSignal.Timeout(5000) };
        var responses = await Task.WhenAll(
            Http.FetchAsync($"https://sensors.example/v1/readings/{job.SensorId}", options),
            Http.FetchAsync($"https://weather.example/v1/context/{job.SensorId}", options));
        if (!responses[0].Ok || !responses[1].Ok)
            throw new InvalidOperationException("Reading provider failed");

        var reading = await responses[0].JsonAsync<Reading>();
        var weather = await responses[1].JsonAsync<WeatherContext>();
        if (reading is null || weather is null)
            throw new InvalidOperationException("Reading provider returned invalid JSON");

        await env.D1("DB").Prepare(
                "INSERT OR REPLACE INTO readings (id, sensor_id, value, condition, processed_at) VALUES (?, ?, ?, ?, ?)")
            .Bind(job.RequestId, job.SensorId, reading.Value, weather.Condition, Date.Now())
            .RunAsync();
        await gate.InvokeAsync("complete", lease.Token);
    }
}

public sealed class RateGate : DurableObject
{
    public async Task<Lease> ReserveAsync(string requestId)
    {
        var lease = new Lease(false, "");
        await Ctx.Storage.TransactionAsync(async storage =>
        {
            var expiresAt = await storage.GetAsync<long?>("expiresAt") ?? 0;
            if (expiresAt > Date.Now())
                return;

            await storage.PutAsync("expiresAt", Date.Now() + 30000);
            await storage.PutAsync("owner", requestId);
            lease = new Lease(true, requestId);
        });
        return lease;
    }

    public Task CompleteAsync(string token) => Ctx.Storage.TransactionAsync(async storage =>
    {
        var owner = await storage.GetAsync<string>("owner");
        if (owner != token)
            return;
        await storage.DeleteAsync("owner");
        await storage.DeleteAsync("expiresAt");
    });
}

public sealed record SensorJob(string SensorId, string RequestId);
public sealed record SensorRow(string SensorId);
public sealed record HealthRow(int Pending);
public sealed record Lease(bool Allowed, string Token);
public sealed record Reading(double Value);
public sealed record WeatherContext(string Condition);
