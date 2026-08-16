using Workers;

namespace BatchAccumulator;

public static class Worker
{
    [Fetch]
    public static Task<Response> FetchAsync(Request request, Env environment, Context context) =>
        environment.DurableObject("ACCUMULATORS").GetByName(request.QueryParameters.Get("bucket") ?? "default").FetchAsync(request);
}

[DurableObject("BatchAccumulator")]
public sealed class Accumulator
{
    private readonly DurableObjectState _state;
    private readonly DurableObjectSqlStorage _sql;

    public Accumulator(DurableObjectState state, Env environment)
    {
        _state = state;
        _sql = state.Storage.Sql;
        state.BlockConcurrencyWhileAsync(async () =>
        {
            _sql.Exec<object>("CREATE TABLE IF NOT EXISTS pending (id TEXT PRIMARY KEY, amount INTEGER NOT NULL, created_at INTEGER NOT NULL)");
            _sql.Exec<object>("CREATE TABLE IF NOT EXISTS totals (id INTEGER PRIMARY KEY, total INTEGER NOT NULL, batches INTEGER NOT NULL)");
            _sql.Exec<object>("INSERT OR IGNORE INTO totals (id, total, batches) VALUES (1, 0, 0)");
            await Task.CompletedTask;
        });
    }

    public async Task<Response> FetchAsync(Request request)
    {
        if (request.Method == "POST" && request.Path == "/add")
        {
            var input = await request.JsonAsync<AddInput>();
            if (input is null || input.Amount == 0)
                return Response.Json(new { error = "amount must be a non-zero integer" }, 400);
            var id = Guid.NewGuid().ToString();
            _state.Storage.TransactionSync(() =>
            {
                _sql.Exec<object>("INSERT INTO pending (id, amount, created_at) VALUES (?, ?, ?)", id, input.Amount, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                _state.Storage.Kv.Put($"meta:{id}", new PendingMetadata(input.Amount, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
            });
            if (await _state.Storage.GetAlarmAsync() is null)
                await _state.Storage.SetAlarmAsync(DateTimeOffset.UtcNow.AddSeconds(1));
            return Response.Json(new { id, queued = true }, 202);
        }

        if (request.Method == "GET" && request.Path == "/state")
        {
            var totals = _sql.Exec<TotalsRow>("SELECT total, batches FROM totals WHERE id = 1").One();
            var pending = _sql.Exec<PendingRow>("SELECT id, amount, created_at AS createdAt FROM pending ORDER BY created_at").ToArray();
            var metadata = _state.Storage.Kv.List<PendingMetadata>(new DurableObjectKvListOptions { Prefix = "meta:" });
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
                alarm = await _state.Storage.GetAlarmAsync()
            });
        }

        if (request.Method == "DELETE" && request.Path == "/reset")
        {
            _state.Storage.TransactionSync(() =>
            {
                _sql.Exec<object>("DELETE FROM pending");
                _sql.Exec<object>("UPDATE totals SET total = 0, batches = 0 WHERE id = 1");
            });
            await _state.Storage.DeleteAlarmAsync();
            return Response.Empty(204);
        }
        return Response.Text("Not found", 404);
    }

    public async Task AlarmAsync()
    {
        var rows = _sql.Exec<PendingRow>("SELECT id, amount, created_at AS createdAt FROM pending ORDER BY created_at").ToArray();
        if (rows.Count == 0)
            return;
        var delta = 0;
        foreach (var row in rows)
            delta += row.Amount;
        _state.Storage.TransactionSync(() =>
        {
            _sql.Exec<object>("UPDATE totals SET total = total + ?, batches = batches + 1 WHERE id = 1", delta);
            _sql.Exec<object>("DELETE FROM pending");
            _state.Storage.Kv.Put("lastBatch", new BatchMetadata(rows.Count, delta, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        });
        foreach (var row in rows)
            _state.Storage.Kv.Delete($"meta:{row.Id}");
        await _state.Storage.SyncAsync();
    }
}

public sealed record AddInput(int Amount);
public sealed record TotalsRow(int Total, int Batches);
public sealed record PendingRow(string Id, int Amount, long CreatedAt);
public sealed record PendingMetadata(int Amount, long CreatedAt);
public sealed record BatchMetadata(int Count, int Delta, long ProcessedAt);
