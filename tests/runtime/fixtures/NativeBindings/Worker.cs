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
        if (path == "/websocket-close-null" || path == "/websocket-close-reason")
        {
            var closingPair = new WebSocketPair();
            closingPair.Server.Accept();
            if (path == "/websocket-close-null") closingPair.Server.Close(null, null);
            else closingPair.Server.Close(1000, null);
            return new Response { Status = 101, WebSocket = closingPair.Client };
        }
        if (path == "/r2-sequence-complete")
        {
            var multipartBucket = environment.R2("BUCKET");
            var upload = await multipartBucket.CreateMultipartUploadAsync("sequence-multipart");
            var part = await upload.UploadPartAsync(1, "payload");
            var parts = new[] { part }.Select(value => value);
            var completed = await upload.CompleteAsync(parts);
            var stored = await multipartBucket.GetAsync("sequence-multipart");
            var text = await stored!.TextAsync();
            await multipartBucket.DeleteAsync("sequence-multipart");
            return Response.Json(new { completed.Size, text });
        }
        if (path == "/r2-sequence-delete")
        {
            var sequenceBucket = environment.R2("BUCKET");
            await sequenceBucket.PutAsync("sequence-one", "one");
            await sequenceBucket.PutAsync("sequence-two", "two");
            var keys = new[] { "sequence-one", "sequence-two" }.Select(key => key);
            await sequenceBucket.DeleteAsync(keys);
            return Response.Json(new {
                first = await sequenceBucket.HeadAsync("sequence-one") is null,
                second = await sequenceBucket.HeadAsync("sequence-two") is null
            });
        }
        if (path == "/kv-sequence-read")
        {
            var keys = new[] { "__sequence_first__", "__sequence_second__" }.Select(key => key);
            var sequenceKv = environment.KV("KV");
            await sequenceKv.PutAsync("__sequence_first__", "7");
            var values = await sequenceKv.GetAsync(keys);
            var jsonValues = await sequenceKv.GetJsonAsync<JsonValue>(keys);
            await sequenceKv.DeleteAsync("__sequence_first__");
            return Response.Json(new {
                count = values.Count, first = values["__sequence_first__"], second = values["__sequence_second__"],
                jsonCount = jsonValues.Count, number = jsonValues["__sequence_first__"]!.AsNumber(),
                missing = jsonValues["__sequence_second__"] is null
            });
        }
        if (path == "/d1-sequence-batch" || path == "/d1-session-sequence-batch")
        {
            var batchDatabase = environment.D1("DB");
            var statements = new[] { 7, 11 }.Select(value => batchDatabase.Prepare("SELECT ? AS value").Bind(value));
            var rows = path == "/d1-session-sequence-batch"
                ? await batchDatabase.WithSession().BatchAsync<JsonValue>(statements)
                : await batchDatabase.BatchAsync<JsonValue>(statements);
            return Response.Json(new {
                first = rows[0].Results[0]["value"].AsNumber(),
                second = rows[1].Results[0]["value"].AsNumber()
            });
        }
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

public sealed class StoredGraph
{
    public StoredGraph? Next { get; set; }
    public string Describe() => Next == this ? "self" : "other";
}

public sealed class StoredSnapshot
{
    private readonly int value;
    public StoredSnapshot(int value) { this.value = value; }
    public string Describe() => "snapshot:" + value;
}

public sealed class EchoObject : DurableObject
{
    public async Task<bool> StoredCollectionsAsync()
    {
        var numbers = new Dictionary<int, int> { [1] = 2 };
        await Ctx.Storage.PutAsync("numbers", numbers);
        var restored = await Ctx.Storage.GetAsync<Dictionary<int, int>>("numbers");
        await Ctx.Storage.PutAsync(new Dictionary<string, Dictionary<int, int>> { ["batch-numbers"] = numbers });
        var batch = await Ctx.Storage.GetAsync<Dictionary<int, int>>("batch-numbers");
        HashSet<(int, int)> points = [(7, 8)];
        Ctx.Storage.Kv.Put("points", points);
        var restoredPoints = Ctx.Storage.Kv.Get<HashSet<(int, int)>>("points");
        var transaction = await Ctx.Storage.TransactionAsync(async storage =>
        {
            await storage.PutAsync("transaction-numbers", numbers);
            var value = await storage.GetAsync<Dictionary<int, int>>("transaction-numbers");
            return value![1] == 2;
        });
        var cycle = new Dictionary<int, object>();
        cycle[1] = cycle;
        await Ctx.Storage.PutAsync("cycle", cycle);
        var restoredCycle = await Ctx.Storage.GetAsync<Dictionary<int, object>>("cycle");
        return restored![1] == 2 && batch![1] == 2 && restoredPoints!.Contains((7, 8))
            && transaction && restoredCycle![1] == restoredCycle;
    }

