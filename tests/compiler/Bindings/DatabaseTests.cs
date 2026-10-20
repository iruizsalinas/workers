namespace Workers.Compiler.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void ReadsD1RowsWithTheJsonNamesAndSqliteBooleans()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record Row(long Total, bool Active, [property: JsonName("created_at")] Date? CreatedAt);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var rows = await env.D1("DB").Prepare("select total, active, created_at from rows").AllAsync<Row>();
                    return Response.Json(rows.Results);
                }
            }
            """);

        Assert.Contains("value.results.map(value => value == null ? value : $workers$Row.$fromRow(value))", module);
        Assert.Contains("$workers$Row.$fromRow = function (source) {", module);
        Assert.Contains("Object.hasOwn(source, \"total\") ? source[\"total\"] : 0", module);
        Assert.Contains("Object.hasOwn(source, \"active\") ? (value => value == null ? value : Boolean(value))(source[\"active\"]) : false", module);
        Assert.Contains("Object.hasOwn(source, \"created_at\") ? (value => value == null ? value : new Date(value))(source[\"created_at\"]) : null", module);
        Assert.DoesNotContain("mode", module);
    }

    [Fact]
    public void DisposesUsingStatementResourcesAfterTheirBody()
    {
        var module = Compile("""
            using System;
            using Workers;
            using System.Threading.Tasks;
            public sealed class Lease : IAsyncDisposable
            {
                public async ValueTask DisposeAsync() => await Scheduler.WaitAsync(0);
            }
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    await using (var first = new Lease())
                    await using (var second = new Lease())
                    {
                        Log.Info("working");
                    }
                    return new Response("done");
                }
            }
            """).ReplaceLineEndings("\n");

        Assert.Contains("""
                const first = new $workers$Lease();
                try {
                  {
                    const second = new $workers$Lease();
                    try {
                      console.info("working");
                    } finally {
                      if (second != null) await second.$workers$cs$Lease$dispose();
                    }
                  }
                } finally {
                  if (first != null) await first.$workers$cs$Lease$dispose();
                }
            """.ReplaceLineEndings("\n"), module);
    }

    [Fact]
    public void DisposesUserTypesThroughTheirDisposeMethod()
    {
        var module = Compile("""
            using System;
            using Workers;
            public sealed class Lease : IDisposable
            {
                public void Dispose() => Log.Info("released");
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context)
                {
                    using var lease = new Lease();
                    Log.Info("working");
                    return new Response("ok");
                }
            }
            """).ReplaceLineEndings("\n");

        Assert.Contains("""
              const lease = new $workers$Lease();
              try {
                console.info("working");
                return new Response("ok"
            """.ReplaceLineEndings("\n"), module);
        Assert.Contains("if (lease != null) lease.", module);
    }

    [Fact]
    public void AllowsSafeInt64LiteralsAndRejectsUnsafeOnes()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) =>
                    Response.Json(new { max = 9007199254740991L, small = 7UL });
            }
            """);
        Assert.Contains("max: 9007199254740991, small: 7", module);

        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, ExecutionContext context) =>
                    Response.Json(9007199254740992L);
            }
            """));
        Assert.Contains("WRK108", error.Message);
    }

    [Fact]
    public void ReadsD1MetadataAndMaterializesBatchRows()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record Row(int Id);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var db = env.D1("DB");
                    var inserted = await db.Prepare("insert into rows default values").RunAsync();
                    var batch = await db.BatchAsync<Row>([db.Prepare("select id from rows")]);
                    return Response.Json(new { inserted.Meta.LastRowId, inserted.Meta.Timings!.SqlDurationMs, batch[0].Meta.RowsRead });
                }
            }
            """);

        Assert.Contains("lastRowId: $workers$notNull($workers$notNull(inserted).meta).last_row_id", module);
        Assert.Contains("sqlDurationMs: $workers$notNull($workers$notNull($workers$notNull(inserted).meta).timings).sql_duration_ms", module);
        Assert.Contains("$workers$notNull($workers$notNull($workers$sequenceIndex(batch, 0)).meta).rows_read", module);
        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).batch(Array.from($workers$arg1)))(db, [$workers$notNull(db).prepare(\"select id from rows\")]).then(value => value.map(result => (result.results = result.results.map(value => value == null ? value : $workers$Row.$fromRow(value)), result)))", module);
    }

    [Fact]
    public void ExposesHyperdriveFieldsAndSocket()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext context)
                {
                    var hyperdrive = env.Hyperdrive("HYPERDRIVE");
                    var socket = hyperdrive.Connect();
                    await socket.CloseAsync();
                    return new Response($"{hyperdrive.Host}:{hyperdrive.Port}/{hyperdrive.Database}");
                }
            }
            """);

        Assert.Contains("let socket = $workers$notNull(hyperdrive).connect();", module);
        Assert.Contains("`${($workers$notNull(hyperdrive).host) ?? \"\"}:${$workers$notNull(hyperdrive).port}/${($workers$notNull(hyperdrive).database) ?? \"\"}`", module);
    }
}
