using System.Text.Json.Serialization;
using Workers;

namespace Databases;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env env, Context context) => request.Path switch
    {
        "/postgres" => Response.Json(await PostgresAsync(await PostgresClient.ConnectAsync(env.Variable("POSTGRES_URL")))),
        "/postgres-hyperdrive" => Response.Json(await PostgresAsync(await PostgresClient.ConnectAsync(env.Hyperdrive("POSTGRES")))),
        "/mysql" => Response.Json(await MySqlAsync(await MySqlClient.ConnectAsync(env.Variable("MYSQL_URL")))),
        "/mysql-hyperdrive" => Response.Json(await MySqlAsync(await MySqlClient.ConnectAsync(env.Hyperdrive("MYSQL")))),
        "/mongo" => Response.Json(await MongoAsync(env.Variable("MONGODB_URL"))),
        "/mongo-canceled" => Response.Text(await MongoCanceledAsync(env.Variable("MONGODB_URL"))),
        _ => Response.Text("Not found", 404)
    };

    private static async Task<SqlReport> PostgresAsync(PostgresClient connection)
    {
        await using var db = connection;
        await db.QueryAsync("create temporary table items (id serial primary key, name text not null, total int8 not null, active boolean not null, created timestamptz not null, payload bytea)");
        var inserted = await db.QueryAsync(
            "insert into items (name, total, active, created, payload) values ($1, $2, $3, $4, $5), ($6, $7, $8, $9, null)",
            ["first", 9007199254740991L, true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), new byte[] { 1, 2, 3 },
                "second", 7L, false, new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero)]);
        var items = await db.QueryAsync<Item>("select id, name, total, active, created, payload from items order by id");
        var count = (await db.QueryAsync<Count>("select count(*) as value from items where active = $1", [true]))[0];
        return new SqlReport(inserted.RowCount ?? -1, 0, items, count.Value);
    }

    private static async Task<SqlReport> MySqlAsync(MySqlClient connection)
    {
        await using var db = connection;
        await db.QueryAsync("create temporary table items (id int auto_increment primary key, name varchar(50) not null, total bigint not null, active tinyint(1) not null, created datetime(3) not null, payload blob)");
        var inserted = await db.QueryAsync(
            "insert into items (name, total, active, created, payload) values (?, ?, ?, ?, ?), (?, ?, ?, ?, null)",
            ["first", 9007199254740991L, true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), new byte[] { 1, 2, 3 },
                "second", 7L, false, new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero)]);
        var items = await db.QueryAsync<Item>("select id, name, total, active, created, payload from items order by id");
        var count = (await db.QueryAsync<Count>("select count(*) as value from items where active = ?", [true]))[0];
        return new SqlReport(inserted.AffectedRows, inserted.InsertId, items, count.Value);
    }

    private static async Task<MongoReport> MongoAsync(string connectionString)
    {
        await using var client = await MongoClient.ConnectAsync(connectionString);
        var people = client.Database("workers_runtime_tests").Collection<Person>("people_" + Guid.NewGuid().ToString("N"));
        var ada = await people.InsertOneAsync(new Person(null, "Ada", 36, ["math"], new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)));
        var many = await people.InsertManyAsync([
            new Person(null, "Grace", 45, ["navy", "cobol"], new DateTimeOffset(2026, 2, 3, 4, 5, 6, TimeSpan.Zero)),
            new Person(null, "Linus", 28, [], new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero))
        ]);
        var found = await people.FindOneAsync(new Dictionary<string, object?> { ["_id"] = MongoClient.ObjectId(ada.InsertedId) });
        var updated = await people.UpdateOneAsync(new { name = "Linus" },
            new Dictionary<string, object?> { ["$set"] = new { age = 29 }, ["$push"] = new { tags = "linux" } });
        var adults = await people.FindAsync(
            new Dictionary<string, object?> { ["age"] = new Dictionary<string, object?> { ["$gte"] = 29 } },
            new MongoFindOptions { Sort = new { age = -1 }, Limit = 2 });
        var count = await people.CountDocumentsAsync();
        var totals = await people.AggregateAsync<AgeTotal>([
            new Dictionary<string, object?> { ["$group"] = new Dictionary<string, object?> { ["_id"] = null, ["total"] = new Dictionary<string, object?> { ["$sum"] = "$age" } } }
        ]);
        var deleted = await people.DeleteManyAsync(new { });
        return new MongoReport(ada.InsertedId, many.InsertedCount, found, updated.MatchedCount, updated.ModifiedCount,
            adults, count, totals[0].Total, deleted.DeletedCount);
    }

    private static async Task<string> MongoCanceledAsync(string connectionString)
    {
        await using var client = await MongoClient.ConnectAsync(connectionString);
        var people = client.Database("workers_runtime_tests").Collection<Person>("slow");
        await people.InsertOneAsync(new Person(null, "Slow", 1, [], DateTimeOffset.UtcNow));
        var source = new CancellationTokenSource();
        source.CancelAfter(100);
        try
        {
            await people.FindAsync(new Dictionary<string, object?> { ["$where"] = "sleep(2000) || true" }, cancellationToken: source.Token);
            return "completed";
        }
        catch (Exception)
        {
            return "canceled";
        }
        finally
        {
            await people.DeleteManyAsync(new { });
        }
    }
}

public sealed record Item(int Id, string Name, long Total, bool Active, DateTimeOffset Created, byte[]? Payload);
public sealed record Count(long Value);
public sealed record SqlReport(int Inserted, long InsertId, IReadOnlyList<Item> Items, long ActiveCount);
public sealed record Person([property: JsonPropertyName("_id")] string? Id, string Name, int Age, List<string> Tags, DateTimeOffset Joined);
public sealed record AgeTotal(int Total);
public sealed record MongoReport(
    string InsertedId, int InsertedCount, Person? Found, long Matched, long Modified,
    IReadOnlyList<Person> Adults, long Count, int TotalAge, long Deleted);