    public async Task<bool> StoredGraphAsync()
    {
        var graph = new StoredGraph();
        graph.Next = graph;
        await Ctx.Storage.PutAsync("graph", graph);
        var restored = await Ctx.Storage.GetAsync<StoredGraph>("graph");
        var several = await Ctx.Storage.GetAsync<StoredGraph>(new[] { "graph" });
        var listed = await Ctx.Storage.ListAsync<StoredGraph>(new DurableObjectListOptions { Prefix = "graph" });
        Ctx.Storage.Kv.Put("sync-graph", graph);
        var sync = Ctx.Storage.Kv.Get<StoredGraph>("sync-graph");
        var syncListed = Ctx.Storage.Kv.List<StoredGraph>(new SyncKvListOptions { Prefix = "sync-graph" });
        var transaction = await Ctx.Storage.TransactionAsync(async storage =>
        {
            var value = await storage.GetAsync<StoredGraph>("graph");
            var rows = await storage.ListAsync<StoredGraph>(new DurableObjectListOptions { Prefix = "graph" });
            return value is not null && value.Next == value && rows["graph"].Describe() == "self";
        });
        await Ctx.Storage.PutAsync("snapshot", new StoredSnapshot(7));
        var snapshot = await Ctx.Storage.GetAsync<StoredSnapshot>("snapshot");
        return restored is not null && restored.Next == restored && restored.Describe() == "self"
            && several["graph"].Next == several["graph"] && listed["graph"].Describe() == "self"
            && sync is not null && sync.Next == sync && syncListed["sync-graph"].Describe() == "self"
            && transaction && snapshot!.Describe() == "snapshot:7";
    }

    private int _iteratorOffset;

    public async Task<bool> StoredListEnumerationAsync()
    {
        List<int> values = [1, 2];
        await Ctx.Storage.PutAsync("enumeration-list", values);
        var stored = await Ctx.Storage.GetAsync<List<int>>("enumeration-list");
        var caught = false;
        var seen = 0;
        try
        {
            foreach (var value in stored!)
            {
                seen++;
                stored!.Add(value);
                if (seen == 4) break;
            }
        }
        catch (Exception) { caught = true; }
        return caught && seen == 1 && stored!.Count == 3;
    }

    public async Task<string> AsyncControlFlowAsync(int value)
    {
        var url = value < 0 ? null : new Url("https://worker.test/?value=present");
        return value switch
        {
            0 => await Task.FromResult("awaited"),
            _ => url?.SearchParams.Get(await Task.FromResult("value")) ?? "missing"
        };
    }

    public int[] IteratorSnapshot(int start)
    {
        _iteratorOffset = 10;
        var sequence = Values(start);
        var first = sequence.ToArray();
        _iteratorOffset = 20;
        return first.Concat(sequence).ToArray();
    }

    private IEnumerable<int> Values(int start)
    {
        yield return _iteratorOffset + start++;
        yield return _iteratorOffset + start++;
    }

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

    public async Task<int> ReadSequenceAsync(bool transaction)
    {
        await Ctx.Storage.PutAsync("sequence-one", 7);
        await Ctx.Storage.PutAsync("sequence-two", 11);
        var keys = new[] { "sequence-one", "sequence-two" }.Select(key => key);
        var values = transaction
            ? await Ctx.Storage.TransactionAsync(tx => tx.GetAsync<int>(keys))
            : await Ctx.Storage.GetAsync<int>(keys);
        return values["sequence-one"] + values["sequence-two"];
    }

    public async Task<int> DeleteSequenceAsync()
    {
        await Ctx.Storage.PutAsync("sequence-one", 7);
        await Ctx.Storage.PutAsync("sequence-two", 11);
        var keys = new[] { "sequence-one", "sequence-two" }.Select(key => key);
        return await Ctx.Storage.DeleteAsync(keys);
    }

    public int[] InspectSocketTags(string mode)
    {
        var pair = new WebSocketPair();
        IEnumerable<string>? tags = mode == "null" ? null
            : mode == "lazy" ? new[] { "first", "second" }.Select(tag => tag)
            : new[] { "first", "second" };
        Ctx.AcceptWebSocket(pair.Server, tags);
        pair.Client.Accept();
        var counts = new[] {
            Ctx.GetTags(pair.Server).Count,
            Ctx.GetWebSockets(null).Count,
            Ctx.GetWebSockets("null").Count
        };
        pair.Client.Close(1000, "done");
        return counts;
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
