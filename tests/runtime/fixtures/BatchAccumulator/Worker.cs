using Workers;

namespace BatchAccumulator;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context) =>
        environment.DurableObject("ACCUMULATORS").GetByName(new Url(request.Url).SearchParams.Get("bucket") ?? "default").FetchAsync(request);
}

public sealed class Accumulator : DurableObject
{
    private readonly SqlStorage _sql;

    public Accumulator()
    {
        _sql = Ctx.Storage.Sql;
        Ctx.BlockConcurrencyWhileAsync(async () =>
        {
            _sql.Exec<object>("CREATE TABLE IF NOT EXISTS pending (id TEXT PRIMARY KEY, amount INTEGER NOT NULL, created_at INTEGER NOT NULL)");
            _sql.Exec<object>("CREATE TABLE IF NOT EXISTS totals (id INTEGER PRIMARY KEY, total INTEGER NOT NULL, batches INTEGER NOT NULL)");
            _sql.Exec<object>("INSERT OR IGNORE INTO totals (id, total, batches) VALUES (1, 0, 0)");
            await Task.CompletedTask;
        });
    }

    public override async Task<Response> FetchAsync(Request request)
    {
        var path = new Url(request.Url).Pathname;
        if (request.Method == "POST" && path == "/add")
        {
            var input = await request.JsonAsync<AddInput>();
            if (input is null || input.Amount == 0)
                return Response.Json(new { error = "amount must be a non-zero integer" }, status: 400);
            var id = Crypto.RandomUuid();
            Ctx.Storage.TransactionSync(() =>
            {
                _sql.Exec<object>("INSERT INTO pending (id, amount, created_at) VALUES (?, ?, ?)", id, input.Amount, Date.Now());
                Ctx.Storage.Kv.Put($"meta:{id}", new PendingMetadata(input.Amount, Date.Now()));
            });
            if (await Ctx.Storage.GetAlarmAsync() is null)
                await Ctx.Storage.SetAlarmAsync(new Date(Date.Now() + 1000));
            return Response.Json(new { id, queued = true }, status: 202);
        }

        if (request.Method == "GET" && path == "/state")
        {
            var totals = _sql.Exec<TotalsRow>("SELECT total, batches FROM totals WHERE id = 1").One();
            var pending = _sql.Exec<PendingRow>("SELECT id, amount, created_at AS createdAt FROM pending ORDER BY created_at").ToArray();
            var metadata = Ctx.Storage.Kv.List<PendingMetadata>(new SyncKvListOptions { Prefix = "meta:" });
            var metadataCount = 0;
            foreach (var entry in metadata)
                metadataCount++;
            return Response.Json(new
            {
                totals.Total,
                totals.Batches,
                pending,
                metadataCount,
                databaseSize = _sql.DatabaseSize,
                alarm = await Ctx.Storage.GetAlarmAsync()
            });
        }

        if (request.Method == "DELETE" && path == "/reset")
        {
            Ctx.Storage.TransactionSync(() =>
            {
                _sql.Exec<object>("DELETE FROM pending");
                _sql.Exec<object>("UPDATE totals SET total = 0, batches = 0 WHERE id = 1");
            });
            await Ctx.Storage.DeleteAlarmAsync();
            return new Response { Status = 204 };
        }
        return new Response("Not found") { Status = 404 };
    }

    public override async Task AlarmAsync(AlarmInvocationInfo alarmInfo)
    {
        var rows = _sql.Exec<PendingRow>("SELECT id, amount, created_at AS createdAt FROM pending ORDER BY created_at").ToArray();
        if (rows.Count == 0)
            return;
        var delta = 0;
        foreach (var row in rows)
            delta += row.Amount;
        Ctx.Storage.TransactionSync(() =>
        {
            _sql.Exec<object>("UPDATE totals SET total = total + ?, batches = batches + 1 WHERE id = 1", delta);
            _sql.Exec<object>("DELETE FROM pending");
            Ctx.Storage.Kv.Put("lastBatch", new BatchMetadata(rows.Count, delta, Date.Now()));
        });
        foreach (var row in rows)
            Ctx.Storage.Kv.Delete($"meta:{row.Id}");
        await Ctx.Storage.SyncAsync();
    }
}

public sealed record AddInput(int Amount);
public sealed record TotalsRow(int Total, int Batches);
public sealed record PendingRow(string Id, int Amount, double CreatedAt);
public sealed record PendingMetadata(int Amount, double CreatedAt);
public sealed record BatchMetadata(int Count, int Delta, double ProcessedAt);
