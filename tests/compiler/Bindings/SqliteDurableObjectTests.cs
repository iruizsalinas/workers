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

        Assert.Contains("$workers$notNull($workers$notNull(this.ctx).storage).transactionSync", module);
        Assert.Contains("$workers$notNull($workers$notNull($workers$notNull(this.ctx).storage).sql).exec(\"DELETE FROM rows\")", module);
        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver).put($workers$arg1, $workers$arg2))($workers$notNull($workers$notNull(this.ctx).storage).kv, \"key\", { value: 1 })", module);
        Assert.Contains(".toArray()", module);
    }
}
