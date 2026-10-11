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
        if (request.Path == "/response-ownership")
        {
            var upstream = await Http.FetchAsync("https://upstream.test/");
            var immutable = false;
            var cloneImmutable = false;
            try { upstream.Headers["x-example"] = "original"; }
            catch (Exception) { immutable = true; }
            try { upstream.Clone().Headers.Set("x-example", "clone"); }
            catch (Exception) { cloneImmutable = true; }
            var response = new Response(upstream) { Headers = { ["x-example"] = "copied" } };
            var source = new Response("shared");
            var independent = source.Clone();
            var shared = new Response(source.Body, source);
            var text = await shared.TextAsync();
            var headers = new Headers { ["x-value"] = "initial" };
            var json = Response.Json(new { ok = true }, status: 201, headers: headers);
            headers["x-value"] = "later";
            json.Headers.Append("set-cookie", "first=1");
            json.Headers.Append("set-cookie", "second=2");
            return Response.Json(new
            {
                immutable, cloneImmutable, response.Status,
                location = response.Headers["location"],
                copied = response.Headers["x-example"],
                original = upstream.Headers["x-example"],
                text, source.BodyUsed,
                independentText = await independent.TextAsync(),
                snapshot = json.Headers["x-value"],
                contentType = json.Headers["content-type"],
                cookies = json.Headers.GetSetCookie()
            });
        }

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
            var original = new Response("response-body") { Status = 201, Headers = { ["x-generated"] = "yes" } };
            var clone = original.Clone();
            var text = await clone.TextAsync();
            return Response.Json(new
            {
                status = original.Status,
                header = original.Headers.Get("x-generated"),
                text
            });
        }

        if (request.Path == "/http-semantics")
        {
            var original = request.Headers;
            var clone = new Headers(original);
            clone.Set("x-cloned", "yes");
            clone.Delete("x-remove");
            var responseHeaders = new Response("cookies").Headers;
            responseHeaders.Append("set-cookie", "first=1");
            responseHeaders.Append("set-cookie", "second=2");
            return Response.Json(new
            {
                pathAndQuery = request.PathAndQuery,
                values = request.QueryParameters.GetAll("value"),
                originalCount = original.Count,
                cloneCount = clone.Count,
                originalCloned = original.Contains("x-cloned"),
                cloneRemoved = clone.Contains("x-remove"),
                cookies = responseHeaders.GetSetCookie()
            });
        }

        if (request.Path == "/url")
        {
            var url = request.Url;
            return Response.Json(new
            {
                url.Origin,
                url.Protocol,
                url.Host,
                url.Hostname,
                url.Port,
                url.Username,
                url.Password,
                url.Path,
                url.Query,
                url.Fragment,
                request.Redirect,
                hasSignal = request.Signal != null
            });
        }

        if (request.Path == "/text-codec")
        {
            var bytes = TextCodec.EncodeUtf8("hello + edge");
            var encoded = TextCodec.EncodeBase64(bytes);
            var decoded = TextCodec.DecodeUtf8(TextCodec.DecodeBase64(encoded), fatal: true);
            var escaped = Url.EncodeComponent(decoded.Replace("+", " "));
            var forwarded = request.WithUrl(new Url("/accepted?source=codec", request.Url.Origin));
            return Response.Json(new
            {
                encoded,
                decoded,
                escaped,
                forwarded = forwarded.PathAndQuery
            });
        }

        if (request.Path == "/cache-lifecycle")
        {
            var cache = await CacheStorage.OpenAsync("runtime-intrinsics");
            var key = $"https://cache.test/{Crypto.RandomUuid()}";
            var cacheable = new Response("cached") { Headers = { ["cache-control"] = "public, max-age=60" } };
            await cache.PutAsync(key, cacheable);
            var found = await cache.MatchAsync(key);
            var deleted = await cache.DeleteAsync(key);
            var missing = await cache.MatchAsync(key);
            return Response.Json(new
            {
                found = found is not null,
                deleted,
                missing = missing is null
            });
        }

        if (request.Path == "/body")
            return new Response("body-value");

        if (request.Path == "/body-form")
        {
            var response = new Response(await request.FormDataAsync());
            var usedBefore = response.BodyUsed;
            var form = await response.FormDataAsync();
            var usedAfter = response.BodyUsed;
            string? value = null;
            foreach (var entry in form)
                if (entry.Key == "value") value = entry.Value.Text;
            return Response.Json(new { usedBefore, usedAfter, value });
        }

        if (request.Path == "/body-stream")
            return new Response(request.Body!);

        if (request.Path == "/body-query")
            return new Response(request.QueryParameters);

        if (request.Path == "/stream")
        {
            var bytes = await request.Body!.BytesAsync();
            return Response.Json(new { length = bytes.Length });
        }

        if (request.Path == "/crypto")
        {
            var uuid = Crypto.RandomUuid();
            var random = Crypto.RandomBytes(16);
            var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, "hello");
            var digestStream = Crypto.CreateDigestStream(DigestStreamAlgorithm.Sha256);
            var writer = digestStream.GetWriter();
            await writer.WriteAsync(TextCodec.EncodeUtf8("hel"));
            await writer.WriteAsync(TextCodec.EncodeUtf8("lo"));
            await writer.CloseAsync();
            var streamedDigest = await digestStream.DigestAsync();
            var equal = Crypto.TimingSafeEqual(random, random);
            var streamEqual = Crypto.TimingSafeEqual(digest, streamedDigest);
            return Response.Json(new
            {
                uuid = uuid,
                randomLength = random.Length,
                digestLength = digest.Length,
                equal,
                streamEqual
            });
        }

        if (request.Path == "/timer")
        {
            await Timers.WaitAsync(1);
            return new Response("delayed");
        }

        if (request.Path == "/request-cancellation")
        {
            var signal = request.Signal;
            var threw = false;
            try
            {
                signal.ThrowIfAborted();
            }
            catch (Exception)
            {
                threw = true;
            }
            return Response.Json(new { aborted = signal.Aborted, threw });
        }

        if (request.Path == "/websocket")
        {
            var pair = WebSocketPair.Create();
            pair.Server.Accept();
            pair.Server.SendText("hello");
            return new Response("websocket-sent");
        }

        if (request.Path == "/websocket-events")
        {
            var pair = WebSocketPair.Create();
            pair.Server.Accept();
            var server = pair.Server;
            server.OnMessage(message =>
            {
                server.SendText(message.IsText ? message.AsText() : "binary");
                server.Close(1000, "done");
            });
            return new Response { Status = 101, WebSocket = pair.Client };
        }

        if (request.Path == "/websocket-close")
        {
            var pair = WebSocketPair.Create();
            pair.Server.Accept();
            var key = request.QueryParameters.Get("key")!;
            pair.Server.OnClose(close => context.WaitUntil(environment.Kv("KV").PutJsonAsync(key, new
            {
                code = close.Code,
                reason = close.Reason,
                clean = close.WasClean
            })));
            return new Response { Status = 101, WebSocket = pair.Client };
        }

        if (request.Path == "/html")
        {
            var response = new Response("<main><p>hello</p></main>") { Headers = new Headers { ["content-type"] = "text/html; charset=utf-8" } };
            return new HtmlRewriter()
                .On("p", new ParagraphHandler())
                .Transform(response);
        }

        if (request.Path == "/socket-shape")
        {
            var socket = TcpSocket.Connect("example.com:80");
            await socket.CloseAsync();
            return new Response("socket-closed");
        }

        if (request.Path == "/helpers")
        {
            Log.Info("generated-console");
            Log.Error("generated-error");
            return Response.Json(new
            {
                message = Helpers.ReachableMessage("cross-file"),
                uuid = Crypto.RandomUuid(),
                fraction = Numbers.Random()
            });
        }

        return new Response("Not found") { Status = 404 };
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
