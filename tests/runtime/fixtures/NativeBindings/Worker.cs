using Workers;

namespace NativeBindings;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Path == "/durable")
        {
            var stub = environment.DurableObject("ECHO").GetByName("generated-csharp");
            return await stub.FetchAsync("https://worker.test/from-csharp");
        }

        if (request.Path == "/kv-lifecycle")
        {
            var key = Guid.NewGuid().ToString();
            var kv = environment.Kv("KV");
            await kv.PutTextAsync(key, "stored", new KvPutOptions
            {
                Metadata = new { source = "csharp" }
            });
            var stored = await kv.GetTextWithMetadataAsync(key);
            var listed = await kv.ListAsync(new KvListOptions { Prefix = key });
            await kv.DeleteAsync(key);
            var deleted = await kv.GetTextAsync(key);
            return Response.Json(new
            {
                value = stored.Value,
                listed = listed.Keys[0].Name,
                listComplete = listed.ListComplete,
                deleted
            });
        }

        if (request.Path == "/r2-lifecycle")
        {
            var key = $"object-{Guid.NewGuid()}.txt";
            var bucket = environment.R2("BUCKET");
            await bucket.PutAsync(key, Body.Text("r2-value"), new R2PutOptions
            {
                HttpMetadata = new R2HttpMetadata(ContentType: "text/custom")
            });
            var head = await bucket.HeadAsync(key);
            var listed = await bucket.ListAsync(new R2ListOptions { Prefix = key });
            await bucket.DeleteAsync(key);
            var deleted = await bucket.HeadAsync(key);
            return Response.Json(new
            {
                key = head!.Key,
                size = head.Size,
                contentType = head.HttpMetadata.ContentType,
                listed = listed.Objects[0].Key,
                deleted
            });
        }

        if (request.Path == "/d1-advanced")
        {
            var advancedDatabase = environment.D1("DB");
            await advancedDatabase.ExecAsync("CREATE TABLE IF NOT EXISTS numbers (value INTEGER NOT NULL); DELETE FROM numbers;");
            var first = advancedDatabase.Prepare("INSERT INTO numbers (value) VALUES (?)").Bind(1);
            var second = advancedDatabase.Prepare("INSERT INTO numbers (value) VALUES (?)").Bind(2);
            var batch = await advancedDatabase.BatchAsync<object>([first, second]);
            var raw = await advancedDatabase.Prepare("SELECT value FROM numbers ORDER BY value").RawAsync();
            var session = advancedDatabase.WithSession(D1SessionMode.FirstPrimary);
            await session.Prepare("SELECT value FROM numbers LIMIT 1").FirstAsync<int>("value");
            var bookmark = await session.GetBookmarkAsync();
            return Response.Json(new
            {
                firstSuccess = batch[0].Success,
                secondSuccess = batch[1].Success,
                firstValue = raw[0][0],
                secondValue = raw[1][0],
                hasBookmark = bookmark is not null
            });
        }

        var database = environment.D1("DB");
        await database.ExecAsync("CREATE TABLE IF NOT EXISTS people (name TEXT NOT NULL); DELETE FROM people;");
        await database.Prepare("INSERT INTO people (name) VALUES (?)").Bind("Ada").RunAsync();
        var name = await database.Prepare("SELECT name FROM people LIMIT 1").FirstAsync<string>("name");
        return Response.Json(new { name });
    }
}

[DurableObject("EchoObject")]
public sealed class EchoObject
{
    private readonly DurableObjectState _state;

    public EchoObject(DurableObjectState state, Env environment)
    {
        _state = state;
    }

    public Response FetchAsync(Request request) =>
        Response.Text($"durable:{request.Path}");

    public string Greet(string name) => $"Hello, {name}";

    public async Task<string> StoreAsync(string value)
    {
        await _state.Storage.PutAsync("rpc-value", value);
        await _state.Storage.PutAsync("count", 0);
        return await _state.Storage.GetAsync<string>("rpc-value") ?? "missing";
    }

    public async Task<int> IncrementAsync()
    {
        var current = await _state.Storage.GetAsync<int>("count");
        var next = current + 1;
        await _state.Storage.PutAsync("count", next);
        return next;
    }

    public async Task<bool> StorageLifecycleAsync()
    {
        await _state.Storage.PutAsync("temporary", "value");
        var deleted = await _state.Storage.DeleteAsync("temporary");
        var value = await _state.Storage.GetAsync<string>("temporary");
        await _state.Storage.PutAsync("typed", new { value = 42 });
        var typed = await _state.Storage.GetAsync<StoredValue>("typed");
        await _state.Storage.DeleteAsync("typed");
        var typedMissing = await _state.Storage.GetAsync<StoredValue>("typed");
        return deleted && value is null && typed!.Value == 42 && typedMissing is null;
    }

    public async Task<bool> TransactionLifecycleAsync()
    {
        var observed = false;
        await _state.Storage.TransactionAsync(async transaction =>
        {
            await transaction.PutAsync("transactional", "stored");
            var value = await transaction.GetAsync<string>("transactional");
            if (value == "stored")
                observed = true;
        });
        var stored = await _state.Storage.GetAsync<string>("transactional");
        await _state.Storage.DeleteAsync("transactional");
        return observed && stored == "stored";
    }

    public void AlarmAsync(AlarmInfo info)
    {
    }
}

public sealed record StoredValue(int Value);
