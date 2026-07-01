using System.Text.Json;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task DurableObjectStorageDispatchesTransactions()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"txn:1"}""",
            """{"value":4}""",
            "{}",
            "{}");
        var state = new DurableObjectState(
            "invocation-do-2",
            new DurableObjectId("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
            dispatcher);

        var result = await state.Storage.TransactionAsync(async transaction =>
        {
            var count = await transaction.GetJsonAsync<int>("count");
            await transaction.PutJsonAsync("count", count + 1);
            return count + 1;
        });

        Assert.Equal(5, result);
        Assert.Equal(
            [
                "durable.storage.transaction.begin",
                "durable.storage.transaction.get",
                "durable.storage.transaction.put",
                "durable.storage.transaction.commit"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("txn:1", getPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("count", getPayload.RootElement.GetProperty("key").GetString());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("txn:1", putPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal(5, putPayload.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task DurableObjectStorageRollsBackTransactionOnCallbackFailure()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"txn:2"}""",
            "{}");
        var state = new DurableObjectState(
            "invocation-do-3",
            new DurableObjectId("cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"),
            dispatcher);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            state.Storage.TransactionAsync(_ => throw new InvalidOperationException("boom")));

        Assert.Equal(
            ["durable.storage.transaction.begin", "durable.storage.transaction.rollback"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var rollbackPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("txn:2", rollbackPayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task DurableObjectSqlStorageDispatchesQueries()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "rows": [{ "id": 1, "name": "Ada" }],
              "columnNames": ["id", "name"],
              "rowsRead": 1,
              "rowsWritten": 0
            }
            """,
            """{"value":{"id":2,"name":"Grace"}}""",
            """
            {
              "rows": [[3, "Katherine"]],
              "columnNames": ["id", "name"],
              "rowsRead": 1,
              "rowsWritten": 0
            }
            """,
            """{"databaseSize":4096}""");
        var state = new DurableObjectState(
            "invocation-do-sql",
            new DurableObjectId("dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"),
            dispatcher);

        var rows = await state.Storage.Sql
            .Prepare("SELECT id, name FROM users WHERE id = ?")
            .Bind(1)
            .AllAsync<UserRow>();
        var one = await state.Storage.Sql
            .Prepare("SELECT id, name FROM users WHERE id = ?")
            .Bind(D1Value.Integer(2))
            .OneAsync<UserRow>();
        var raw = await state.Storage.Sql
            .Prepare("SELECT id, name FROM users")
            .RawAsync();
        var size = await state.Storage.Sql.GetDatabaseSizeAsync();

        Assert.Equal("Ada", rows.Rows.Single().Name);
        Assert.Equal(["id", "name"], rows.ColumnNames);
        Assert.Equal(1, rows.RowsRead);
        Assert.Equal(0, rows.RowsWritten);
        Assert.Equal("Grace", one.Name);
        Assert.Equal(3, raw.Rows.Single()[0].GetInt32());
        Assert.Equal("Katherine", raw.Rows.Single()[1].GetString());
        Assert.Equal(4096, size);
        Assert.Equal(
            [
                "durable.storage.sql.all",
                "durable.storage.sql.one",
                "durable.storage.sql.raw",
                "durable.storage.sql.databaseSize"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var allPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("SELECT id, name FROM users WHERE id = ?", allPayload.RootElement.GetProperty("query").GetString());
        Assert.Equal("integer", allPayload.RootElement.GetProperty("values")[0].GetProperty("type").GetString());
        Assert.Equal(1, allPayload.RootElement.GetProperty("values")[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task DurableObjectSqlStorageDispatchesTransactionSyncRawBatch()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "results": [
                {
                  "rows": [],
                  "columnNames": [],
                  "rowsRead": 0,
                  "rowsWritten": 1
                },
                {
                  "rows": [[1, "Ada"]],
                  "columnNames": ["id", "name"],
                  "rowsRead": 1,
                  "rowsWritten": 0
                }
              ]
            }
            """);
        var state = new DurableObjectState(
            "invocation-do-sql-sync-txn",
            new DurableObjectId("dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"),
            dispatcher);

        var results = await state.Storage.Sql.TransactionSyncRawAsync(
            [
                state.Storage.Sql
                    .Prepare("INSERT INTO users (id, name) VALUES (?, ?)")
                    .Bind(1, "Ada"),
                state.Storage.Sql
                    .Prepare("SELECT id, name FROM users WHERE id = ?")
                    .Bind(1)
            ]);

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].RowsWritten);
        Assert.Empty(results[0].Rows);
        Assert.Equal(["id", "name"], results[1].ColumnNames);
        Assert.Equal(1, results[1].RowsRead);
        Assert.Equal(1, results[1].Rows.Single()[0].GetInt32());
        Assert.Equal("Ada", results[1].Rows.Single()[1].GetString());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("$durableObjectState", invocation.BindingName);
        Assert.Equal("durable.storage.sql.transactionSync.raw", invocation.Operation);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        var statements = payload.RootElement.GetProperty("statements");
        Assert.Equal(2, statements.GetArrayLength());
        Assert.Equal("INSERT INTO users (id, name) VALUES (?, ?)", statements[0].GetProperty("query").GetString());
        Assert.Equal("integer", statements[0].GetProperty("values")[0].GetProperty("type").GetString());
        Assert.Equal(1, statements[0].GetProperty("values")[0].GetProperty("value").GetInt32());
        Assert.Equal("text", statements[0].GetProperty("values")[1].GetProperty("type").GetString());
        Assert.Equal("Ada", statements[0].GetProperty("values")[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task DurableObjectSqlStorageDispatchesCursors()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "handle": "sql-cursor:1",
              "columnNames": ["id", "name"],
              "rowsRead": 0,
              "rowsWritten": 0
            }
            """,
            """
            {
              "done": false,
              "value": { "id": 1, "name": "Ada" },
              "columnNames": ["id", "name"],
              "rowsRead": 1,
              "rowsWritten": 0
            }
            """,
            """
            {
              "done": false,
              "value": [2, "Grace"],
              "columnNames": ["id", "name"],
              "rowsRead": 2,
              "rowsWritten": 0
            }
            """,
            """
            {
              "done": true,
              "value": null,
              "columnNames": ["id", "name"],
              "rowsRead": 2,
              "rowsWritten": 0
            }
            """,
            "{}",
            """
            {
              "handle": "sql-cursor:2",
              "columnNames": ["id", "name"],
              "rowsRead": 0,
              "rowsWritten": 0
            }
            """,
            """
            {
              "done": false,
              "value": { "id": 3, "name": "Katherine" },
              "columnNames": ["id", "name"],
              "rowsRead": 1,
              "rowsWritten": 0
            }
            """,
            """
            {
              "done": true,
              "value": null,
              "columnNames": ["id", "name"],
              "rowsRead": 1,
              "rowsWritten": 0
            }
            """,
            "{}");
        var state = new DurableObjectState(
            "invocation-do-sql-cursor",
            new DurableObjectId("dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"),
            dispatcher);

        await using (var cursor = await state.Storage.Sql
            .Prepare("SELECT id, name FROM users WHERE id > ?")
            .Bind(0)
            .OpenCursorAsync<UserRow>())
        {
            Assert.Equal(["id", "name"], cursor.ColumnNames);
            Assert.Equal(0, cursor.RowsRead);

            var first = await cursor.NextAsync();
            var raw = await cursor.NextRawAsync();
            var done = await cursor.NextAsync();

            Assert.NotNull(first);
            Assert.Equal("Ada", first.Name);
            Assert.NotNull(raw);
            Assert.Equal(2, raw[0].GetInt32());
            Assert.Equal("Grace", raw[1].GetString());
            Assert.Null(done);
            Assert.Equal(2, cursor.RowsRead);
            Assert.Equal(0, cursor.RowsWritten);
        }

        var streamed = new List<UserRow>();
        var streamedCursor = await state.Storage.Sql
            .Prepare("SELECT id, name FROM users ORDER BY id")
            .OpenCursorAsync<UserRow>();
        await foreach (var row in streamedCursor.ReadAllAsync())
            streamed.Add(row);

        Assert.Equal("Katherine", streamed.Single().Name);
        Assert.Equal(
            [
                "durable.storage.sql.cursor.open",
                "durable.storage.sql.cursor.next",
                "durable.storage.sql.cursor.rawNext",
                "durable.storage.sql.cursor.next",
                "durable.storage.sql.cursor.dispose",
                "durable.storage.sql.cursor.open",
                "durable.storage.sql.cursor.next",
                "durable.storage.sql.cursor.next",
                "durable.storage.sql.cursor.dispose"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var openPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal(
            "SELECT id, name FROM users WHERE id > ?",
            openPayload.RootElement.GetProperty("query").GetString());
        Assert.Equal("integer", openPayload.RootElement.GetProperty("values")[0].GetProperty("type").GetString());
        Assert.Equal(0, openPayload.RootElement.GetProperty("values")[0].GetProperty("value").GetInt32());

        using var nextPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("sql-cursor:1", nextPayload.RootElement.GetProperty("handle").GetString());

        using var rawNextPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("sql-cursor:1", rawNextPayload.RootElement.GetProperty("handle").GetString());

        using var disposePayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal("sql-cursor:1", disposePayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task DurableObjectStorageDispatchesPointInTimeRecoveryBookmarks()
    {
        var dispatcher = new CapturingDispatcher(
            """{"bookmark":"0000007b-current"}""",
            """{"bookmark":"0000007b-time"}""",
            """{"bookmark":"0000007b-undo"}""");
        var state = new DurableObjectState(
            "invocation-do-pitr",
            new DurableObjectId("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"),
            dispatcher);

        var current = await state.Storage.GetCurrentBookmarkAsync();
        var bookmarkAtTime = await state.Storage.GetBookmarkForTimeAsync(
            DateTimeOffset.FromUnixTimeMilliseconds(1704067200123));
        var undoBookmark = await state.Storage.OnNextSessionRestoreBookmarkAsync(bookmarkAtTime);

        Assert.Equal("0000007b-current", current);
        Assert.Equal("0000007b-time", bookmarkAtTime);
        Assert.Equal("0000007b-undo", undoBookmark);
        Assert.Equal(
            [
                "durable.storage.getCurrentBookmark",
                "durable.storage.getBookmarkForTime",
                "durable.storage.onNextSessionRestoreBookmark"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var timePayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal(1704067200123, timePayload.RootElement.GetProperty("timestamp").GetInt64());

        using var restorePayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("0000007b-time", restorePayload.RootElement.GetProperty("bookmark").GetString());
    }

    [Fact]
    public async Task DurableObjectStateDispatchesAbort()
    {
        var dispatcher = new CapturingDispatcher("{}", "{}");
        var state = new DurableObjectState(
            "invocation-do-abort",
            new DurableObjectId("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"),
            dispatcher);

        await state.AbortAsync("restore requested");
        await state.AbortAsync();

        Assert.Equal(["durable.state.abort", "durable.state.abort"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$durableObjectState", call.BindingName));

        using var reasonPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("restore requested", reasonPayload.RootElement.GetProperty("reason").GetString());

        using var emptyPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal(JsonValueKind.Null, emptyPayload.RootElement.GetProperty("reason").ValueKind);
    }
}
