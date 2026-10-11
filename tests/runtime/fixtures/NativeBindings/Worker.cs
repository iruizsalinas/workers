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
            var conditional = await bucket.GetAsync(key, new R2GetOptions
            {
                OnlyIf = new R2Conditional { UploadedAfter = new Date(0), SecondsGranularity = true }
            });
            var md5 = head!.Checksums.Md5;
            var listed = await bucket.ListAsync(new R2ListOptions { Prefix = key });
            await bucket.DeleteAsync(key);
            var deleted = await bucket.HeadAsync(key);
            return Response.Json(new
            {
                key = head!.Key,
                size = head.Size,
                contentType = head.HttpMetadata?.ContentType,
                md5 = md5 is null ? null : Hex.Encode(md5),
                conditional = conditional is not null ? await conditional.TextAsync() : null,
                listed = listed.Objects[0].Key,
                deleted
            });
        }

        if (path == "/r2-encrypted")
        {
            const string encryptionKey = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";
            var key = $"encrypted-{Crypto.RandomUuid()}";
            var bucket = environment.R2("BUCKET");
            var stored = await bucket.PutAsync(key, "secret", new R2PutOptions { SsecKey = encryptionKey });
            var read = await bucket.GetAsync(key, new R2GetOptions { SsecKey = encryptionKey });
            var text = read is null ? null : await read.TextAsync();
            await bucket.DeleteAsync(key);
            return Response.Json(new { keyMd5 = stored?.SsecKeyMd5, text });
        }

        if (path == "/durable-primary")
        {
            var stub = environment.DurableObject("ECHO").GetByName("primary-csharp",
                new DurableObjectNamespaceGetDurableObjectOptions { RoutingMode = DurableObjectRoutingMode.PrimaryOnly });
            return await stub.FetchAsync("https://worker.test/primary");
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

    public async Task<bool> AlarmOptionsLifecycleAsync()
    {
        await Ctx.Storage.SetAlarmAsync(new Date(Date.Now() + 60000), new DurableObjectSetAlarmOptions { AllowUnconfirmed = true });
        var scheduled = await Ctx.Storage.GetAlarmAsync(new DurableObjectGetAlarmOptions { AllowConcurrency = true });
        var inTransaction = await Ctx.Storage.TransactionAsync(async transaction =>
        {
            var seen = await transaction.GetAlarmAsync();
            await transaction.DeleteAlarmAsync();
            await transaction.SetAlarmAsync(new Date(Date.Now() + 120000));
            return seen is not null && seen.GetTime() == scheduled!.GetTime();
        });
        var moved = await Ctx.Storage.GetAlarmAsync();
        await Ctx.Storage.DeleteAlarmAsync(new DurableObjectSetAlarmOptions { AllowConcurrency = true });
        var cleared = await Ctx.Storage.GetAlarmAsync();
        return inTransaction && moved!.GetTime() > scheduled!.GetTime() && cleared is null;
    }

    public override Task AlarmAsync(AlarmInvocationInfo alarmInfo) => Task.CompletedTask;
}

public sealed record StoredValue(int Value);

public sealed record StoredFile(int Id, string Name, byte[] Data, bool Public, [property: JsonName("created_at")] Date? CreatedAt)
{
    public string Describe() => $"{Id}:{Name}:{Base64.Encode(Data)}:{Public}:{CreatedAt?.GetUtcFullYear()}";
}
