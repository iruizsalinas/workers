using Workers;

namespace TelemetryPipeline;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Method != "GET" || request.Path != "/health")
            return Response.Text("Not found", 404);

        var row = await environment.D1("DB")
            .Prepare("SELECT COUNT(*) AS pending FROM readings WHERE processed_at IS NULL")
            .FirstAsync<HealthRow>();
        return Response.Json(new { ok = true, pending = row?.Pending ?? 0 });
    }

    [Scheduled]
    public static void Scheduled(ScheduledEvent scheduled, Env environment, Context context) =>
        context.WaitUntil(EnqueueDueSensorsAsync(environment, scheduled.ScheduledTime));

    [Queue]
    public static async Task QueueAsync(
        QueueMessageBatch<SensorJob> batch,
        Env environment,
        Context context)
    {
        foreach (var message in batch)
        {
            try
            {
                await ProcessAsync(environment, message.Body);
                message.Ack();
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Sensor job failed: {exception.Message}");
                if (message.Attempts < 4)
                    message.Retry(new QueueRetryOptions { DelaySeconds = 30 * message.Attempts });
                else
                    message.Ack();
            }
        }
    }

    private static async Task EnqueueDueSensorsAsync(Env environment, DateTimeOffset scheduledTime)
    {
        var due = await environment.D1("DB")
            .Prepare("SELECT sensor_id AS sensorId FROM sensors WHERE next_read_at <= ? LIMIT 50")
            .Bind(scheduledTime.ToUnixTimeMilliseconds())
            .AllAsync<SensorRow>();

        var jobs = new List<SensorJob>();
        foreach (var sensor in due.Results)
            jobs.Add(new SensorJob(sensor.SensorId, Guid.NewGuid().ToString()));
        if (jobs.Count != 0)
            await environment.Queue("READINGS").SendJsonBatchAsync(jobs);
    }

    private static async Task ProcessAsync(Env environment, SensorJob job)
    {
        var gate = environment.DurableObject("RATE_GATE").GetByName("outbound-api");
        var lease = await gate.InvokeAsync<Lease>("reserve", [job.RequestId]);
        if (lease is null || !lease.Allowed)
            throw new InvalidOperationException("Outbound API is busy");

        var controller = new AbortController();
        var timeout = Timers.SetTimeout(
            () => controller.Abort("Reading timeout"),
            TimeSpan.FromMilliseconds(5000));
        try
        {
            var options = new FetchOptions { Signal = controller.Signal };
            var responses = await Task.WhenAll(
                Http.FetchAsync($"https://sensors.example/v1/readings/{job.SensorId}", options),
                Http.FetchAsync($"https://weather.example/v1/context/{job.SensorId}", options));
            if (!responses[0].IsSuccessStatusCode || !responses[1].IsSuccessStatusCode)
                throw new InvalidOperationException("Reading provider failed");

            var reading = await responses[0].JsonAsync<Reading>();
            var context = await responses[1].JsonAsync<WeatherContext>();
            if (reading is null || context is null)
                throw new InvalidOperationException("Reading provider returned invalid JSON");

            await environment.D1("DB").Prepare(
                    "INSERT OR REPLACE INTO readings (id, sensor_id, value, condition, processed_at) VALUES (?, ?, ?, ?, ?)")
                .Bind(job.RequestId, job.SensorId, reading.Value, context.Condition, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                .RunAsync();
            await gate.InvokeVoidAsync("complete", [lease.Token]);
        }
        finally
        {
            Timers.ClearTimeout(timeout);
        }
    }
}

[DurableObject("RateGate")]
public sealed class RateGate
{
    private readonly DurableObjectState _state;

    public RateGate(DurableObjectState state, Env environment) => _state = state;

    public async Task<Lease> ReserveAsync(string requestId)
    {
        var lease = new Lease(false, "");
        await _state.Storage.TransactionAsync(async storage =>
        {
            var expiresAt = await storage.GetAsync<long?>("expiresAt") ?? 0;
            if (expiresAt > DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                return;

            await storage.PutAsync("expiresAt", DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeMilliseconds());
            await storage.PutAsync("owner", requestId);
            lease = new Lease(true, requestId);
        });
        return lease;
    }

    public Task CompleteAsync(string token) => _state.Storage.TransactionAsync(async storage =>
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
