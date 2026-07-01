using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task D1ProxyDispatchesPreparedStatements()
    {
        var dispatcher = new CapturingDispatcher(
            """{"results":[{"id":7,"name":"Ada"}],"success":true,"meta":{"duration":1.5,"rows_read":1}}""",
            """[["id","name"],[7,"Ada"]]""",
            """{"value":"Ada"}""",
            """{"success":true,"meta":{"changes":1}}""",
            """
            [
              {"results":[{"id":7,"name":"Ada"}],"success":true,"meta":{"rows_read":1}},
              {"results":[{"id":8,"name":"Grace"}],"success":true,"meta":{"rows_read":1}}
            ]
            """,
            """{"count":2,"duration":3.25}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-5");
        var database = environment.D1("DB");

        var all = await database.Prepare("select * from users where id = ?")
            .Bind(7)
            .AllAsync<UserRow>();
        var raw = await database.Prepare("select id, name from users where id = ?")
            .Bind(7)
            .RawAsync(new D1RawOptions { ColumnNames = true });
        var first = await database.Prepare("select name from users where id = ?")
            .Bind(D1Value.Integer(7))
            .FirstAsync<string>("name");
        var run = await database.Prepare("update users set name = ? where id = ?")
            .Bind("Grace", 7)
            .RunAsync();
        var batch = await database.BatchAsync<UserRow>(
            [
                database.Prepare("select * from users where id = ?").Bind(7),
                database.Prepare("select * from users where id = ?").Bind(8)
            ]);
        var exec = await database.ExecAsync("pragma optimize");

        Assert.True(all.Success);
        Assert.Equal("Ada", all.Results.Single().Name);
        Assert.Equal(1, all.Meta?.RowsRead);
        Assert.Equal("id", raw[0][0].GetString());
        Assert.Equal(7, raw[1][0].GetInt32());
        Assert.Equal("Ada", raw[1][1].GetString());
        Assert.Equal("Ada", first);
        Assert.True(run.Success);
        Assert.Equal(1, run.Meta?.Changes);
        Assert.Equal("Ada", batch[0].Results.Single().Name);
        Assert.Equal("Grace", batch[1].Results.Single().Name);
        Assert.Equal(2, exec.Count);
        Assert.Equal(
            ["d1.all", "d1.raw", "d1.first", "d1.run", "d1.batch", "d1.exec"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var payload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("select * from users where id = ?", payload.RootElement.GetProperty("query").GetString());
        Assert.Equal("integer", payload.RootElement.GetProperty("values")[0].GetProperty("type").GetString());
        Assert.Equal(7, payload.RootElement.GetProperty("values")[0].GetProperty("value").GetInt32());

        using var rawPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.True(rawPayload.RootElement.GetProperty("options").GetProperty("columnNames").GetBoolean());

        using var batchPayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal(2, batchPayload.RootElement.GetProperty("statements").GetArrayLength());
        Assert.Equal(
            "select * from users where id = ?",
            batchPayload.RootElement.GetProperty("statements")[0].GetProperty("query").GetString());
    }

    [Fact]
    public async Task D1BatchRejectsInvalidStatementSets()
    {
        var dispatcher = new CapturingDispatcher("[]");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-d1-invalid-batch");
        var database = environment.D1("DB");
        var otherDatabase = environment.D1("OTHER_DB");

        await Assert.ThrowsAsync<ArgumentException>(() => database.BatchAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            database.BatchAsync([otherDatabase.Prepare("select 1")]));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task D1ProxyDispatchesSessionOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"success":true,"meta":{"rows_read":1}}""",
            """{"results":[{"id":7,"name":"Ada"}],"success":true}""",
            """[["id","name"],[7,"Ada"]]""",
            """{"value":"Ada"}""",
            """
            [
              {"results":[{"id":7,"name":"Ada"}],"success":true},
              {"results":[{"id":8,"name":"Grace"}],"success":true}
            ]
            """,
            """{"bookmark":"bookmark-1"}""",
            """{"success":true}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-d1-session");
        var database = environment.D1("DB");
        var session = database.WithSession(new D1SessionOptions { Mode = D1SessionMode.FirstPrimary });

        var run = await session.Prepare("select * from users where id = ?").Bind(7).RunAsync();
        var all = await session.Prepare("select * from users where id = ?").Bind(7).AllAsync<UserRow>();
        var raw = await session.Prepare("select id, name from users where id = ?").Bind(7).RawAsync(new D1RawOptions { ColumnNames = true });
        var first = await session.Prepare("select name from users where id = ?").Bind(7).FirstAsync<string>("name");
        var batch = await session.BatchAsync<UserRow>(
            [
                session.Prepare("select * from users where id = ?").Bind(7),
                session.Prepare("select * from users where id = ?").Bind(8)
            ]);
        var bookmark = await session.GetBookmarkAsync();
        var resumed = database.WithSession(D1SessionOptions.FromBookmark("bookmark-1"));
        await resumed.Prepare("select 1").RunAsync();

        Assert.True(run.Success);
        Assert.Equal("Ada", all.Results.Single().Name);
        Assert.Equal("id", raw[0][0].GetString());
        Assert.Equal("Ada", first);
        Assert.Equal("Ada", batch[0].Results.Single().Name);
        Assert.Equal("Grace", batch[1].Results.Single().Name);
        Assert.Equal("bookmark-1", bookmark);
        Assert.Equal(
            ["d1.session.run", "d1.session.all", "d1.session.raw", "d1.session.first", "d1.session.batch", "d1.session.getBookmark", "d1.session.run"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var firstPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("first-primary", firstPayload.RootElement.GetProperty("parameter").GetString());
        Assert.Equal("select * from users where id = ?", firstPayload.RootElement.GetProperty("payload").GetProperty("query").GetString());

        using var batchPayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal(2, batchPayload.RootElement.GetProperty("payload").GetProperty("statements").GetArrayLength());

        using var resumedPayload = JsonDocument.Parse(dispatcher.Invocations[6].PayloadJson);
        Assert.Equal("bookmark-1", resumedPayload.RootElement.GetProperty("parameter").GetString());
        Assert.NotEqual(
            firstPayload.RootElement.GetProperty("handle").GetString(),
            resumedPayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task D1SessionRejectsInvalidStatementSets()
    {
        var dispatcher = new CapturingDispatcher("[]");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-d1-invalid-session");
        var database = environment.D1("DB");
        var session = database.WithSession();
        var otherSession = database.WithSession();

        await Assert.ThrowsAsync<ArgumentException>(() => session.BatchAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.BatchAsync([database.Prepare("select 1")]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            session.BatchAsync([otherSession.Prepare("select 1")]));
        Assert.Throws<ArgumentException>(() => database.WithSession(new D1SessionOptions
        {
            Mode = D1SessionMode.FirstPrimary,
            Bookmark = "bookmark"
        }));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task CacheProxyDispatchesRequestAndResponseEnvelopes()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("cached", 203));
        var dispatcher = new CapturingDispatcher(
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            """{"deleted":true}""",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-6");

        var matched = await environment.Cache().MatchAsync(
            "https://cache.example/value",
            new CacheQueryOptions { IgnoreMethod = true });
        var deleted = await environment.Cache("assets").DeleteAsync(
            Request.Delete("https://cache.example/value"),
            ignoreMethod: true);
        await environment.Cache("assets").PutAsync(
            "https://cache.example/value",
            Response.Text("next"));

        Assert.NotNull(matched);
        Assert.Equal(203, matched.Status);
        Assert.Equal("cached", matched.Body.AsText());
        Assert.Equal(CacheDeleteResult.Deleted, deleted);
        Assert.Equal(["cache.match", "cache.delete", "cache.put"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.Equal("$default", dispatcher.Invocations[0].BindingName);
        Assert.Equal("assets", dispatcher.Invocations[1].BindingName);

        using var payload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.True(payload.RootElement.GetProperty("options").GetProperty("ignoreMethod").GetBoolean());
        Assert.Equal("https://cache.example/value", payload.RootElement.GetProperty("key").GetProperty("url").GetString());

        using var deletePayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.True(deletePayload.RootElement.GetProperty("options").GetProperty("ignoreMethod").GetBoolean());
        Assert.Equal("DELETE", deletePayload.RootElement.GetProperty("key").GetProperty("request").GetProperty("method").GetString());
    }

    [Fact]
    public async Task CachePutRejectsDocumentedInvalidInputs()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-cache-invalid");
        var cache = environment.Cache();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.PutAsync(
                Request.Post("https://cache.example/value", Body.Text("request")),
                Response.Text("cached")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.PutAsync("https://cache.example/partial", Response.Text("partial", 206)));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            cache.PutAsync(
                "https://cache.example/vary",
                Response.Text("vary").WithHeader("vary", "accept-encoding, *")));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task DurableObjectNamespaceDispatchesIdsAndStubFetch()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("from durable object", 209));
        var dispatcher = new CapturingDispatcher(
            """{"id":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","name":"room-1"}""",
            """{"id":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb","name":null}""",
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            """{"value":{"ok":true,"count":2}}""",
            """{"value":null}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-7");
        var objects = environment.DurableObject("ROOMS");

        var namedId = await objects.IdFromNameAsync("room-1");
        var uniqueId = await objects.NewUniqueIdAsync(new DurableObjectIdOptions { Jurisdiction = "eu" });
        var idResponse = await objects.Get(
            namedId,
            new DurableObjectGetOptions { LocationHint = "weur" })
            .FetchAsync(Request.Post(
                "https://internal.example/message",
                Body.Text("hello")));
        var nameResponse = await objects.GetByName("room-2").FetchAsync("https://internal.example/state");
        var status = await objects.GetByName("room-2").InvokeAsync<RoomStatus>(
            "status",
            [1, "compact"]);
        await objects.Get(namedId).InvokeVoidAsync(
            "touch",
            [new { ttl = 60 }]);

        Assert.Equal("room-1", namedId.Name);
        Assert.Null(uniqueId.Name);
        Assert.Equal(new DurableObjectId(namedId.Value, name: "ignored metadata"), namedId);
        Assert.Equal(209, idResponse.Status);
        Assert.Equal("from durable object", nameResponse.Body.AsText());
        Assert.NotNull(status);
        Assert.True(status.Ok);
        Assert.Equal(2, status.Count);
        Assert.Equal(
            ["durable.idFromName", "durable.newUniqueId", "durable.fetch", "durable.fetch", "durable.rpc", "durable.rpc"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var idPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal(namedId.Value, idPayload.RootElement.GetProperty("target").GetProperty("id").GetString());
        Assert.Equal("weur", idPayload.RootElement.GetProperty("options").GetProperty("locationHint").GetString());
        Assert.Equal("POST", idPayload.RootElement.GetProperty("request").GetProperty("method").GetString());

        using var namePayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("room-2", namePayload.RootElement.GetProperty("target").GetProperty("name").GetString());

        using var rpcPayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal("room-2", rpcPayload.RootElement.GetProperty("target").GetProperty("name").GetString());
        Assert.Equal("status", rpcPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(1, rpcPayload.RootElement.GetProperty("arguments")[0].GetInt32());
        Assert.Equal("compact", rpcPayload.RootElement.GetProperty("arguments")[1].GetString());

        using var voidRpcPayload = JsonDocument.Parse(dispatcher.Invocations[5].PayloadJson);
        Assert.Equal(namedId.Value, voidRpcPayload.RootElement.GetProperty("target").GetProperty("id").GetString());
        Assert.Equal("touch", voidRpcPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(60, voidRpcPayload.RootElement.GetProperty("arguments")[0].GetProperty("ttl").GetInt32());
    }

    [Fact]
    public async Task DurableObjectStubDispatchesRpcStubOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"rpc:durable"}""",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-durable-rpc-stub");

        var stub = await environment.DurableObject("ROOMS")
            .GetByName("room-1")
            .InvokeStubAsync("session", [new { mode = "compact" }]);
        await stub.DisposeAsync();

        Assert.Equal(["durable.rpcStub", "rpc.stub.dispose"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.Equal("ROOMS", dispatcher.Invocations[0].BindingName);
        Assert.Equal("$rpc", dispatcher.Invocations[1].BindingName);

        using var rpcPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("room-1", rpcPayload.RootElement.GetProperty("target").GetProperty("name").GetString());
        Assert.Equal("session", rpcPayload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal("compact", rpcPayload.RootElement.GetProperty("arguments")[0].GetProperty("mode").GetString());
    }

    [Fact]
    public async Task DurableObjectStubCreatesTypedRpcClients()
    {
        var dispatcher = new CapturingDispatcher("""{"value":{"ok":true,"count":4}}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-durable-typed-rpc");

        var status = await environment.DurableObject("ROOMS")
            .GetByName("room-typed")
            .AsRpc<IRoomRpc>()
            .Status(9, "wide");

        Assert.NotNull(status);
        Assert.True(status.Ok);
        Assert.Equal(4, status.Count);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("durable.rpc", invocation.Operation);
        Assert.Equal("ROOMS", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("room-typed", payload.RootElement.GetProperty("target").GetProperty("name").GetString());
        Assert.Equal("Status", payload.RootElement.GetProperty("methodName").GetString());
        Assert.Equal(9, payload.RootElement.GetProperty("arguments")[0].GetInt32());
        Assert.Equal("wide", payload.RootElement.GetProperty("arguments")[1].GetString());
    }
}
