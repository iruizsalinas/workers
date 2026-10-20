namespace Workers.Compiler.Tests;

public sealed class RuntimeCoverageTests
{
    [Fact]
    public void LowersR2ChecksumsCustomerKeysAndSecondPrecisionConditions()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                private const string Key = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var bucket = env.R2("FILES");
                    var stored = await bucket.PutAsync("a", "body", new R2PutOptions { SsecKey = Key });
                    var md5 = stored!.Checksums.Md5;
                    var keyed = stored.SsecKeyMd5;
                    var read = await bucket.GetAsync("a", new R2GetOptions
                    {
                        SsecKey = Key,
                        OnlyIf = new R2Conditional { UploadedAfter = new Date(0), SecondsGranularity = true }
                    });
                    var upload = await bucket.CreateMultipartUploadAsync("b", new R2MultipartOptions { SsecKey = Key });
                    var part = await upload.UploadPartAsync(1, "part", new R2UploadPartOptions { SsecKey = Key });
                    return new Response(md5 is null ? "none" : Hex.Encode(md5));
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2, $workers$arg3) => $workers$notNull($workers$receiver).put($workers$arg1, $workers$arg2, $workers$arg3))(bucket, \"a\", \"body\", { ssecKey: \"000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f\" })", module);
        Assert.Contains("(value => value == null ? null : new Uint8Array(value))($workers$notNull($workers$notNull(stored).checksums).md5)", module);
        Assert.Contains("let keyed = $workers$notNull(stored).ssecKeyMd5;", module);
        Assert.Contains("onlyIf: { uploadedAfter: new Date(0), secondsGranularity: true }", module);
        Assert.Contains("(($workers$receiver$4, $workers$arg1$4, $workers$arg2$4, $workers$arg3$2) => $workers$notNull($workers$receiver$4).uploadPart($workers$arg1$4, $workers$arg2$4, $workers$arg3$2))(upload, 1, \"part\", { ssecKey: \"000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f\" })", module);
    }

    [Fact]
    public void LowersDurableObjectAlarmOptionsTransactionAlarmsAndRoutingMode()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public sealed class Scheduler : DurableObject
            {
                public async Task<bool> Reschedule()
                {
                    var stub = Env.DurableObject("PEERS").GetByName("main",
                        new DurableObjectNamespaceGetDurableObjectOptions { RoutingMode = DurableObjectRoutingMode.PrimaryOnly });
                    await Ctx.Storage.SetAlarmAsync(new Date(1000), new DurableObjectSetAlarmOptions { AllowUnconfirmed = true });
                    return await Ctx.Storage.TransactionAsync(async transaction =>
                    {
                        var alarm = await transaction.GetAlarmAsync(new DurableObjectGetAlarmOptions { AllowConcurrency = true });
                        if (alarm is not null) await transaction.DeleteAlarmAsync();
                        await transaction.SetAlarmAsync(new Date(2000));
                        return alarm is null;
                    });
                }
            }
            """);

        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver).getByName($workers$arg1, $workers$arg2))($workers$notNull(this.env)[\"PEERS\"], \"main\", { routingMode: \"primary-only\" })", module);
        Assert.Contains("(($workers$receiver$2, $workers$arg1$2, $workers$arg2$2) => $workers$notNull($workers$receiver$2).setAlarm($workers$arg1$2, $workers$arg2$2))($workers$notNull(this.ctx).storage, new Date(1000), { allowUnconfirmed: true })", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3) => $workers$notNull($workers$receiver$3).getAlarm($workers$arg1$3).then(value => value == null ? null : new Date(value)))(transaction, { allowConcurrency: true })", module);
        Assert.Contains("$workers$notNull(transaction).deleteAlarm()", module);
        Assert.Contains("(($workers$receiver$4, $workers$arg1$4) => $workers$notNull($workers$receiver$4).setAlarm($workers$arg1$4))(transaction, new Date(2000))", module);
    }

    [Fact]
    public void LowersRequestBodyEncodingR2RoutingAndIncomingTlsDetails()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var upstream = await Http.FetchAsync("https://example.com", new RequestInit
                    {
                        EncodeResponseBody = ResponseBodyEncoding.Manual,
                        Cf = new RequestInitCfProperties { R2 = new RequestInitCfPropertiesR2 { BucketColoId = 12 } }
                    });
                    var cf = request.Cf;
                    return Response.Json(new
                    {
                        encoding = cf?.ClientAcceptEncoding,
                        keepAlive = cf?.EdgeRequestKeepAliveStatus,
                        handshake = cf?.TlsExportedAuthenticator?.ClientHandshake
                    });
                }
            }
            """);

        Assert.Contains("{ encodeResponseBody: \"manual\", cf: { r2: { bucketColoId: 12 } } }", module);
        Assert.Contains(".clientAcceptEncoding ?? null", module);
        Assert.Contains(".edgeRequestKeepAliveStatus ?? null", module);
        Assert.Contains(".clientHandshake ?? null", module);
        Assert.Contains(".tlsExportedAuthenticator)", module);
    }

    [Fact]
    public void ReadsUnredactedTailRequestsAndTheirCloudflareProperties()
    {
        var module = Compile("""
            using Workers;
            public static class Worker
            {
                [Tail]
                public static void Tail(IReadOnlyList<TraceItem> events)
                {
                    foreach (var item in events)
                    {
                        var request = item.Event?.Request.GetUnredacted();
                        if (request is not null) Log.Info(request.Cf);
                    }
                }
            }
            """);

        Assert.Contains("(($workers$receiver) => $workers$receiver == null ? null : ($workers$notNull($workers$notNull($workers$receiver).request).getUnredacted() ?? null))((value => value != null && \"request\" in value ? value : null)($workers$notNull(item).event))", module);
        Assert.Contains("console.info($workers$notNull(request).cf)", module);
    }

    [Fact]
    public void LowersAiGatewayLogsModelSearchAndMarkdownConversion()
    {
        var module = Compile("""
            using Workers;
            using System.Threading.Tasks;
            public static class Worker
            {
                [Fetch]
                public static async Task<Response> Fetch(Request request, Env env, ExecutionContext ctx)
                {
                    var ai = env.Ai("AI");
                    var gateway = ai.Gateway("main");
                    var logId = ai.AiGatewayLogId;
                    var log = await gateway.GetLogAsync(logId!);
                    await gateway.PatchLogAsync(log.Id, new AiGatewayPatchLog { Feedback = 1, Score = 90 });
                    var url = await gateway.GetUrlAsync("openai");
                    var universal = await gateway.RunAsync(new AIGatewayUniversalRequest
                    {
                        Provider = "workers-ai",
                        Endpoint = "@cf/meta/llama-3.1-8b-instruct",
                        Headers = new Dictionary<string, object> { ["authorization"] = "Bearer token" },
                        Query = new { prompt = "hello" }
                    }, new AiGatewayRunOptions { Gateway = new UniversalGatewayOptions { SkipCache = true } });
                    var models = await ai.ModelsAsync(new AiModelsSearchParams { Task = "Text Generation", PerPage = 5, HideExperimental = true });
                    var converted = await ai.ToMarkdownAsync(new MarkdownDocument { Name = "page.html", Blob = new Blob("hello", "text/html") },
                        new ConversionRequestOptions
                        {
                            ConversionOptions = new ConversionOptions
                            {
                                Output = new ConversionOutputOptions { Format = ConversionOutputFormat.Text },
                                Html = new ConversionHtmlOptions { CssSelector = "main", Images = new EmbeddedImageConversionOptions { ConvertOGImage = true } }
                            }
                        });
                    var formats = await ai.ToMarkdown().SupportedAsync();
                    return Response.Json(new
                    {
                        log.StatusCode, log.TokensIn, created = log.CreatedAt.ToIsoString(),
                        property = models[0].Properties[0].PropertyId,
                        converted.Data, converted.Error, formats.Count, url
                    });
                }
            }
            """);

        Assert.Contains("let gateway = $workers$notNull(ai).gateway(\"main\");", module);
        Assert.Contains("let logId = $workers$notNull(ai).aiGatewayLogId;", module);
        Assert.Contains("(($workers$receiver, $workers$arg1, $workers$arg2) => $workers$notNull($workers$receiver).patchLog($workers$arg1, $workers$arg2))(gateway, $workers$notNull(log).id, { feedback: 1, score: 90 })", module);
        Assert.Contains("$workers$notNull(gateway).getUrl(\"openai\")", module);
        Assert.Contains("{ provider: \"workers-ai\", endpoint: \"@cf/meta/llama-3.1-8b-instruct\"", module);
        Assert.Contains("{ gateway: { skipCache: true } }", module);
        Assert.Contains("(($workers$receiver$3, $workers$arg1$3) => $workers$notNull($workers$receiver$3).models($workers$arg1$3))(ai, { task: \"Text Generation\", per_page: 5, hide_experimental: true })", module);
        Assert.Contains("(($workers$receiver$4, $workers$arg1$4, $workers$arg2$3) => $workers$notNull($workers$receiver$4).toMarkdown($workers$arg1$4, $workers$arg2$3))(ai, { name: \"page.html\", blob: new Blob([\"hello\"], { type: (\"text/html\") ?? undefined }) }, { conversionOptions: { output: { format: \"text\" }, html: { cssSelector: \"main\", images: { convertOGImage: true } } } })", module);
        Assert.Contains("conversionOptions: { output: { format: \"text\" }, html: { cssSelector: \"main\", images: { convertOGImage: true } } }", module);
        Assert.Contains("$workers$notNull($workers$notNull(ai).toMarkdown()).supported()", module);
        Assert.Contains("statusCode: $workers$notNull(log).status_code", module);
        Assert.Contains("tokensIn: $workers$notNull(log).tokens_in", module);
        Assert.Contains("$workers$notNull(new Date($workers$notNull(log).created_at)).toISOString()", module);
        Assert.Contains(".property_id", module);
    }
}
