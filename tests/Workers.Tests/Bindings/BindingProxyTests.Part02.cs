using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task R2ProxyDispatchesBinaryOperations()
    {
        var bodyBase64 = Convert.ToBase64String([1, 2, 3]);
        var md5 = Convert.ToBase64String([10, 11, 12]);
        var sha256 = Convert.ToBase64String([20, 21, 22]);
        var dispatcher = new CapturingDispatcher(
            $$"""
            {
              "key": "file.bin",
              "version": "v1",
              "size": 3,
              "etag": "etag",
              "httpEtag": "\"etag\"",
              "uploaded": "2026-06-29T17:30:00Z",
              "httpMetadata": {
                "contentType": "application/test",
                "cacheControl": "max-age=60",
                "cacheExpiry": "2026-06-30T17:30:00Z"
              },
              "customMetadata": {
                "owner": "tests"
              },
              "checksums": {
                "md5": "{{md5}}"
              },
              "range": {
                "offset": 0,
                "length": 3,
                "suffix": null
              }
            }
            """,
            $$"""{"bodyBase64":"{{bodyBase64}}","contentType":"application/test"}""",
            $$"""
            {
              "key": "file.bin",
              "version": "v2",
              "size": 2,
              "etag": "etag-v2",
              "httpEtag": "\"etag-v2\"",
              "uploaded": "2026-06-29T17:31:00Z",
              "httpMetadata": {
                "contentType": "image/png",
                "cacheControl": "max-age=120"
              },
              "customMetadata": {
                "owner": "stored"
              },
              "checksums": {
                "sha256": "{{sha256}}"
              },
              "range": null
            }
            """,
            "{}",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-2");

        var metadata = await environment.Bucket("ASSETS").HeadAsync("file.bin");
        var body = await environment.Bucket("ASSETS").GetAsync(
            "file.bin",
            new R2GetOptions
            {
                Range = R2Range.OffsetWithLength(10, 3),
                OnlyIf = new R2Conditional(
                    EtagMatches: "etag",
                    UploadedAfter: DateTimeOffset.Parse("2026-06-29T17:00:00Z"))
            });
        var stored = await environment.Bucket("ASSETS").PutObjectAsync(
            "file.bin",
            Body.FromBytes([4, 5], "application/octet-stream"),
            new R2PutOptions
            {
                HttpMetadata = new R2HttpMetadata(
                    ContentType: "image/png",
                    ContentLanguage: "en",
                    ContentDisposition: null,
                    ContentEncoding: null,
                    CacheControl: "max-age=120",
                    CacheExpiry: DateTimeOffset.Parse("2026-06-30T17:31:00Z")),
                CustomMetadata = new Dictionary<string, string>
                {
                    ["owner"] = "stored"
                },
                Checksums = new R2Checksums(Md5: null, Sha1: null, Sha256: [20, 21, 22], Sha384: null, Sha512: null),
                OnlyIf = new R2Conditional(EtagDoesNotMatch: "stale")
            });
        await environment.Bucket("ASSETS").DeleteAsync("file.bin");
        await environment.Bucket("ASSETS").DeleteAsync(["old-a.bin", "old-b.bin"]);

        Assert.NotNull(metadata);
        Assert.Equal("file.bin", metadata.Key);
        Assert.Equal("v1", metadata.Version);
        Assert.Equal((ulong)3, metadata.Size);
        Assert.Equal("etag", metadata.Etag);
        Assert.Equal("\"etag\"", metadata.HttpEtag);
        Assert.Equal("application/test", metadata.HttpMetadata.ContentType);
        Assert.Equal("max-age=60", metadata.HttpMetadata.CacheControl);
        Assert.Equal("tests", metadata.CustomMetadata["owner"]);
        Assert.Equal([10, 11, 12], metadata.Checksums.Md5);
        Assert.Equal((ulong)0, metadata.Range!.Offset);
        Assert.Equal((ulong)3, metadata.Range.Length);
        Assert.NotNull(body);
        Assert.Equal([1, 2, 3], body.Bytes.ToArray());
        Assert.Equal("application/test", body.ContentType);
        Assert.NotNull(stored);
        Assert.Equal("v2", stored.Version);
        Assert.Equal("image/png", stored.HttpMetadata.ContentType);
        Assert.Equal("stored", stored.CustomMetadata["owner"]);
        Assert.Equal([20, 21, 22], stored.Checksums.Sha256);
        Assert.Equal(["r2.head", "r2.get", "r2.put", "r2.delete", "r2.deleteMany"], dispatcher.Invocations.Select(static call => call.Operation));

        using var getPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("file.bin", getPayload.RootElement.GetProperty("key").GetString());
        Assert.Equal((ulong)10, getPayload.RootElement.GetProperty("options").GetProperty("range").GetProperty("offset").GetUInt64());
        Assert.Equal((ulong)3, getPayload.RootElement.GetProperty("options").GetProperty("range").GetProperty("length").GetUInt64());
        Assert.Equal("etag", getPayload.RootElement.GetProperty("options").GetProperty("onlyIf").GetProperty("etagMatches").GetString());

        using var putPayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("image/png", putPayload.RootElement.GetProperty("options").GetProperty("httpMetadata").GetProperty("contentType").GetString());
        Assert.Equal("en", putPayload.RootElement.GetProperty("options").GetProperty("httpMetadata").GetProperty("contentLanguage").GetString());
        Assert.Equal("stored", putPayload.RootElement.GetProperty("options").GetProperty("customMetadata").GetProperty("owner").GetString());
        Assert.Equal(sha256, putPayload.RootElement.GetProperty("options").GetProperty("checksums").GetProperty("sha256").GetString());
        Assert.Equal("stale", putPayload.RootElement.GetProperty("options").GetProperty("onlyIf").GetProperty("etagDoesNotMatch").GetString());

        using var deleteManyPayload = JsonDocument.Parse(dispatcher.Invocations[4].PayloadJson);
        Assert.Equal("old-a.bin", deleteManyPayload.RootElement.GetProperty("keys")[0].GetString());
        Assert.Equal("old-b.bin", deleteManyPayload.RootElement.GetProperty("keys")[1].GetString());
    }

    [Fact]
    public async Task R2PutObjectReturnsNullWhenConditionFails()
    {
        var dispatcher = new CapturingDispatcher("null");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-r2-put-condition");

        var result = await environment.Bucket("ASSETS").PutObjectAsync(
            "file.bin",
            Body.Text("new"),
            new R2PutOptions
            {
                OnlyIf = new R2Conditional(EtagMatches: "missing")
            });

        Assert.Null(result);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("r2.put", invocation.Operation);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("missing", payload.RootElement.GetProperty("options").GetProperty("onlyIf").GetProperty("etagMatches").GetString());
    }

    [Fact]
    public async Task R2GetReturnsNullWhenObjectIsMissing()
    {
        var dispatcher = new CapturingDispatcher("null");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-r2-get-missing");

        var result = await environment.Bucket("ASSETS").GetAsync("missing.bin");

        Assert.Null(result);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("r2.get", invocation.Operation);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("missing.bin", payload.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public async Task R2MultipartUploadDispatchesLifecycleOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"key":"large.bin","uploadId":"upload-1"}""",
            """{"partNumber":1,"etag":"part-1"}""",
            """
            {
              "key": "large.bin",
              "version": "complete-v1",
              "size": 6,
              "etag": "complete-etag",
              "httpEtag": "\"complete-etag\"",
              "uploaded": "2026-06-29T18:00:00Z",
              "httpMetadata": {
                "contentType": "application/octet-stream"
              },
              "customMetadata": {},
              "checksums": {},
              "range": null
            }
            """,
            "{}",
            """{"partNumber":2,"etag":"part-2"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-r2-multipart");
        var bucket = environment.Bucket("ASSETS");

        var upload = await bucket.CreateMultipartUploadAsync(
            "large.bin",
            new R2MultipartUploadOptions
            {
                HttpMetadata = new R2HttpMetadata(
                    ContentType: "application/octet-stream",
                    ContentLanguage: null,
                    ContentDisposition: null,
                    ContentEncoding: null,
                    CacheControl: null,
                    CacheExpiry: null),
                CustomMetadata = new Dictionary<string, string>
                {
                    ["kind"] = "backup"
                }
            });
        var part = await upload.UploadPartAsync(1, Body.FromBytes([1, 2, 3]));
        var completed = await upload.CompleteAsync([part]);
        await upload.AbortAsync();
        var resumedPart = await bucket.ResumeMultipartUpload("large.bin", "upload-1")
            .UploadPartAsync(2, Body.FromBytes([4, 5, 6]));

        Assert.Equal("large.bin", upload.Key);
        Assert.Equal("upload-1", upload.UploadId);
        Assert.Equal(new R2UploadedPart(1, "part-1"), part);
        Assert.Equal("complete-v1", completed.Version);
        Assert.Equal(new R2UploadedPart(2, "part-2"), resumedPart);
        Assert.Equal(
            ["r2.multipart.create", "r2.multipart.uploadPart", "r2.multipart.complete", "r2.multipart.abort", "r2.multipart.uploadPart"],
            dispatcher.Invocations.Select(static call => call.Operation));

        using var createPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("application/octet-stream", createPayload.RootElement.GetProperty("options").GetProperty("httpMetadata").GetProperty("contentType").GetString());
        Assert.Equal("backup", createPayload.RootElement.GetProperty("options").GetProperty("customMetadata").GetProperty("kind").GetString());

        using var uploadPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("upload-1", uploadPayload.RootElement.GetProperty("uploadId").GetString());
        Assert.Equal(1, uploadPayload.RootElement.GetProperty("partNumber").GetInt32());

        using var completePayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("part-1", completePayload.RootElement.GetProperty("parts")[0].GetProperty("etag").GetString());
    }

    [Fact]
    public async Task R2ProxyDispatchesListOperation()
    {
        var dispatcher = new CapturingDispatcher(
            """
            {
              "objects": [
                {
                  "key": "images/a.png",
                  "version": "v1",
                  "size": 42,
                  "etag": "etag-a",
                  "httpEtag": "\"etag-a\"",
                  "uploaded": "2026-06-29T17:30:00Z",
                  "httpMetadata": {
                    "contentType": "image/png"
                  },
                  "customMetadata": {
                    "kind": "avatar"
                  },
                  "checksums": {},
                  "range": null
                }
              ],
              "truncated": true,
              "cursor": "cursor-2",
              "delimitedPrefixes": ["images/archive/"]
            }
            """);
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-r2-list");

        var result = await environment.Bucket("ASSETS").ListAsync(new R2ListOptions
        {
            Limit = 10,
            Prefix = "images/",
            StartAfter = "images/0.png",
            Cursor = "cursor-1",
            Delimiter = "/",
            IncludeHttpMetadata = true,
            IncludeCustomMetadata = true
        });

        Assert.True(result.Truncated);
        Assert.Equal("cursor-2", result.Cursor);
        Assert.Equal(["images/archive/"], result.DelimitedPrefixes);
        var item = Assert.Single(result.Objects);
        Assert.Equal("images/a.png", item.Key);
        Assert.Equal((ulong)42, item.Size);
        Assert.Equal("image/png", item.HttpMetadata.ContentType);
        Assert.Equal("avatar", item.CustomMetadata["kind"]);

        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("r2.list", invocation.Operation);
        Assert.Equal("ASSETS", invocation.BindingName);
        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal(10, payload.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal("images/", payload.RootElement.GetProperty("prefix").GetString());
        Assert.Equal("images/0.png", payload.RootElement.GetProperty("startAfter").GetString());
        Assert.Equal("cursor-1", payload.RootElement.GetProperty("cursor").GetString());
        Assert.Equal("/", payload.RootElement.GetProperty("delimiter").GetString());
        Assert.True(payload.RootElement.GetProperty("includeHttpMetadata").GetBoolean());
        Assert.True(payload.RootElement.GetProperty("includeCustomMetadata").GetBoolean());
    }

    [Fact]
    public async Task R2ListRejectsInvalidLimit()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-r2-list-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Bucket("ASSETS").ListAsync(new R2ListOptions { Limit = 1001 }));
    }

    [Fact]
    public async Task R2MultiDeleteRejectsInvalidKeyCounts()
    {
        using var _ = BindingDispatcher.Use(new CapturingDispatcher("{}"));
        var environment = EnvironmentWithInvocation("invocation-r2-delete-many-invalid");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Bucket("ASSETS").DeleteAsync([]));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            environment.Bucket("ASSETS").DeleteAsync(Enumerable.Range(0, 1001).Select(static index => $"key-{index}")));
    }

    [Fact]
    public async Task ServiceProxyDispatchesRequestEnvelope()
    {
        var response = ResponseEnvelope.FromResponse(
            Response.Text("ok", 202).WithCf(new { colo = "CDG", cacheStatus = "HIT" }));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-3");

        var result = await environment.Service("API").FetchAsync(Request.Get("https://service.example/test"));
        var cf = result.CfAs<ResponseCf>();

        Assert.Equal(202, result.Status);
        Assert.Equal("ok", result.Body.AsText());
        Assert.Equal("CDG", cf.Colo);
        Assert.Equal("HIT", cf.CacheStatus);
        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("https://service.example/test", payload.RootElement.GetProperty("request").GetProperty("url").GetString());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("options").ValueKind);
    }

    [Fact]
    public async Task ServiceProxyDispatchesUrlFetch()
    {
        var response = ResponseEnvelope.FromResponse(Response.Text("url", 203));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-service-url");

        var result = await environment.Service("API").FetchAsync("https://service.example/url");

        Assert.Equal(203, result.Status);
        Assert.Equal("url", result.Body.AsText());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("service.fetch", invocation.Operation);
        Assert.Equal("API", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("https://service.example/url", payload.RootElement.GetProperty("request").GetProperty("url").GetString());
    }

    [Fact]
    public async Task ServiceProxyDispatchesFetchOptions()
    {
        var response = ResponseEnvelope.FromResponse(Response.Empty(204));
        var dispatcher = new CapturingDispatcher(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-service-options");

        var result = await environment.Service("API").FetchAsync(
            "https://service.example/options",
            new FetchOptions
            {
                Redirect = RequestRedirect.Manual,
                Cache = RequestCache.NoCache
            });

        Assert.Equal(204, result.Status);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("service.fetch", invocation.Operation);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("manual", payload.RootElement.GetProperty("options").GetProperty("redirect").GetString());
        Assert.Equal("no-cache", payload.RootElement.GetProperty("options").GetProperty("cache").GetString());
    }
}
