using Workers;

namespace NativeBindings;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        ExecutionContext context)
    {
        var path = new Url(request.Url).Pathname;
        if (path == "/durable")
        {
            var stub = environment.DurableObject("ECHO").GetByName("generated-csharp");
            return await stub.FetchAsync("https://worker.test/from-csharp");
        }

        if (path == "/kv-lifecycle")
        {
            var key = Crypto.RandomUuid();
            var kv = environment.KV("KV");
            await kv.PutAsync(key, "stored", new KVNamespacePutOptions
            {
                Metadata = new { source = "csharp" }
            });
            var stored = await kv.GetWithMetadataAsync(key);
            var listed = await kv.ListAsync(new KVNamespaceListOptions { Prefix = key });
            await kv.DeleteAsync(key);
            var deleted = await kv.GetAsync(key);
            return Response.Json(new
            {
                value = stored.Value,
                listed = listed.Keys[0].Name,
                listComplete = listed.ListComplete,
                deleted
            });
        }

        if (path == "/r2-lifecycle")
        {
            var key = $"object-{Crypto.RandomUuid()}.txt";
            var bucket = environment.R2("BUCKET");
            await bucket.PutAsync(key, "r2-value", new R2PutOptions
            {
                HttpMetadata = new R2HttpMetadata { ContentType = "text/custom" }
            });
            var head = await bucket.HeadAsync(key);
            var listed = await bucket.ListAsync(new R2ListOptions { Prefix = key });
            await bucket.DeleteAsync(key);
            var deleted = await bucket.HeadAsync(key);
            return Response.Json(new
            {
                key = head!.Key,
                size = head.Size,
                contentType = head.HttpMetadata?.ContentType,
                listed = listed.Objects[0].Key,
                deleted
            });
        }

        if (path == "/d1-advanced")
        {
            var advancedDatabase = environment.D1("DB");
            await advancedDatabase.ExecAsync("CREATE TABLE IF NOT EXISTS numbers (value INTEGER NOT NULL); DELETE FROM numbers;");
            var first = advancedDatabase.Prepare("INSERT INTO numbers (value) VALUES (?)").Bind(1);
            var second = advancedDatabase.Prepare("INSERT INTO numbers (value) VALUES (?)").Bind(2);
            var batch = await advancedDatabase.BatchAsync<object>([first, second]);
            var raw = await advancedDatabase.Prepare("SELECT value FROM numbers ORDER BY value").RawAsync();
            var session = advancedDatabase.WithSession(D1SessionConstraint.FirstPrimary);
            await session.Prepare("SELECT value FROM numbers LIMIT 1").FirstAsync<int>("value");
            var bookmark = session.GetBookmark();
            return Response.Json(new
            {
                firstSuccess = batch[0].Success,
                secondSuccess = batch[1].Success,
                firstValue = raw[0][0],
                secondValue = raw[1][0],
                hasBookmark = bookmark is not null
            });
        }

        if (path == "/d1-rows")
        {
            var filesDatabase = environment.D1("DB");
            await filesDatabase.ExecAsync("DROP TABLE IF EXISTS files; CREATE TABLE files (id INTEGER PRIMARY KEY, name TEXT NOT NULL, data BLOB NOT NULL, public INTEGER NOT NULL, created_at TEXT);");
            var inserted = await filesDatabase.Prepare("INSERT INTO files (name, data, public, created_at) VALUES (?, ?, ?, ?)")
                .Bind("a.bin", new byte[] { 1, 2, 3 }, true, "2026-10-11T12:00:00.000Z").RunAsync();
            var batch = await filesDatabase.BatchAsync<StoredFile>([filesDatabase.Prepare("SELECT id, name, data, public, created_at FROM files")]);
            return Response.Json(new
            {
                lastRowId = inserted.Meta.LastRowId,
                changes = inserted.Meta.Changes,
                changedDb = inserted.Meta.ChangedDb,
                rowsWritten = inserted.Meta.RowsWritten,
                rowsRead = batch[0].Meta.RowsRead,
                file = batch[0].Results[0].Describe()
            });
        }

        var database = environment.D1("DB");
        await database.ExecAsync("CREATE TABLE IF NOT EXISTS people (name TEXT NOT NULL); DELETE FROM people;");
        await database.Prepare("INSERT INTO people (name) VALUES (?)").Bind("Ada").RunAsync();
        var name = await database.Prepare("SELECT name FROM people LIMIT 1").FirstAsync<string>("name");
        return Response.Json(new { name });
    }
}

public sealed class EchoObject : DurableObject
{
    public override Task<Response> FetchAsync(Request request) =>
        Task.FromResult(new Response($"durable:{new Url(request.Url).Pathname}"));

    public string Greet(string name) => $"Hello, {name}";

    public async Task<string> StoreAsync(string value)
    {
        await Ctx.Storage.PutAsync("rpc-value", value);
        await Ctx.Storage.PutAsync("count", 0);
        return await Ctx.Storage.GetAsync<string>("rpc-value") ?? "missing";
    }

    public async Task<int> IncrementAsync()
    {
        var current = await Ctx.Storage.GetAsync<int>("count");
        var next = current + 1;
        await Ctx.Storage.PutAsync("count", next);
        return next;
    }

    public async Task<bool> StorageLifecycleAsync()
    {
        await Ctx.Storage.PutAsync("temporary", "value");
        var deleted = await Ctx.Storage.DeleteAsync("temporary");
        var value = await Ctx.Storage.GetAsync<string>("temporary");
        await Ctx.Storage.PutAsync("typed", new { value = 42 });
        var typed = await Ctx.Storage.GetAsync<StoredValue>("typed");
        await Ctx.Storage.DeleteAsync("typed");
        var typedMissing = await Ctx.Storage.GetAsync<StoredValue>("typed");
        return deleted && value is null && typed!.Value == 42 && typedMissing is null;
    }

    public async Task<bool> TransactionLifecycleAsync()
    {
        var observed = false;
        await Ctx.Storage.TransactionAsync(async transaction =>
        {
            await transaction.PutAsync("transactional", "stored");
            var value = await transaction.GetAsync<string>("transactional");
            if (value == "stored")
                observed = true;
        });
        var stored = await Ctx.Storage.GetAsync<string>("transactional");
        await Ctx.Storage.DeleteAsync("transactional");
        return observed && stored == "stored";
    }

    public override Task AlarmAsync(AlarmInvocationInfo alarmInfo) => Task.CompletedTask;
}

public sealed record StoredValue(int Value);

public sealed record StoredFile(int Id, string Name, byte[] Data, bool Public, [property: JsonName("created_at")] Date? CreatedAt)
{
    public string Describe() => $"{Id}:{Name}:{Base64.Encode(Data)}:{Public}:{CreatedAt?.GetUtcFullYear()}";
}
