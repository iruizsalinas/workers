using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed class EnvelopeTests
{
    [Fact]
    public void RequestEnvelopeRoundTripsRequest()
    {
        var headers = new Headers()
            .Append("x-test", "a")
            .Append("X-Test", "b");

        var request = Request.Post(
            "https://example.com/path?q=1",
            Body.Text("hello"),
            headers);

        var roundTripped = RequestEnvelope.FromRequest(request).ToRequest();

        Assert.Equal("POST", roundTripped.Method);
        Assert.Equal("/path", roundTripped.Path);
        Assert.Equal("hello", roundTripped.Text());
        Assert.Equal(["a", "b"], roundTripped.Headers.GetAll("x-test"));
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    public void RequestEnvelopeRejectsBodiesForGetAndHead(string method)
    {
        var envelope = new RequestEnvelope(
            "https://example.com/path",
            method,
            [],
            Convert.ToBase64String("body"u8));

        Assert.Throws<ArgumentException>(() => envelope.ToRequest());
    }

    [Theory]
    [InlineData("CONNECT")]
    [InlineData("TRACE")]
    [InlineData("TRACK")]
    [InlineData("bad method")]
    [InlineData("A@B")]
    public void RequestEnvelopeRejectsUnsupportedPlatformMethods(string method)
    {
        var request = Request.Create("https://example.com/path", method);

        Assert.Throws<ArgumentException>(() => RequestEnvelope.FromRequest(request));
    }

    [Theory]
    [InlineData("M-SEARCH")]
    [InlineData("PROPFIND")]
    [InlineData("A_B")]
    [InlineData("A+B")]
    public void RequestEnvelopeAllowsSupportedCustomPlatformMethods(string method)
    {
        var request = Request.Create("https://example.com/path", method);
        var envelope = RequestEnvelope.FromRequest(request);

        Assert.Equal(method, envelope.Method);
    }

    [Fact]
    public void RequestEnvelopeRoundTripsCloudflareMetadata()
    {
        var envelope = JsonSerializer.Deserialize<RequestEnvelope>(
            """
            {
              "url": "https://example.com/",
              "method": "GET",
              "headers": [],
              "bodyBase64": null,
              "cf": {
                "colo": "CDG",
                "country": "FR",
                "asn": 13335
              }
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var request = envelope.ToRequest();
        var cf = request.CfAs<RequestCf>();

        Assert.Equal("CDG", cf.Colo);
        Assert.Equal("FR", cf.Country);
        Assert.Equal(13335, cf.Asn);
    }

    [Fact]
    public void RequestEnvelopePreservesNativeRequestHandleAcrossHeaderAndUrlEdits()
    {
        var envelope = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [new Header("content-type", "text/plain")],
            Convert.ToBase64String("hello"u8),
            nativeRequestHandle: "request:1");

        var request = envelope.ToRequest()
            .WithUrl("https://origin.example/other")
            .WithoutHeader("cookie");
        var roundTripped = RequestEnvelope.FromRequest(request);

        Assert.Equal("request:1", roundTripped.NativeRequestHandle);
        Assert.Equal("https://origin.example/other", roundTripped.Url);
        Assert.Equal("hello", request.Text());
    }

    [Fact]
    public async Task NativeRequestBodyCanBeReadAsynchronously()
    {
        var dispatcher = new CapturingDispatcher("""
            {"value":"native text"}
            """);
        var envelope = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [new Header("content-type", "text/plain")],
            Convert.ToBase64String("clone text"u8),
            nativeRequestHandle: "request:1");

        var request = envelope.ToRequest("invocation:1", dispatcher)
            .WithUrl("https://origin.example/path");
        var text = await request.TextAsync();

        Assert.Equal("native text", text);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation:1", invocation.InvocationId);
        Assert.Equal("$request", invocation.BindingName);
        Assert.Equal("native.request.text", invocation.Operation);
        Assert.Contains("\"handle\":\"request:1\"", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NativeRequestBodyWithoutMaterializedCloneRejectsSynchronousReads()
    {
        var request = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [new Header("content-type", "application/json")],
            bodyBase64: null,
            nativeRequestHandle: "request:1").ToRequest("invocation:1", new CapturingDispatcher("{}"));

        Assert.Throws<WorkersException>(() => request.Text());
        Assert.Throws<WorkersException>(() => request.Bytes());
        Assert.Throws<WorkersException>(() => request.Json<JsonElement>());
        Assert.Throws<WorkersException>(() => request.FormData());
    }

    [Fact]
    public async Task NativeRequestBodyCanBeReadAsStream()
    {
        var dispatcher = new CapturingDispatcher(
            """{"done":false,"bodyBase64":"aGVs"}""",
            """{"done":false,"bodyBase64":"bG8="}""",
            """{"done":true,"bodyBase64":null}""");
        var envelope = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [new Header("content-type", "text/plain")],
            bodyBase64: null,
            nativeRequestHandle: "request:1");

        var stream = envelope.ToRequest("invocation:1", dispatcher).BodyStream();
        var bytes = await stream.ReadAllBytesAsync();

        Assert.Equal("hello", System.Text.Encoding.UTF8.GetString(bytes.Span));
        Assert.Equal(["stream.read", "stream.read", "stream.read"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        var payloads = dispatcher.Invocations.Select(static invocation => invocation.PayloadJson).ToArray();
        Assert.All(dispatcher.Invocations, invocation =>
        {
            Assert.Equal("$stream", invocation.BindingName);
            Assert.Contains("\"source\":\"request\"", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"handle\":\"request:1#stream:", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Single(payloads.Select(ExtractHandle).Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void ResponseEnvelopeRoundTripsBinaryResponse()
    {
        var response = Response.Bytes([1, 2, 3], 201, statusText: "Created")
            .WithHeader("x-first", "one");
        response.Headers.Append("x-first", "two");

        var roundTripped = ResponseEnvelope.FromResponse(response).ToResponse();

        Assert.Equal(201, roundTripped.Status);
        Assert.Equal("Created", roundTripped.StatusText);
        Assert.Equal([1, 2, 3], roundTripped.Body.Bytes.ToArray());
        Assert.Equal(["one", "two"], roundTripped.Headers.GetAll("x-first"));
        Assert.Equal(["application/octet-stream"], roundTripped.Headers.GetAll("content-type"));
    }

    [Fact]
    public void ResponseEnvelopePreservesPlatformContentType()
    {
        var envelope = new ResponseEnvelope(
            200,
            [new Header("content-type", "image/webp")],
            Convert.ToBase64String([9, 8]));

        var response = envelope.ToResponse();

        Assert.Equal([9, 8], response.Body.Bytes.ToArray());
        Assert.Equal("image/webp", response.Headers.Get("content-type"));
        Assert.Equal(["image/webp"], response.Headers.GetAll("content-type"));
    }

    [Fact]
    public void ResponseEnvelopeRoundTripsCloudflareMetadata()
    {
        var response = Response.Text("cached")
            .WithCf(new { cacheTtl = 120, cacheEverything = true });

        var roundTripped = ResponseEnvelope.FromResponse(response).ToResponse();
        var cf = roundTripped.CfAs<ResponseCf>();

        Assert.Equal("cached", roundTripped.Body.AsText());
        Assert.Equal(120, cf.CacheTtl);
        Assert.True(cf.CacheEverything);
    }

    [Fact]
    public void ResponseEnvelopeRoundTripsManualBodyEncoding()
    {
        var response = Response.Bytes([31, 139], contentType: "application/gzip")
            .WithEncodeBody(ResponseEncodeBody.Manual);

        var envelope = ResponseEnvelope.FromResponse(response);
        var roundTripped = envelope.ToResponse();

        Assert.Equal("manual", envelope.EncodeBody);
        Assert.Equal(ResponseEncodeBody.Manual, roundTripped.EncodeBody);
        Assert.Equal([31, 139], roundTripped.Body.Bytes.ToArray());
    }

    [Theory]
    [InlineData(204)]
    [InlineData(205)]
    [InlineData(304)]
    public void ResponseEnvelopeRejectsBodiesForNullBodyStatusCodes(int status)
    {
        Assert.Throws<ArgumentException>(() => new ResponseEnvelope(
            status,
            [],
            Convert.ToBase64String("body"u8)));

        Assert.Throws<ArgumentException>(() => new ResponseEnvelope(
            status,
            [],
            bodyBase64: null,
            nativeBodyStreamSource: "response",
            nativeBodyStreamHandle: "response:1"));

        Assert.Throws<ArgumentException>(() => new ResponseEnvelope(
            status,
            [],
            bodyBase64: null,
            managedBodyStreamHandle: "stream:1"));
    }

    [Fact]
    public void ResponseEnvelopePreservesNativeResponseHandle()
    {
        var envelope = new ResponseEnvelope(
            200,
            [new Header("content-type", "text/css")],
            bodyBase64: null,
            nativeResponseHandle: "response:1");

        var response = envelope.ToResponse().WithoutHeader("set-cookie");
        var roundTripped = ResponseEnvelope.FromResponse(response);

        Assert.Equal("response:1", roundTripped.NativeResponseHandle);
        Assert.Null(roundTripped.BodyBase64);
        Assert.Equal("text/css", roundTripped.Headers.Single().Value);
        Assert.Throws<WorkersException>(() => response.Text());
    }

    [Fact]
    public async Task NativeResponseBodyCanBeReadAsynchronously()
    {
        var dispatcher = new CapturingDispatcher("""
            {"bodyBase64":"AQID"}
            """);
        var envelope = new ResponseEnvelope(
            200,
            [new Header("content-type", "application/octet-stream")],
            bodyBase64: null,
            nativeResponseHandle: "response:1");

        var response = envelope.ToResponse("invocation:1", dispatcher)
            .WithHeader("x-test", "ok");
        var bytes = await response.BytesAsync();

        Assert.Equal([1, 2, 3], bytes.ToArray());
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("invocation:1", invocation.InvocationId);
        Assert.Equal("$response", invocation.BindingName);
        Assert.Equal("native.response.bytes", invocation.Operation);
        Assert.Contains("\"handle\":\"response:1\"", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NativeResponseBodyCanBeReadAsStreamAndCancelled()
    {
        var dispatcher = new CapturingDispatcher(
            """{"done":false,"bodyBase64":"AQID"}""",
            "{}");
        var envelope = new ResponseEnvelope(
            200,
            [new Header("content-type", "application/octet-stream")],
            bodyBase64: null,
            nativeResponseHandle: "response:1");

        var stream = envelope.ToResponse("invocation:1", dispatcher).BodyStream();
        var read = await stream.ReadAsync();
        await stream.CancelAsync();

        Assert.False(read.Done);
        Assert.Equal([1, 2, 3], read.Bytes.ToArray());
        Assert.Equal(["stream.read", "stream.cancel"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        var payloads = dispatcher.Invocations.Select(static invocation => invocation.PayloadJson).ToArray();
        Assert.All(dispatcher.Invocations, invocation =>
        {
            Assert.Equal("$stream", invocation.BindingName);
            Assert.Contains("\"source\":\"response\"", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"handle\":\"response:1#stream:", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Single(payloads.Select(ExtractHandle).Distinct(StringComparer.Ordinal));
    }

    [Fact]
    public void ResponseEnvelopePreservesNativeRequestStreamBody()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var request = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [],
            bodyBase64: null,
            nativeRequestHandle: "request:1").ToRequest("invocation:1", dispatcher);

        var response = Response.FromStream(request.BodyStream(), headers: new Headers().Set("content-type", "text/plain"));
        var envelope = ResponseEnvelope.FromResponse(response);
        var roundTripped = envelope.ToResponse("invocation:1", dispatcher);

        Assert.Null(envelope.BodyBase64);
        Assert.Equal("request", envelope.NativeBodyStreamSource);
        Assert.StartsWith("request:1#stream:", envelope.NativeBodyStreamHandle, StringComparison.Ordinal);
        Assert.Equal("text/plain", roundTripped.Headers.Get("content-type"));
        Assert.Throws<WorkersException>(() => roundTripped.Text());
    }

    [Fact]
    public void NativeRequestBodyStreamsUseIndependentHandles()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var request = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [],
            bodyBase64: null,
            nativeRequestHandle: "request:1").ToRequest("invocation:1", dispatcher);

        var first = request.BodyStream();
        var second = request.Clone().BodyStream();

        Assert.StartsWith("request:1#stream:", first.Handle, StringComparison.Ordinal);
        Assert.StartsWith("request:1#stream:", second.Handle, StringComparison.Ordinal);
        Assert.NotEqual(first.Handle, second.Handle);
    }

    [Fact]
    public void NativeResponseBodyStreamsUseIndependentHandles()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var response = new ResponseEnvelope(
            200,
            [],
            bodyBase64: null,
            nativeResponseHandle: "response:1").ToResponse("invocation:1", dispatcher);

        var first = response.BodyStream();
        var second = response.Clone().BodyStream();

        Assert.StartsWith("response:1#stream:", first.Handle, StringComparison.Ordinal);
        Assert.StartsWith("response:1#stream:", second.Handle, StringComparison.Ordinal);
        Assert.NotEqual(first.Handle, second.Handle);
    }

    [Fact]
    public void NativeResponseCloneSerializesAsIndependentBodyStream()
    {
        var dispatcher = new CapturingDispatcher("{}");
        var response = new ResponseEnvelope(
            200,
            [new Header("content-type", "text/plain")],
            bodyBase64: null,
            nativeResponseHandle: "response:1").ToResponse("invocation:1", dispatcher);

        var envelope = ResponseEnvelope.FromResponse(response.Clone());

        Assert.Null(envelope.NativeResponseHandle);
        Assert.Equal("response", envelope.NativeBodyStreamSource);
        Assert.StartsWith("response:1#stream:", envelope.NativeBodyStreamHandle, StringComparison.Ordinal);
        Assert.Null(envelope.BodyBase64);
        Assert.Equal("text/plain", envelope.Headers.Single().Value);
    }

    [Fact]
    public async Task NativeResponseCloneCanBeReadAsTextAsync()
    {
        var dispatcher = new CapturingDispatcher(
            """{"done":false,"bodyBase64":"Y2xvbmVk"}""",
            """{"done":true,"bodyBase64":null}""");
        var response = new ResponseEnvelope(
            200,
            [new Header("content-type", "text/plain")],
            bodyBase64: null,
            nativeResponseHandle: "response:1").ToResponse("invocation:1", dispatcher);

        var text = await response.Clone().TextAsync();

        Assert.Equal("cloned", text);
        Assert.Equal(["stream.read", "stream.read"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.All(dispatcher.Invocations, invocation =>
        {
            Assert.Equal("$stream", invocation.BindingName);
            Assert.Contains("\"source\":\"response\"", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"handle\":\"response:1#stream:", invocation.PayloadJson, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task StreamBackedResponseCanBeReadAsBytesAsync()
    {
        var response = Response.FromStream(ReadableStream.FromAsyncEnumerable(ManagedChunks()));

        var bytes = await response.BytesAsync();

        Assert.Equal("managed stream", System.Text.Encoding.UTF8.GetString(bytes.Span));
    }

    [Fact]
    public async Task ResponseEnvelopeBridgesAlreadyReadNativeRequestStream()
    {
        var dispatcher = new CapturingDispatcher(
            """{"done":false,"bodyBase64":"Zmlyc3Q="}""",
            """{"done":false,"bodyBase64":"c2Vjb25k"}""",
            """{"done":true,"bodyBase64":null}""");
        var request = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [],
            bodyBase64: null,
            nativeRequestHandle: "request:1").ToRequest("invocation:1", dispatcher);
        var stream = request.BodyStream();

        var first = await stream.ReadAsync();
        var envelope = ResponseEnvelope.FromResponse(Response.FromStream(stream));
        var roundTripped = envelope.ToResponse();
        var remaining = await roundTripped.BodyStream().ReadAllBytesAsync();

        Assert.Equal("first", System.Text.Encoding.UTF8.GetString(first.Bytes.Span));
        Assert.Null(envelope.NativeBodyStreamSource);
        Assert.Null(envelope.NativeBodyStreamHandle);
        Assert.NotNull(envelope.ManagedBodyStreamHandle);
        Assert.Equal("second", System.Text.Encoding.UTF8.GetString(remaining.Span));
    }

    [Fact]
    public async Task NativeStreamReadDoesNotRestartAfterDone()
    {
        var dispatcher = new CapturingDispatcher("""{"done":true,"bodyBase64":null}""");
        var request = new RequestEnvelope(
            "https://example.com/path",
            "POST",
            [],
            bodyBase64: null,
            nativeRequestHandle: "request:1").ToRequest("invocation:1", dispatcher);
        var stream = request.BodyStream();

        var first = await stream.ReadAsync();
        var second = await stream.ReadAsync();

        Assert.True(first.Done);
        Assert.True(second.Done);
        Assert.Single(dispatcher.Invocations);
    }

    [Fact]
    public async Task ResponseEnvelopePreservesManagedStreamBody()
    {
        var response = Response.FromStream(
            ReadableStream.FromAsyncEnumerable(ManagedChunks()),
            headers: new Headers().Set("content-type", "text/plain"));

        var envelope = ResponseEnvelope.FromResponse(response);
        var roundTripped = envelope.ToResponse();
        var bytes = await roundTripped.BodyStream().ReadAllBytesAsync();

        Assert.Null(envelope.BodyBase64);
        Assert.Null(envelope.NativeBodyStreamSource);
        Assert.Null(envelope.NativeBodyStreamHandle);
        Assert.NotNull(envelope.ManagedBodyStreamHandle);
        Assert.Equal("text/plain", roundTripped.Headers.Get("content-type"));
        Assert.Equal("managed stream", System.Text.Encoding.UTF8.GetString(bytes.Span));
    }

    [Fact]
    public void ResponseEnvelopeOmitsAutomaticBodyEncoding()
    {
        var envelope = ResponseEnvelope.FromResponse(Response.Text("auto"));

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("encodeBody", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ResponseEnvelopeSerializesStatusTextWhenPresent()
    {
        var envelope = ResponseEnvelope.FromResponse(Response.Empty(204, "No Content"));

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"statusText\":\"No Content\"", json, StringComparison.Ordinal);
        Assert.Equal("No Content", envelope.ToResponse().StatusText);
    }

    [Fact]
    public void WebSocketResponseEnvelopeCannotRoundTripToManagedResponse()
    {
        var envelope = new ResponseEnvelope(101, [], bodyBase64: null, webSocketHandle: "ws:1");

        Assert.Equal("ws:1", envelope.WebSocketHandle);
        Assert.Throws<WorkersException>(() => envelope.ToResponse());
    }

    private sealed class RequestCf
    {
        public required string Colo { get; init; }

        public required string Country { get; init; }

        public int Asn { get; init; }
    }

    private sealed class ResponseCf
    {
        public int CacheTtl { get; init; }

        public bool CacheEverything { get; init; }
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> ManagedChunks()
    {
        await Task.Yield();
        yield return "managed "u8.ToArray();
        yield return "stream"u8.ToArray();
    }

    private static string ExtractHandle(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.GetProperty("handle").GetString()
            ?? throw new InvalidOperationException("Stream payload did not contain a handle.");
    }

    private sealed class CapturingDispatcher : IBindingDispatcher
    {
        private readonly Queue<string> _results;

        public CapturingDispatcher(params string[] results)
        {
            _results = new Queue<string>(results);
        }

        public List<BindingInvocation> Invocations { get; } = [];

        public Task<string> DispatchAsync(BindingInvocation invocation, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Invocations.Add(invocation);
            return Task.FromResult(_results.Count == 0 ? "{}" : _results.Dequeue());
        }
    }
}
