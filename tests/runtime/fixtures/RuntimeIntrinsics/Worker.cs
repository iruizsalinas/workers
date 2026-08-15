using Workers;

namespace RuntimeIntrinsics;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        Context context)
    {
        if (request.Path == "/request")
        {
            var text = await request.TextAsync();
            return Response.Json(new
            {
                method = request.Method,
                path = request.Path,
                text,
                header = request.Headers.Get("x-test")
            });
        }

        if (request.Path == "/response")
        {
            var original = Response.Text("response-body", 201).WithHeader("x-generated", "yes");
            var clone = original.Clone();
            var text = await clone.TextAsync();
            return Response.Json(new
            {
                status = original.Status,
                header = original.Headers.Get("x-generated"),
                text
            });
        }

        if (request.Path == "/body")
            return Response.FromBody(Body.Text("body-value"));

        if (request.Path == "/stream")
        {
            var bytes = await request.BodyStream().ReadAllBytesAsync();
            return Response.Json(new { length = bytes.Length });
        }

        if (request.Path == "/crypto")
        {
            var uuid = Guid.NewGuid();
            var random = Crypto.RandomBytes(16);
            var digest = await Crypto.DigestTextAsync(DigestAlgorithm.Sha256, "hello");
            var digestStream = Crypto.CreateDigestStream(DigestAlgorithm.Sha256);
            await digestStream.WriteTextAsync("hel");
            await digestStream.WriteTextAsync("lo");
            await digestStream.CloseAsync();
            var streamedDigest = await digestStream.DigestAsync();
            var equal = Crypto.TimingSafeEqual(random, random);
            var streamEqual = Crypto.TimingSafeEqual(digest, streamedDigest);
            return Response.Json(new
            {
                uuid = uuid.ToString(),
                randomLength = random.Length,
                digestLength = digest.Length,
                equal,
                streamEqual
            });
        }

        if (request.Path == "/timer")
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1));
            return Response.Text("delayed");
        }

        if (request.Path == "/abort")
        {
            var controller = new AbortController();
            controller.Abort("test-complete");
            return Response.Text("aborted");
        }

        if (request.Path == "/websocket")
        {
            var pair = WebSocketPair.Create();
            pair.Server.Accept();
            pair.Server.SendText("hello");
            return Response.Text("websocket-sent");
        }

        if (request.Path == "/html")
        {
            var response = Response.Html("<main><p>hello</p></main>");
            return new HtmlRewriter()
                .On("p", new ParagraphHandler())
                .Transform(response);
        }

        if (request.Path == "/socket-shape")
        {
            var socket = TcpSocket.Connect("example.com:80");
            await socket.CloseAsync();
            return Response.Text("socket-closed");
        }

        if (request.Path == "/helpers")
        {
            Console.WriteLine("generated-console");
            Console.Error.WriteLine("generated-error");
            return Response.Json(new
            {
                message = Helpers.ReachableMessage("cross-file"),
                guid = Guid.NewGuid().ToString(),
                next = Random.Shared.Next(),
                bounded = Random.Shared.Next(10),
                ranged = Random.Shared.Next(5, 10),
                fraction = Random.Shared.NextDouble()
            });
        }

        return Response.Text("Not found", status: 404);
    }
}

public sealed class ParagraphHandler : HtmlElementHandler
{
    public override ValueTask ElementAsync(HtmlElement element)
    {
        element.SetAttribute("data-generated", "csharp");
        return ValueTask.CompletedTask;
    }
}
