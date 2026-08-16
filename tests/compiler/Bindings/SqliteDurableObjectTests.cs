namespace Workers.Compiler.Tests;

public sealed class SqliteDurableObjectTests
{
    [Fact]
    public void EmitsSynchronousSqliteStorageCalls()
    {
        var module = Compile("""
            using Workers;
            [DurableObject("Store")]
            public sealed class Store
            {
                private readonly DurableObjectState state;
                public Store(DurableObjectState state, Env env) => this.state = state;
                public Response FetchAsync(Request request)
                {
                    state.Storage.TransactionSync(() => state.Storage.Sql.Exec<object>("DELETE FROM rows"));
                    state.Storage.Kv.Put("key", new { value = 1 });
                    var rows = state.Storage.Sql.Exec<Row>("SELECT value FROM rows").ToArray();
                    return Response.Json(rows);
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
