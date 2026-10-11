namespace Workers.Compiler.Tests;

public sealed class SqliteDurableObjectTests
{
    [Fact]
    public void EmitsSynchronousSqliteStorageCalls()
    {
        var module = Compile("""
            using Workers;
            public sealed class Store : DurableObject
            {
                public override Task<Response> FetchAsync(Request request)
                {
                    Ctx.Storage.TransactionSync(() => Ctx.Storage.Sql.Exec<object>("DELETE FROM rows"));
                    Ctx.Storage.Kv.Put("key", new { value = 1 });
                    var rows = Ctx.Storage.Sql.Exec<Row>("SELECT value FROM rows").ToArray();
                    return Task.FromResult(Response.Json(rows));
                }
            }
            public sealed record Row(int Value);
            """);

        Assert.Contains("storage.transactionSync", module);
        Assert.Contains("storage.sql.exec(\"DELETE FROM rows\")", module);
        Assert.Contains("storage.kv.put(\"key\"", module);
        Assert.Contains(".toArray()", module);
    }
}
