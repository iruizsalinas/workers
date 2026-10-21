namespace Workers.Compiler.Tests;

public sealed class VectorizeEnumerableRegressionTests
{
    [Theory]
    [InlineData("InsertAsync", "insert")]
    [InlineData("UpsertAsync", "upsert")]
    public void VectorBatchesMaterializeLazySequences(string method, string nativeMethod)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                private static IEnumerable<VectorizeVector> Vectors()
                {
                    yield return new VectorizeVector { Id = "one", Values = [1, 2] };
                    yield return new VectorizeVector { Id = "two", Values = [3, 4] };
                }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    int count = -1;
                    string first = "";
                    var index = JsonValue.From(new {
                        {{nativeMethod}} = (Func<VectorizeVector[], Task<VectorizeAsyncMutation>>)(vectors => {
                            count = vectors.Length;
                            first = vectors[0].Id;
                            return Task.FromResult(Json.Parse("{}").As<VectorizeAsyncMutation>()!);
                        })
                    }).As<Vectorize>()!;
                    await index.{{method}}(Vectors());
                    return Response.Json(new { count, first });
                }
            }
            """);

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal("one", result.GetProperty("first").GetString());
    }

    [Theory]
    [InlineData("DeleteByIdsAsync", "deleteByIds")]
    [InlineData("GetByIdsAsync", "getByIds")]
    public void IdBatchesSerializeLazySequencesAsArrays(string method, string nativeMethod)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                private static IEnumerable<string> Ids() { yield return "one"; yield return "two"; }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    string payload = "";
                    var index = JsonValue.From(new {
                        {{nativeMethod}} = (Func<string[], Task<JsonValue>>)(ids => {
                            payload = Json.Stringify(new { ids });
                            return Task.FromResult(Json.Parse("{}"));
                        })
                    }).As<Vectorize>()!;
                    await index.{{method}}(Ids());
                    return Response.Json(payload);
                }
            }
            """);

        Assert.Equal("{\"ids\":[\"one\",\"two\"]}", result.GetString());
    }
}
