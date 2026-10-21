namespace Workers.Compiler.Tests;

public sealed class PlatformEnumerableRegressionTests
{
    [Theory]
    [InlineData("Ai", "ToMarkdownAsync", "toMarkdown")]
    [InlineData("ToMarkdownService", "TransformAsync", "transform")]
    public void MarkdownBatchesMaterializeLazySequences(string type, string method, string nativeMethod)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                private static int visits;
                private static IEnumerable<MarkdownDocument> Documents()
                {
                    visits++;
                    yield return new MarkdownDocument { Name = "one.html", Blob = new Blob("one") };
                    visits++;
                    yield return new MarkdownDocument { Name = "two.html", Blob = new Blob("two") };
                }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    int count = -1;
                    string first = "";
                    var binding = JsonValue.From(new {
                        {{nativeMethod}} = (Func<MarkdownDocument[], Task<ConversionResponse[]>>)(files => {
                            count = files.Length;
                            first = files[0].Name;
                            return Task.FromResult(new ConversionResponse[0]);
                        })
                    }).As<{{type}}>()!;
                    await binding.{{method}}(Documents());
                    return Response.Json(new { count, first, visits });
                }
            }
            """);

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal("one.html", result.GetProperty("first").GetString());
        Assert.Equal(2, result.GetProperty("visits").GetInt32());
    }

    [Theory]
    [InlineData("Ai", "ToMarkdownAsync", "toMarkdown")]
    [InlineData("ToMarkdownService", "TransformAsync", "transform")]
    public void SingleMarkdownDocumentKeepsItsNativeShape(string type, string method, string nativeMethod)
    {
        var result = JavaScriptRunner.FetchJson($$"""
            using Workers;
            public static class Worker
            {
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    var binding = JsonValue.From(new {
                        {{nativeMethod}} = (Func<MarkdownDocument, Task<ConversionResponse>>)(file =>
                            Task.FromResult(JsonValue.From(new { name = file.Name }).As<ConversionResponse>()!))
                    }).As<{{type}}>()!;
                    var converted = await binding.{{method}}(new MarkdownDocument { Name = "one.html", Blob = new Blob("one") });
                    return Response.Json(converted.Name);
                }
            }
            """);

        Assert.Equal("one.html", result.GetString());
    }

    [Fact]
    public void GatewayBatchesMaterializeLazySequences()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static IEnumerable<AIGatewayUniversalRequest> Requests()
                {
                    yield return new AIGatewayUniversalRequest {
                        Provider = "workers-ai", Endpoint = "first", Headers = new Dictionary<string, object>(), Query = null
                    };
                    yield return new AIGatewayUniversalRequest {
                        Provider = "workers-ai", Endpoint = "second", Headers = new Dictionary<string, object>(), Query = null
                    };
                }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    var gateway = JsonValue.From(new {
                        run = (Func<AIGatewayUniversalRequest[], Task<Response>>)(batch =>
                            Task.FromResult(Response.Json(new { count = batch.Length, first = batch[0].Endpoint })))
                    }).As<AiGateway>()!;
                    return await gateway.RunAsync(Requests());
                }
            }
            """);

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal("first", result.GetProperty("first").GetString());
    }

    [Fact]
    public void WorkflowDeletionMaterializesLazyIds()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static IEnumerable<string> Ids() { yield return "one"; yield return "two"; }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    int count = -1;
                    string first = "";
                    var workflow = JsonValue.From(new {
                        deleteBatch = (Func<string[], Task<WorkflowBatchDeleteResult>>)(ids => {
                            count = ids.Length;
                            first = ids[0];
                            return Task.FromResult(Json.Parse("{}").As<WorkflowBatchDeleteResult>()!);
                        })
                    }).As<Workflow>()!;
                    await workflow.DeleteBatchAsync(Ids());
                    return Response.Json(new { count, first });
                }
            }
            """);

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal("one", result.GetProperty("first").GetString());
    }

    [Fact]
    public void LegacyWorkflowCreationMaterializesLazyInstances()
    {
        var result = JavaScriptRunner.FetchJson("""
            using Workers;
            public static class Worker
            {
                private static IEnumerable<WorkflowInstanceCreateOptions> Instances()
                {
                    yield return new WorkflowInstanceCreateOptions { Id = "one" };
                    yield return new WorkflowInstanceCreateOptions { Id = "two" };
                }
                [Fetch] public static async Task<Response> Fetch(Request request)
                {
                    int count = -1;
                    string first = "";
                    var workflow = JsonValue.From(new {
                        createBatch = (Func<WorkflowInstanceCreateOptions[], Task<WorkflowInstance[]>>)(instances => {
                            count = instances.Length;
                            first = instances[0].Id!;
                            return Task.FromResult(new WorkflowInstance[0]);
                        })
                    }).As<Workflow>()!;
                    await workflow.CreateBatchAsync(Instances());
                    return Response.Json(new { count, first });
                }
            }
            """);

        Assert.Equal(2, result.GetProperty("count").GetInt32());
        Assert.Equal("one", result.GetProperty("first").GetString());
    }
}
