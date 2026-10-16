namespace Workers.Compiler.Tests;

public sealed class RuntimeUpdateTests
{
    [Fact]
    public void ReadsQueueBacklogMetricsFromSendResponsesAndBatches()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record Job(string Path);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var sent = await env.Queue("JOBS").SendAsync(new Job("/a"));
                    var batch = await env.Queue("JOBS").SendBatchAsync([new MessageSendRequest<Job> { Body = new Job("/b") }]);
                    var count = sent.Metadata.Metrics.BacklogCount;
                    var bytes = batch.Metadata.Metrics.BacklogBytes;
                    return new Response($"{count} {bytes}");
                }

                [Queue]
                public static void Consume(MessageBatch<Job> batch, Env env, ExecutionContext ctx)
                {
                    var oldest = batch.Metadata.Metrics.OldestMessageTimestamp;
                    if (oldest is not null) Log.Info(oldest.ToIsoString());
                }
            }
            """);

        Assert.Contains("$workers$notNull($workers$notNull($workers$notNull(sent).metadata).metrics).backlogCount", module);
        Assert.Contains("$workers$notNull($workers$notNull($workers$notNull(batch).metadata).metrics).backlogBytes", module);
        Assert.Contains("$workers$notNull($workers$notNull($workers$notNull(batch).metadata).metrics).oldestMessageTimestamp", module);
    }

    [Fact]
    public void LowersWorkflowBatchesDeletionAndSubscriptions()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var flow = env.Workflow("FLOW");
                    var created = await flow.CreateBatchAsync(new WorkflowBatchCreateOptions
                    {
                        Count = 3,
                        Params = new { source = "api" },
                        Retention = new WorkflowRetention { SuccessRetention = "1 day" },
                        LocationHint = "weur"
                    });
                    var listed = await flow.CreateBatchAsync(new WorkflowBatchCreateOptions
                    {
                        Instances = [new WorkflowInstanceCreateOptions { Id = "a" }]
                    });
                    var deleted = await flow.DeleteBatchAsync(["a", "b"]);
                    var instance = created.Created[0];
                    await instance.SendEventAsync(new WorkflowInstanceSendEventOptions { Type = "approved", Payload = true });
                    using var events = await instance.SubscribeAsync(new WorkflowInstanceSubscribeOptions
                    {
                        Cursor = 4,
                        Filter = ["step_completed", "workflow_completed"]
                    });
                    var next = await events.NextAsync();
                    var at = next?.Timestamp;
                    await instance.DeleteAsync();
                    return new Response($"{listed.Errors.Count} {deleted.Deleted.Count} {next?.StepName}");
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).createBatch($workers$arg1))(flow, { count: 3, params: { source: \"api\" }, retention: { successRetention: \"1 day\" }, locationHint: \"weur\" })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).createBatch($workers$arg1$2))(flow, { instances: [{ id: \"a\" }] })", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3) => $workers$notNull($workers$receiver$3).deleteBatch($workers$arg1$3))(flow, [\"a\", \"b\"])", module);
        Assert.Contains("(($workers$receiver$4, $workers$arg1$4) => $workers$notNull($workers$receiver$4).sendEvent($workers$arg1$4))(instance, { type: \"approved\", payload: true })", module);
        Assert.Contains("(($workers$receiver$5, $workers$arg1$5) => $workers$notNull($workers$receiver$5).subscribe($workers$arg1$5))(instance, { cursor: 4, filter: [\"step_completed\", \"workflow_completed\"] })", module);
        Assert.Contains("$workers$notNull(events).next().then(item => item.done ? null : item.value)", module);
        Assert.Contains("[Symbol.dispose]()", module);
        Assert.Contains("new Date(value)", module);
        Assert.Contains("$workers$notNull(instance).delete()", module);
    }

    [Fact]
    public void LowersContainerSnapshotsInstanceTypesAndPipedExec()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Sandbox : DurableObject
            {
                public async Task<string> Run(ReadableStream input)
                {
                    var container = Ctx.Container!;
                    container.Start(new ContainerStartupOptions
                    {
                        Image = container.Images["default"],
                        Instance = "standard-1"
                    });
                    container.Start(new ContainerStartupOptions
                    {
                        Instance = new ContainerStartResources { Vcpu = 1, MemoryMib = 512, DiskMb = 2000 },
                        ContainerSnapshot = new ContainerSnapshotRestoreParams { Id = "snap" }
                    });
                    var snapshot = await container.SnapshotContainerAsync(new ContainerSnapshotOptions { Name = "warm" });
                    var info = await container.InspectAsync();
                    var piped = await container.ExecAsync(["cat"], new ContainerExecOptions
                    {
                        Stdin = ContainerStdio.Pipe,
                        Stderr = ContainerStdio.Combined,
                        Pty = new ContainerExecPtyOptions { Cols = 120 }
                    });
                    await piped.Stdin!.CloseAsync();
                    piped.Resize(100, 40);
                    var streamed = await container.ExecAsync(["sh"], new ContainerExecOptions { Stdin = input, Pty = true });
                    return snapshot.Id + info?.Image + piped.IsPty;
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1) => $workers$notNull($workers$receiver).start($workers$arg1))(container, { image: $workers$dictionaryIndex($workers$notNull(container).images, \"default\"), instance: \"standard-1\" })", module);
        Assert.Contains("instance: { vcpu: 1, memoryMib: 512, diskMb: 2000 }, containerSnapshot: { id: \"snap\" }", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3) => $workers$notNull($workers$receiver$3).snapshotContainer($workers$arg1$3))(container, { name: \"warm\" })", module);
        Assert.Contains("$workers$notNull(container).inspect()", module);
        Assert.Contains("{ stdin: \"pipe\", stderr: \"combined\", pty: { cols: 120 } }", module);
        Assert.Contains("$workers$notNull($workers$notNull(piped).stdin).close()", module);
        Assert.Contains("$workers$notNull(piped).resize(100, 40)", module);
        Assert.Contains("{ stdin: input, pty: true }", module);
        Assert.Contains("$workers$notNull(piped).isPty", module);
    }

    [Fact]
    public void LowersEmailAddressesAttachmentsAndBuiltReplies()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Email]
                public static async Task Receive(ForwardableEmailMessage message, Env env, ExecutionContext ctx)
                {
                    byte[] logo = [1, 2, 3];
                    await env.SendEmail("MAIL").SendAsync(new EmailMessageBuilder
                    {
                        From = new EmailAddress { Name = "Support", Email = "support@example.com" },
                        To = ["a@example.com", new EmailAddress { Name = "B", Email = "b@example.com" }],
                        Subject = "Hello",
                        Html = "<img src=\"cid:logo\">",
                        Attachments =
                        [
                            new EmailAttachment
                            {
                                Disposition = EmailAttachmentDisposition.Inline, ContentId = "logo",
                                Filename = "logo.png", Type = "image/png", Content = logo
                            },
                            new EmailAttachment
                            {
                                Disposition = EmailAttachmentDisposition.Attachment,
                                Filename = "notes.txt", Type = "text/plain", Content = "notes"
                            }
                        ]
                    });
                    await message.ReplyAsync(new EmailReplyMessageBuilder
                    {
                        From = "support@example.com", Subject = "Re: ticket", Text = "Received"
                    });
                }
            }
            """);

        Assert.Contains("from: { name: \"Support\", email: \"support@example.com\" }", module);
        Assert.Contains("to: [\"a@example.com\", { name: \"B\", email: \"b@example.com\" }]", module);
        Assert.Contains("{ disposition: \"inline\", contentId: \"logo\", filename: \"logo.png\", type: \"image/png\", content: logo }", module);
        Assert.Contains("{ disposition: \"attachment\", filename: \"notes.txt\", type: \"text/plain\", content: \"notes\" }", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).reply($workers$arg1$2))(message, { from: \"support@example.com\", subject: \"Re: ticket\", text: \"Received\" })", module);
    }

    [Fact]
    public void LowersImagesTextEncodingAndResponseHeaders()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var images = env.Images("IMAGES");
                    var label = images.Text("Sale", new ImageTextOptions
                    {
                        Font = new ImageTextFont { Url = "https://example.com/font.ttf" }, Size = 48, Color = "white"
                    });
                    var result = await images.Input(request.Body!, new ImageInputOptions { Encoding = ImageEncoding.Base64 })
                        .Draw(label, new { composite = "over" })
                        .OutputAsync(new ImageOutputOptions { Format = "image/webp", Anim = false });
                    var encoded = result.Image(new ImageTransformationOutputOptions { Encoding = ImageEncoding.Base64 });
                    var headers = new Headers();
                    headers.Set("cache-control", "public, max-age=60");
                    return result.Response(new ImageTransformationResponseOptions { Headers = headers });
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver).text($workers$arg1, $workers$arg2))(images, \"Sale\", { font: { url: \"https://example.com/font.ttf\" }, size: 48, color: \"white\" })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).output($workers$arg1$2))((($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).draw($workers$arg1$2, $workers$arg2$2))((($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).input($workers$arg1$2, $workers$arg2$2))(images, $workers$notNull(request).body, { encoding: \"base64\" }), label, { composite: \"over\" }), { format: \"image/webp\", anim: false })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).output($workers$arg1$2))((($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).draw($workers$arg1$2, $workers$arg2$2))((($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).input($workers$arg1$2, $workers$arg2$2))(images, $workers$notNull(request).body, { encoding: \"base64\" }), label, { composite: \"over\" }), { format: \"image/webp\", anim: false })", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3) => $workers$notNull($workers$receiver$3).image($workers$arg1$3))(result, { encoding: \"base64\" })", module);
        Assert.Contains("(($workers$receiver$4, $workers$arg1$4) => $workers$notNull($workers$receiver$4).response($workers$arg1$4))(result, { headers: headers })", module);
    }

    [Fact]
    public void LowersAiGatewayOptionsAndWebSearch()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed record Answer(string Response);
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var ai = env.Ai("AI");
                    var answer = await ai.RunAsync<object, Answer>("@cf/meta/llama", new { prompt = "hi" }, new AiOptions
                    {
                        Gateway = new GatewayOptions
                        {
                            Id = "main", SkipCache = true, CacheTtl = 60,
                            Retries = new GatewayRetries { MaxAttempts = 3, Backoff = "exponential" }
                        },
                        Tags = ["chat"]
                    });
                    return await ai.WebSearchAsync(new AiWebSearchRequest { GatewayId = "main", Query = answer!.Response, Limit = 5 });
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2, $workers$arg3) => $workers$notNull($workers$receiver).run($workers$arg1, $workers$arg2, $workers$arg3))(ai, \"@cf/meta/llama\", { prompt: \"hi\" }, { gateway: { id: \"main\", skipCache: true, cacheTtl: 60, retries: { maxAttempts: 3, backoff: \"exponential\" } }, tags: [\"chat\"] })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2) => $workers$notNull($workers$receiver$2).websearch($workers$arg1$2))(ai, { gatewayId: \"main\", query: $workers$notNull(answer).response, limit: 5 })", module);
    }

    [Fact]
    public void LowersCacheControlAndVaryRequestProperties()
    {
        var module = Compile("""
            using Workers;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static Task<Response> Fetch(Request request, Env env, ExecutionContext ctx) =>
                    Http.FetchAsync(request, new RequestInit
                    {
                        Cf = new RequestInitCfProperties
                        {
                            CacheControl = "public, max-age=3600",
                            StripEtags = true,
                            OriginRangeRequests = "off",
                            Vary = new RequestInitCfPropertiesVary
                            {
                                Default = new RequestInitCfPropertiesVaryHeader { Action = RequestInitCfPropertiesVaryAction.Bypass },
                                Headers = new Dictionary<string, RequestInitCfPropertiesVaryHeader>
                                {
                                    ["accept"] = new RequestInitCfPropertiesVaryHeader
                                    {
                                        Action = RequestInitCfPropertiesVaryAction.Normalize,
                                        MediaTypes = ["image/avif", "image/webp"]
                                    }
                                }
                            }
                        }
                    });
            }
            """);

        Assert.Contains("cacheControl: \"public, max-age=3600\", stripEtags: true, originRangeRequests: \"off\"", module);
        Assert.Contains("default: { action: \"bypass\" }", module);
        Assert.Contains("{ action: \"normalize\", media_types: [\"image/avif\", \"image/webp\"] }", module);
    }

    [Fact]
    public void ReadsTraceItemVersionsAndLogErrorDetails()
    {
        var module = Compile("""
            using Workers;
            using System.Collections.Generic;
            public static class Worker
            {
                [Tail]
                public static void Tail(IReadOnlyList<TraceItem> events, Env env, ExecutionContext ctx)
                {
                    foreach (var item in events)
                    {
                        Log.Info($"{item.ScriptVersion?.Id} {item.ExecutionModel} {item.DurableObjectId} {item.DispatchNamespace}");
                        foreach (var log in item.Logs)
                            if (log.ErrorInfo is { } errors && errors[0] is { } error)
                                Log.Error(error.Name + ": " + error.Message);
                    }
                }
            }
            """);

        Assert.Contains("(($workers$receiver) => $workers$receiver == null ? null : ($workers$notNull($workers$receiver).id ?? null))($workers$notNull(item).scriptVersion)", module);
        Assert.Contains("$workers$notNull(item).executionModel", module);
        Assert.Contains("$workers$notNull(item).durableObjectId", module);
        Assert.Contains("$workers$notNull(item).dispatchNamespace", module);
        Assert.Contains("(($workers$subject) => (($workers$subject != null) && ((errors = $workers$subject), true)))($workers$notNull(log).errorInfo)", module);
    }

}
