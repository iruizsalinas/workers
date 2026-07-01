using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task VectorizeDispatchesIndexOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"mutationId":"mut-insert"}""",
            """{"mutationId":"mut-upsert"}""",
            """
            {
              "matches": [
                {
                  "id": "doc-1",
                  "score": 0.98,
                  "values": [0.1, 0.2],
                  "metadata": { "title": "Hello" }
                }
              ],
              "count": 1
            }
            """,
            """
            {
              "matches": [
                {
                  "id": "doc-2",
                  "score": 0.88
                }
              ],
              "count": 1
            }
            """,
            """[{"id":"doc-1","values":[0.1,0.2],"namespace":"docs","metadata":{"title":"Hello"}}]""",
            """{"mutationId":"mut-delete"}""",
            """{"dimensions":768,"metric":"cosine","vectorCount":42}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-vectorize");
        var index = environment.Vectorize("DOCS");
        var metadata = JsonSerializer.SerializeToElement(new { title = "Hello" });
        var filter = JsonSerializer.SerializeToElement(new { source = "docs" });

        var inserted = await index.InsertAsync(
            [
                new VectorizeVector
                {
                    Id = "doc-1",
                    Values = [0.1, 0.2],
                    Namespace = "docs",
                    Metadata = metadata
                }
            ]);
        var upserted = await index.UpsertAsync(
            [
                new VectorizeVector
                {
                    Id = "doc-2",
                    Values = [0.3, 0.4]
                }
            ]);
        var query = await index.QueryAsync(
            [0.1, 0.2],
            new VectorizeQueryOptions
            {
                TopK = 5,
                ReturnValues = true,
                ReturnMetadata = VectorizeReturnMetadata.All,
                Filter = filter,
                Namespace = "docs"
            });
        var queryById = await index.QueryByIdAsync(
            "doc-2",
            new VectorizeQueryOptions { ReturnMetadata = VectorizeReturnMetadata.Indexed });
        var vectors = await index.GetByIdsAsync(["doc-1"]);
        var deleted = await index.DeleteByIdsAsync(["doc-2"]);
        var details = await index.DescribeAsync();

        Assert.Equal("mut-insert", inserted.MutationId);
        Assert.Equal("mut-upsert", upserted.MutationId);
        var match = Assert.Single(query.Matches);
        Assert.Equal("doc-1", match.Id);
        Assert.Equal(0.98, match.Score);
        Assert.Equal([0.1, 0.2], match.Values);
        Assert.Equal("Hello", match.Metadata?.GetProperty("title").GetString());
        Assert.Equal("doc-2", Assert.Single(queryById.Matches).Id);
        Assert.Equal("doc-1", Assert.Single(vectors).Id);
        Assert.Equal("Hello", vectors.Single().Metadata?.GetProperty("title").GetString());
        Assert.Equal("mut-delete", deleted.MutationId);
        Assert.Equal(768, details.Dimensions);
        Assert.Equal("cosine", details.Metric);
        Assert.Equal(42, details.VectorCount);
        Assert.Equal(
            [
                "vectorize.insert",
                "vectorize.upsert",
                "vectorize.query",
                "vectorize.queryById",
                "vectorize.getByIds",
                "vectorize.deleteByIds",
                "vectorize.describe"
            ],
            dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("DOCS", invocation.BindingName));

        using var insertPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        var vector = insertPayload.RootElement.GetProperty("vectors")[0];
        Assert.Equal("doc-1", vector.GetProperty("id").GetString());
        Assert.Equal("docs", vector.GetProperty("namespace").GetString());
        Assert.Equal(0.1, vector.GetProperty("values")[0].GetDouble());
        Assert.Equal("Hello", vector.GetProperty("metadata").GetProperty("title").GetString());

        using var queryPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal(5, queryPayload.RootElement.GetProperty("options").GetProperty("topK").GetInt32());
        Assert.True(queryPayload.RootElement.GetProperty("options").GetProperty("returnValues").GetBoolean());
        Assert.Equal("all", queryPayload.RootElement.GetProperty("options").GetProperty("returnMetadata").GetString());
        Assert.Equal("docs", queryPayload.RootElement.GetProperty("options").GetProperty("namespace").GetString());
        Assert.Equal("docs", queryPayload.RootElement.GetProperty("options").GetProperty("filter").GetProperty("source").GetString());

        using var queryByIdPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal("doc-2", queryByIdPayload.RootElement.GetProperty("id").GetString());
        Assert.Equal("indexed", queryByIdPayload.RootElement.GetProperty("options").GetProperty("returnMetadata").GetString());

        using var deletePayload = JsonDocument.Parse(dispatcher.Invocations[5].PayloadJson);
        Assert.Equal("doc-2", deletePayload.RootElement.GetProperty("ids")[0].GetString());
    }

    [Fact]
    public async Task VectorizeRejectsInvalidInputs()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-vectorize-invalid");
        var index = environment.Vectorize("DOCS");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => index.InsertAsync([]));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            index.InsertAsync([new VectorizeVector { Id = "doc-1", Values = [] }]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            index.QueryAsync([0.1], new VectorizeQueryOptions { TopK = 101 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            index.QueryAsync(
                [0.1],
                new VectorizeQueryOptions
                {
                    TopK = 51,
                    ReturnMetadata = VectorizeReturnMetadata.All
                }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => index.DeleteByIdsAsync([]));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task WorkflowBindingDispatchesLifecycleOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"id":"created-1"}""",
            """{"instances":[{"id":"batch-1"},{"id":"batch-2"}]}""",
            """{"id":"created-1"}""",
            """
            {
              "status": "running",
              "error": null,
              "output": {
                "result": "ok"
              },
              "rollback": {
                "outcome": "complete",
                "error": null
              }
            }
            """,
            "{}",
            "{}",
            "{}",
            "{}",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-workflow");
        var workflow = environment.Workflow("BILLING");

        var created = await workflow.CreateAsync(new WorkflowInstanceCreateOptions
        {
            Id = "created-1",
            Params = new
            {
                invoiceId = "inv-1"
            },
            Retention = new WorkflowRetentionOptions
            {
                SuccessRetention = "1 day",
                ErrorRetention = "7 days"
            }
        });
        var batch = await workflow.CreateBatchAsync(
        [
            new WorkflowInstanceCreateOptions { Id = "batch-1", Params = new { invoiceId = "inv-2" } },
            new WorkflowInstanceCreateOptions { Id = "batch-2", Params = new { invoiceId = "inv-3" } }
        ]);
        var existing = await workflow.GetAsync("created-1");
        var status = await existing.StatusAsync();
        await existing.PauseAsync();
        await existing.ResumeAsync();
        await existing.RestartAsync(new WorkflowInstanceRestartOptions
        {
            From = new WorkflowRestartFromStep
            {
                Name = "charge-card",
                Count = 2,
                Type = "do"
            }
        });
        await existing.SendEventAsync(new WorkflowInstanceEventOptions
        {
            Type = "stripe-webhook",
            Payload = new
            {
                paid = true
            }
        });
        await existing.TerminateAsync();

        Assert.Equal("created-1", created.Id);
        Assert.Equal(["batch-1", "batch-2"], batch.Select(static instance => instance.Id));
        Assert.Equal("created-1", existing.Id);
        Assert.Equal("running", status.Status);
        Assert.Equal("ok", status.Output!.Value.GetProperty("result").GetString());
        Assert.NotNull(status.Rollback);
        Assert.Equal("complete", status.Rollback.Outcome);
        Assert.Equal(
            [
                "workflow.create",
                "workflow.createBatch",
                "workflow.get",
                "workflow.instance.status",
                "workflow.instance.pause",
                "workflow.instance.resume",
                "workflow.instance.restart",
                "workflow.instance.sendEvent",
                "workflow.instance.terminate"
            ],
            dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("BILLING", invocation.BindingName));

        using var createPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        var createOptions = createPayload.RootElement.GetProperty("options");
        Assert.Equal("created-1", createOptions.GetProperty("id").GetString());
        Assert.Equal("inv-1", createOptions.GetProperty("params").GetProperty("invoiceId").GetString());
        Assert.Equal("1 day", createOptions.GetProperty("retention").GetProperty("successRetention").GetString());
        Assert.Equal("7 days", createOptions.GetProperty("retention").GetProperty("errorRetention").GetString());

        using var batchPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("batch-1", batchPayload.RootElement.GetProperty("batch")[0].GetProperty("id").GetString());
        Assert.Equal("inv-3", batchPayload.RootElement.GetProperty("batch")[1].GetProperty("params").GetProperty("invoiceId").GetString());

        using var restartPayload = JsonDocument.Parse(dispatcher.Invocations[6].PayloadJson);
        var from = restartPayload.RootElement.GetProperty("options").GetProperty("from");
        Assert.Equal("created-1", restartPayload.RootElement.GetProperty("id").GetString());
        Assert.Equal("charge-card", from.GetProperty("name").GetString());
        Assert.Equal(2, from.GetProperty("count").GetInt32());
        Assert.Equal("do", from.GetProperty("type").GetString());

        using var eventPayload = JsonDocument.Parse(dispatcher.Invocations[7].PayloadJson);
        Assert.Equal("stripe-webhook", eventPayload.RootElement.GetProperty("options").GetProperty("type").GetString());
        Assert.True(eventPayload.RootElement.GetProperty("options").GetProperty("payload").GetProperty("paid").GetBoolean());
    }

    [Fact]
    public async Task WorkflowBindingRejectsInvalidCreateInputs()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var workflow = EnvironmentWithInvocation("invocation-workflow-invalid").Workflow("BILLING");
        var tooMany = Enumerable.Range(0, 101)
            .Select(static index => new WorkflowInstanceCreateOptions { Id = $"batch-{index}", Params = new { index } });

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            workflow.CreateAsync(new WorkflowInstanceCreateOptions { Id = new string('a', 101) }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            workflow.CreateAsync(new WorkflowInstanceCreateOptions { Id = "-invalid" }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            workflow.GetAsync("invalid id"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            workflow.CreateBatchAsync(tooMany));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            workflow.CreateBatchAsync([new WorkflowInstanceCreateOptions { Params = new { ok = true } }]));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task SendEmailDispatchesStructuredAndRawMessages()
    {
        var dispatcher = new CapturingDispatcher(
            """{"messageId":"structured-1"}""",
            """{"messageId":"raw-1"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-email");
        var message = SendEmailMessage.Create(
                EmailAddress.Create("noreply@example.com", "Worker"),
                ["ada@example.com", "grace@example.com"],
                "Report")
            .ReplyTo("support@example.com")
            .Cc("ops@example.com")
            .Header("x-worker", "dotnet")
            .Text("plain")
            .Html("<p>html</p>")
            .Attachment(EmailAttachment.Bytes("report.bin", "application/octet-stream", [1, 2, 3]))
            .Build();

        var structured = await environment.SendEmail("EMAIL").SendAsync(message);
        var raw = await environment.SendEmail("EMAIL").SendRawAsync(
            "noreply@example.com",
            "ada@example.com",
            "Subject: Raw\r\n\r\nBody");

        Assert.Equal("structured-1", structured.MessageId);
        Assert.Equal("raw-1", raw.MessageId);
        Assert.Equal(["email.send", "email.sendRaw"], dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("EMAIL", call.BindingName));

        using var structuredPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        var root = structuredPayload.RootElement.GetProperty("message");
        Assert.Equal("Worker", root.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("noreply@example.com", root.GetProperty("from").GetProperty("email").GetString());
        Assert.Equal(2, root.GetProperty("to").GetArrayLength());
        Assert.Equal("Report", root.GetProperty("subject").GetString());
        Assert.Equal("dotnet", root.GetProperty("headers").GetProperty("x-worker").GetString());
        Assert.Equal("<p>html</p>", root.GetProperty("html").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), root.GetProperty("attachments")[0].GetProperty("bodyBase64").GetString());

        using var rawPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("Subject: Raw\r\n\r\nBody", rawPayload.RootElement.GetProperty("raw").GetString());
    }

    [Fact]
    public async Task WebSocketProxyDispatchesPairAndSocketOperations()
    {
        var dispatcher = new CapturingDispatcher("""{"client":"ws:1","server":"ws:2"}""", "{}", "{}", "{}", "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-8");

        var pair = await environment.WebSocketPairAsync();
        await pair.Server.AcceptAsync();
        await pair.Server.SendTextAsync("hello");
        await pair.Server.SendBytesAsync(new byte[] { 1, 2, 3 });
        await pair.Server.CloseAsync(1000, "done");
        var envelope = ResponseEnvelope.FromResponse(Response.FromWebSocket(pair.Client));

        Assert.Equal(101, envelope.Status);
        Assert.Equal("ws:1", envelope.WebSocketHandle);
        Assert.Null(envelope.BodyBase64);
        Assert.Equal(
            ["websocket.createPair", "websocket.accept", "websocket.sendText", "websocket.sendBytes", "websocket.close"],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, call => Assert.Equal("$websocket", call.BindingName));

        using var sendPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("ws:2", sendPayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal("hello", sendPayload.RootElement.GetProperty("message").GetString());

        using var binaryPayload = JsonDocument.Parse(dispatcher.Invocations[3].PayloadJson);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), binaryPayload.RootElement.GetProperty("bodyBase64").GetString());
    }
}
