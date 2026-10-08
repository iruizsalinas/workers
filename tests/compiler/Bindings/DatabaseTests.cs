namespace Workers.Compiler.Tests;

public sealed class DatabaseTests
{
    [Fact]
    public void MapsPostgresClientToNodePostgres()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record User(int Id, string Name);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await using var db = await PostgresClient.ConnectAsync(env.Hyperdrive("HYPERDRIVE"));
                    var users = await db.QueryAsync<User>("select id, name from users where id = $1", [7]);
                    var result = await db.QueryAsync("delete from users");
                    return Response.Json(new { users, deleted = result.RowCount, result.Command });
                }
            }
            """);

        Assert.Contains("import { Client as $workers$PostgresClient } from \"pg\";", module);
        Assert.Contains("const db = await (client => (client.on(\"error\", () => { }), client.connect().then(() => client)))"
            + "(new $workers$PostgresClient({ connectionString: env[\"HYPERDRIVE\"].connectionString }));", module);
        Assert.Contains("await db.query(\"select id, name from users where id = $1\", $workers$sqlParameters([7])).then(value => value.rows)"
            + ".then(value => value.map(", module);
        Assert.Contains("$workers$User.$fromJSON(", module);
        Assert.Contains("let result = await db.query(\"delete from users\");", module);
        Assert.Contains("deleted: result.rowCount, command: result.command", module);
        Assert.Contains("} finally {\n    if (db != null) await db.end();\n  }", module.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void MapsMySqlClientToMySql2WithoutEval()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record User(int Id, string Name);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await using var db = await MySqlClient.ConnectAsync(env.Secret("MYSQL_URL"));
                    var users = await db.QueryAsync<User>("select id, name from users");
                    var inserted = await db.QueryAsync("insert into users (name) values (?)", ["Ada"]);
                    return Response.Json(new { users, inserted.InsertId, inserted.AffectedRows });
                }
            }
            """);

        Assert.Contains("import { createConnection as $workers$createMySqlConnection } from \"mysql2/promise\";", module);
        Assert.Contains("await $workers$createMySqlConnection({ uri: env[\"MYSQL_URL\"], disableEval: true })", module);
        Assert.Contains("await db.query(\"select id, name from users\").then(([value]) => value).then(value => value.map(", module);
        Assert.Contains("await db.query(\"insert into users (name) values (?)\", $workers$sqlParameters([\"Ada\"])).then(([value]) => value)", module);
        Assert.Contains("insertId: inserted.insertId, affectedRows: inserted.affectedRows", module);
    }

    [Fact]
    public void MapsMySqlHyperdriveBindingToConnectionFields()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await using var db = await MySqlClient.ConnectAsync(env.Hyperdrive("MYSQL"));
                    return Response.Text("ok");
                }
            }
            """);

        Assert.Contains("binding => ({ host: binding.host, port: binding.port, user: binding.user, password: binding.password, "
            + "database: binding.database, disableEval: true }))(env[\"MYSQL\"])", module);
    }

    [Fact]
    public void MapsMongoCollectionsToTheOfficialDriver()
    {
        var module = Compile("""
            using System.Collections.Generic;
            using System.Text.Json.Serialization;
            using Workers;
            using System.Threading.Tasks;
            public sealed record Person([property: JsonPropertyName("_id")] string? Id, string Name, int Age);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await using var client = await MongoClient.ConnectAsync(env.Secret("MONGODB_URL"));
                    var people = client.Database("app").Collection<Person>("people");
                    var inserted = await people.InsertOneAsync(new Person(null, "Ada", 36));
                    var person = await people.FindOneAsync(new Dictionary<string, object?> { ["_id"] = MongoClient.ObjectId(inserted.InsertedId) });
                    var adults = await people.FindAsync(new { age = 36 }, new MongoFindOptions { Sort = new { name = 1 }, Limit = 10 });
                    var updated = await people.UpdateOneAsync(new { name = "Ada" }, new Dictionary<string, object?> { ["$inc"] = new { age = 1 } });
                    var count = await people.CountDocumentsAsync();
                    return Response.Json(new { person, adults, updated.ModifiedCount, count });
                }
            }
            """);

        Assert.Contains("import { MongoClient as $workers$MongoClient } from \"mongodb\";", module);
        Assert.Contains("import { ObjectId as $workers$ObjectId } from \"mongodb\";", module);
        Assert.Contains("const client = await new $workers$MongoClient(env[\"MONGODB_URL\"]).connect();", module);
        Assert.Contains("let people = client.db(\"app\").collection(\"people\");", module);
        Assert.Contains("await people.insertOne($workers$mongoDocument(new $workers$Person(null, \"Ada\", 36), true)).then($workers$mongoValue)", module);
        Assert.Contains("people.findOne($workers$mongoDocument(Object.assign(Object.create(null), { [\"_id\"]: new $workers$ObjectId(inserted.insertedId) })) ?? {})"
            + ".then($workers$mongoValue).then(", module);
        Assert.Contains("people.find($workers$mongoDocument({ age: 36 }) ?? {}, $workers$mongoDocument({ sort: { name: 1 }, limit: 10 }) ?? undefined)"
            + ".toArray().then(documents => documents.map($workers$mongoValue)).then(value => value.map(", module);
        Assert.Contains("await people.countDocuments({})", module);
        Assert.Contains("if (client != null) await client.close();", module);
    }

    [Fact]
    public void ConvertsDriverRowValuesInRowMode()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record Row(long Total, byte[] Payload);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await using var db = await PostgresClient.ConnectAsync("postgres://localhost/app");
                    return Response.Json(await db.QueryAsync<Row>("select total, payload from rows"));
                }
            }
            """);

        Assert.Contains("if (mode === 2 && kind >= 2 && kind <= 4 && (typeof value === \"bigint\" || typeof value === \"string\"", module);
        Assert.Contains("if (mode === 2 && kind === 6 && (ArrayBuffer.isView(value) || value instanceof ArrayBuffer))", module);
    }

    [Fact]
    public void DisposesUsingStatementResourcesAfterTheirBody()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    await using (var first = await PostgresClient.ConnectAsync("postgres://one"))
                    await using (var second = await MySqlClient.ConnectAsync("mysql://two"))
                    {
                        await first.QueryAsync("select 1");
                    }
                    return Response.Text("done");
                }
            }
            """).ReplaceLineEndings("\n");

        Assert.Contains("""
              {
                const first = await (client =>
            """.ReplaceLineEndings("\n"), module);
        Assert.Contains("""
                try {
                  {
                    const second = await $workers$createMySqlConnection({ uri: "mysql://two", disableEval: true });
                    try {
                      await first.query("select 1");
                    } finally {
                      if (second != null) await second.end();
                    }
                  }
                } finally {
                  if (first != null) await first.end();
                }
              }
              return new Response("done"
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
                public void Dispose() => Console.WriteLine("released");
            }
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    using var lease = new Lease();
                    Console.WriteLine("working");
                    return Response.Text("ok");
                }
            }
            """).ReplaceLineEndings("\n");

        Assert.Contains("""
              const lease = new $workers$Lease();
              try {
                console.log("working");
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
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(new { max = 9007199254740991L, small = 7UL });
            }
            """);
        Assert.Contains("max: 9007199254740991, small: 7", module);

        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context) =>
                    Response.Json(9007199254740992L);
            }
            """));
        Assert.Contains("WRK108", error.Message);
    }

    [Fact]
    public void DefaultsMissingDateAndTimeSpanMembers()
    {
        var module = Compile("""
            using System;
            using Workers;
            using System.Threading.Tasks;
            public sealed record Event(DateTimeOffset At, TimeSpan Duration);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context) =>
                    Response.Json(await request.JsonAsync<Event>());
            }
            """);

        Assert.Contains(": new Date(-62135596800000)", module);
    }

    [Fact]
    public void PassesCancellationToMongoReadsAndChecksItBeforeWrites()
    {
        var module = Compile("""
            using System.Threading;
            using Workers;
            using System.Threading.Tasks;
            public sealed record Person(string Name);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    using var source = new CancellationTokenSource();
                    await using var client = await MongoClient.ConnectAsync("mongodb://localhost");
                    var people = client.Database("app").Collection<Person>("people");
                    var found = await people.FindOneAsync(new { name = "Ada" }, source.Token);
                    await people.InsertOneAsync(new Person("Grace"), source.Token);
                    return Response.Json(found);
                }
            }
            """);

        Assert.Contains(".findOne($workers$mongoDocument($workers$arg1) ?? {}, { signal: $workers$arg2 ?? undefined })", module);
        Assert.Contains("($workers$cancellationCheck($workers$arg2$2), $workers$receiver$2.insertOne(", module);
        Assert.DoesNotContain("insertOne($workers$mongoDocument($workers$arg1$2, true), { signal", module);
        Assert.Contains("if (source != null) $workers$cancellationCancelAfter(source, -1);", module);
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
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var db = env.D1("DB");
                    var inserted = await db.Prepare("insert into rows default values").RunAsync();
                    var batch = await db.BatchAsync<Row>([db.Prepare("select id from rows")]);
                    return Response.Json(new { inserted.Meta.LastRowId, inserted.Meta.Timings!.SqlDurationMs, batch[0].Meta.RowsRead });
                }
            }
            """);

        Assert.Contains("lastRowId: inserted.meta.last_row_id", module);
        Assert.Contains("sqlDurationMs: inserted.meta.timings.sql_duration_ms", module);
        Assert.Contains(".meta.rows_read", module);
        Assert.Contains("db.batch([db.prepare(\"select id from rows\")]).then(value => value.map(result => (result.results = result.results.map(", module);
    }

    [Fact]
    public void ExposesHyperdriveSocketAndAddress()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, Context context)
                {
                    var hyperdrive = env.Hyperdrive("HYPERDRIVE");
                    var info = await hyperdrive.GetConnectionInfoAsync();
                    var socket = hyperdrive.Connect();
                    await socket.CloseAsync();
                    return Response.Text(info.Ip);
                }
            }
            """);

        Assert.Contains("let socket = hyperdrive.connect();", module);
        Assert.Contains("new Response(info.ip", module);
    }
}
