using System.Text;
using System.Text.Json;
using Workers.Interop;
using Xunit;

namespace Workers.Tests;

public sealed partial class BindingProxyTests
{
    [Fact]
    public async Task HyperdriveDispatchesConnectSocket()
    {
        var dispatcher = new CapturingDispatcher("""{"handle":"tcp:hyperdrive"}""", "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-hyperdrive-connect");

        var socket = await environment.Hyperdrive("DB").ConnectAsync();
        await socket.WriteTextAsync("startup");

        Assert.Equal(["hyperdrive.connect", "socket.write"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.Equal(["DB", "$socket"], dispatcher.Invocations.Select(static invocation => invocation.BindingName));
        Assert.Equal("{}", dispatcher.Invocations[0].PayloadJson);

        using var writePayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal("tcp:hyperdrive", writePayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal(Convert.ToBase64String("startup"u8), writePayload.RootElement.GetProperty("bodyBase64").GetString());
    }

    [Fact]
    public async Task TcpSocketDispatchesConnectAndStreamOperations()
    {
        var dispatcher = new CapturingDispatcher(
            """{"handle":"tcp:1"}""",
            """{"remoteAddress":"203.0.113.10","localAddress":"198.51.100.20"}""",
            "{}",
            $$"""{"done":false,"bodyBase64":"{{Convert.ToBase64String("hello"u8)}}"}""",
            """{"done":true,"bodyBase64":null}""",
            "{}",
            """{"handle":"tcp:2"}""",
            """{"remoteAddress":"203.0.113.10","localAddress":null}""",
            "{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-socket");

        var socket = await environment.ConnectSocketAsync(
            new SocketAddress("db.example", 5432),
            new SocketOptions
            {
                SecureTransport = SocketSecureTransport.StartTls,
                AllowHalfOpen = true
            });
        var opened = await socket.OpenedAsync();
        await socket.WriteTextAsync("ping");
        var chunk = await socket.ReadAsync();
        var completed = await socket.ReadAsync();
        await socket.CloseWritableAsync();
        var secureSocket = await socket.StartTlsAsync();
        var secureOpened = await secureSocket.OpenedAsync();
        await secureSocket.CloseAsync();

        Assert.Equal("203.0.113.10", opened.RemoteAddress);
        Assert.Equal("198.51.100.20", opened.LocalAddress);
        Assert.False(chunk.Done);
        Assert.Equal("hello", Encoding.UTF8.GetString(chunk.Bytes.Span));
        Assert.True(completed.Done);
        Assert.Equal("203.0.113.10", secureOpened.RemoteAddress);
        Assert.Null(secureOpened.LocalAddress);
        Assert.Equal(
            [
                "socket.connect",
                "socket.opened",
                "socket.write",
                "socket.read",
                "socket.read",
                "socket.closeWritable",
                "socket.startTls",
                "socket.opened",
                "socket.close"
            ],
            dispatcher.Invocations.Select(static call => call.Operation));
        Assert.All(dispatcher.Invocations, static call => Assert.Equal("$socket", call.BindingName));

        using var connectPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal("db.example", connectPayload.RootElement.GetProperty("address").GetProperty("hostname").GetString());
        Assert.Equal(5432, connectPayload.RootElement.GetProperty("address").GetProperty("port").GetInt32());
        Assert.Equal("starttls", connectPayload.RootElement.GetProperty("options").GetProperty("secureTransport").GetString());
        Assert.True(connectPayload.RootElement.GetProperty("options").GetProperty("allowHalfOpen").GetBoolean());

        using var writePayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.Equal("tcp:1", writePayload.RootElement.GetProperty("handle").GetString());
        Assert.Equal(Convert.ToBase64String("ping"u8), writePayload.RootElement.GetProperty("bodyBase64").GetString());

        using var startTlsPayload = JsonDocument.Parse(dispatcher.Invocations[6].PayloadJson);
        Assert.Equal("tcp:1", startTlsPayload.RootElement.GetProperty("handle").GetString());

        using var closePayload = JsonDocument.Parse(dispatcher.Invocations[8].PayloadJson);
        Assert.Equal("tcp:2", closePayload.RootElement.GetProperty("handle").GetString());
    }

    [Fact]
    public async Task TcpSocketDispatchesStringAddress()
    {
        var dispatcher = new CapturingDispatcher("""{"handle":"tcp:1"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-socket-url");

        var socket = await environment.ConnectSocketAsync(
            "example.com:443",
            new SocketOptions { SecureTransport = SocketSecureTransport.On });

        Assert.NotNull(socket);
        using var payload = JsonDocument.Parse(dispatcher.Invocations.Single().PayloadJson);
        Assert.Equal("example.com:443", payload.RootElement.GetProperty("addressText").GetString());
        Assert.Equal("on", payload.RootElement.GetProperty("options").GetProperty("secureTransport").GetString());
    }

    [Fact]
    public async Task AiDispatchesRun()
    {
        var dispatcher = new CapturingDispatcher("""{"output":{"response":"hello","usage":{"totalTokens":7}}}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-ai");

        var output = await environment.Ai("AI").RunAsync<AiPrompt, AiTextOutput>(
            "@cf/meta/llama-3.1-8b-instruct",
            new AiPrompt(["Say hello"]));

        Assert.NotNull(output);
        Assert.Equal("hello", output.Response);
        Assert.Equal(7, output.Usage.TotalTokens);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("ai.run", invocation.Operation);
        Assert.Equal("AI", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("@cf/meta/llama-3.1-8b-instruct", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("Say hello", payload.RootElement.GetProperty("input").GetProperty("messages")[0].GetString());
    }

    [Fact]
    public async Task AiDispatchesRunBytes()
    {
        var dispatcher = new CapturingDispatcher($$"""{"bodyBase64":"{{Convert.ToBase64String([137, 80, 78, 71])}}"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-ai-bytes");

        var body = await environment.Ai("AI").RunBytesAsync(
            "@cf/stabilityai/stable-diffusion-xl-base-1.0",
            new AiPrompt(["Draw a small blue planet"]));

        Assert.Equal([137, 80, 78, 71], body.Bytes.ToArray());
        Assert.Equal("application/octet-stream", body.ContentType);
        var invocation = Assert.Single(dispatcher.Invocations);
        Assert.Equal("ai.runBytes", invocation.Operation);
        Assert.Equal("AI", invocation.BindingName);

        using var payload = JsonDocument.Parse(invocation.PayloadJson);
        Assert.Equal("@cf/stabilityai/stable-diffusion-xl-base-1.0", payload.RootElement.GetProperty("model").GetString());
        Assert.Equal("Draw a small blue planet", payload.RootElement.GetProperty("input").GetProperty("messages")[0].GetString());
    }

    [Fact]
    public async Task ImagesBindingDispatchesInfoAndPipeline()
    {
        var responseJson = JsonSerializer.Serialize(
            ResponseEnvelope.FromResponse(Response.Bytes([9, 8], 203, "image/webp")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var dispatcher = new CapturingDispatcher(
            """{"format":"image/png","fileSize":3,"width":2,"height":1}""",
            responseJson);
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-images");
        var images = environment.Images("IMAGES");

        var info = await images.InfoAsync(Body.FromBytes([1, 2, 3], "image/png"));
        var response = await images
            .Input(Body.FromBytes([1, 2, 3], "image/png"))
            .Transform(new { width = 800 })
            .Draw(Body.FromBytes([4, 5], "image/png"), new { opacity = 0.5 })
            .OutputAsync(new ImagesOutputOptions
            {
                Format = "image/webp",
                Quality = 85,
                Anim = false
            });

        Assert.Equal("image/png", info.Format);
        Assert.Equal(3, info.FileSize);
        Assert.Equal(2, info.Width);
        Assert.Equal(1, info.Height);
        Assert.Equal(203, response.Status);
        Assert.Equal([9, 8], response.Body.Bytes.ToArray());
        Assert.Equal("image/webp", response.Headers.Get("content-type"));
        Assert.Equal(["images.info", "images.pipeline"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("IMAGES", invocation.BindingName));

        using var infoPayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), infoPayload.RootElement.GetProperty("bodyBase64").GetString());
        Assert.Equal("image/png", infoPayload.RootElement.GetProperty("contentType").GetString());

        using var pipelinePayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), pipelinePayload.RootElement.GetProperty("image").GetProperty("bodyBase64").GetString());
        var operations = pipelinePayload.RootElement.GetProperty("operations");
        Assert.Equal("transform", operations[0].GetProperty("kind").GetString());
        Assert.Equal(800, operations[0].GetProperty("options").GetProperty("width").GetInt32());
        Assert.Equal("draw", operations[1].GetProperty("kind").GetString());
        Assert.Equal(Convert.ToBase64String([4, 5]), operations[1].GetProperty("image").GetProperty("bodyBase64").GetString());
        Assert.Equal(0.5, operations[1].GetProperty("options").GetProperty("opacity").GetDouble());
        var output = pipelinePayload.RootElement.GetProperty("output");
        Assert.Equal("image/webp", output.GetProperty("format").GetString());
        Assert.Equal(85, output.GetProperty("quality").GetInt32());
        Assert.False(output.GetProperty("anim").GetBoolean());
    }

    [Fact]
    public async Task ImagesBindingRejectsInvalidInputs()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var images = EnvironmentWithInvocation("invocation-images-invalid").Images("IMAGES");

        await Assert.ThrowsAsync<ArgumentNullException>(() => images.InfoAsync(null!));
        Assert.Throws<ArgumentNullException>(() => images.Input(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await images.Input(Body.FromBytes([1])).OutputAsync(new ImagesOutputOptions { Format = "" }));

        Assert.Empty(dispatcher.Invocations);
    }

    [Fact]
    public async Task MediaBindingDispatchesOutputResults()
    {
        var responseJson = JsonSerializer.Serialize(
            ResponseEnvelope.FromResponse(Response.Bytes([7], 206, "video/mp4")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var dispatcher = new CapturingDispatcher(
            responseJson,
            $$"""{"bodyBase64":"{{Convert.ToBase64String([1, 2, 3])}}","contentType":"audio/mp4"}""",
            """{"contentType":"image/jpeg"}""");
        using var _ = BindingDispatcher.Use(dispatcher);
        var environment = EnvironmentWithInvocation("invocation-media");
        var media = environment.Media("MEDIA");

        var response = await media
            .Input(Body.FromBytes([1, 2, 3, 4], "video/mp4"))
            .Transform(new { width = 480, height = 270, fit = "contain" })
            .Output(new MediaOutputOptions
            {
                Mode = "video",
                Time = "0s",
                Duration = "5s",
                Audio = false
            })
            .ResponseAsync();
        var body = await media
            .Input(Body.FromBytes([5, 6], "video/mp4"))
            .Output(new MediaOutputOptions
            {
                Mode = "audio",
                Time = "1s",
                Duration = "30s",
                Format = "m4a"
            })
            .MediaAsync();
        var contentType = await media
            .Input(Body.FromBytes([8, 9], "video/mp4"))
            .Transform()
            .Output(new MediaOutputOptions
            {
                Mode = "frame",
                Time = "2s",
                Format = "jpg",
                ImageCount = 1
            })
            .ContentTypeAsync();

        Assert.Equal(206, response.Status);
        Assert.Equal([7], response.Body.Bytes.ToArray());
        Assert.Equal("video/mp4", response.Headers.Get("content-type"));
        Assert.Equal([1, 2, 3], body.Bytes.ToArray());
        Assert.Equal("audio/mp4", body.ContentType);
        Assert.Equal("image/jpeg", contentType);
        Assert.Equal(["media.response", "media.media", "media.contentType"], dispatcher.Invocations.Select(static invocation => invocation.Operation));
        Assert.All(dispatcher.Invocations, static invocation => Assert.Equal("MEDIA", invocation.BindingName));

        using var responsePayload = JsonDocument.Parse(dispatcher.Invocations[0].PayloadJson);
        Assert.Equal(Convert.ToBase64String([1, 2, 3, 4]), responsePayload.RootElement.GetProperty("media").GetProperty("bodyBase64").GetString());
        Assert.Equal("video/mp4", responsePayload.RootElement.GetProperty("media").GetProperty("contentType").GetString());
        Assert.True(responsePayload.RootElement.GetProperty("hasTransform").GetBoolean());
        Assert.Equal(480, responsePayload.RootElement.GetProperty("transformOptions").GetProperty("width").GetInt32());
        Assert.Equal("contain", responsePayload.RootElement.GetProperty("transformOptions").GetProperty("fit").GetString());
        var responseOutput = responsePayload.RootElement.GetProperty("output");
        Assert.Equal("video", responseOutput.GetProperty("mode").GetString());
        Assert.Equal("0s", responseOutput.GetProperty("time").GetString());
        Assert.Equal("5s", responseOutput.GetProperty("duration").GetString());
        Assert.False(responseOutput.GetProperty("audio").GetBoolean());

        using var mediaPayload = JsonDocument.Parse(dispatcher.Invocations[1].PayloadJson);
        Assert.False(mediaPayload.RootElement.GetProperty("hasTransform").GetBoolean());
        Assert.Equal(JsonValueKind.Null, mediaPayload.RootElement.GetProperty("transformOptions").ValueKind);
        Assert.Equal("audio", mediaPayload.RootElement.GetProperty("output").GetProperty("mode").GetString());
        Assert.Equal("m4a", mediaPayload.RootElement.GetProperty("output").GetProperty("format").GetString());

        using var contentTypePayload = JsonDocument.Parse(dispatcher.Invocations[2].PayloadJson);
        Assert.True(contentTypePayload.RootElement.GetProperty("hasTransform").GetBoolean());
        Assert.Equal(JsonValueKind.Null, contentTypePayload.RootElement.GetProperty("transformOptions").ValueKind);
        Assert.Equal("frame", contentTypePayload.RootElement.GetProperty("output").GetProperty("mode").GetString());
        Assert.Equal(1, contentTypePayload.RootElement.GetProperty("output").GetProperty("imageCount").GetInt32());
    }

    [Fact]
    public async Task MediaBindingRejectsInvalidInputs()
    {
        var dispatcher = new CapturingDispatcher("{}");
        using var _ = BindingDispatcher.Use(dispatcher);
        var media = EnvironmentWithInvocation("invocation-media-invalid").Media("MEDIA");

        Assert.Throws<ArgumentNullException>(() => media.Input(null!));
        Assert.Throws<ArgumentNullException>(() => media.Input(Body.FromBytes([1])).Output(null!));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await media.Input(Body.FromBytes([1])).Output(new MediaOutputOptions { Mode = "" }).ResponseAsync());

        Assert.Empty(dispatcher.Invocations);
    }
}
