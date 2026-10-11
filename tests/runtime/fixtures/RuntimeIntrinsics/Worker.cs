using Workers;

namespace RuntimeIntrinsics;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(
        Request request,
        Env environment,
        ExecutionContext context)
    {
        var requestUrl = new Url(request.Url);
        var path = requestUrl.Pathname;
        if (path == "/response-ownership")
        {
            var upstream = await Http.FetchAsync("https://upstream.test/");
            var immutable = false;
            var cloneImmutable = false;
            try { upstream.Headers["x-example"] = "original"; }
            catch (Exception) { immutable = true; }
            try { upstream.Clone().Headers.Set("x-example", "clone"); }
            catch (Exception) { cloneImmutable = true; }
            var response = new Response(upstream.Body, upstream) { Headers = { ["x-example"] = "copied" } };
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

        if (path == "/request")
        {
            var text = await request.TextAsync();
            return Response.Json(new
            {
                method = request.Method,
                path,
                text,
                header = request.Headers.Get("x-test")
            });
        }

        if (path == "/response")
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

        if (path == "/http-semantics")
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
                pathAndQuery = requestUrl.Pathname + requestUrl.Search,
                values = requestUrl.SearchParams.GetAll("value"),
                originalCount = original.Keys().Count,
                cloneCount = clone.Keys().Count,
                originalCloned = original.Has("x-cloned"),
                cloneRemoved = clone.Has("x-remove"),
                cookies = responseHeaders.GetSetCookie()
            });
        }

        if (path == "/url")
        {
            var url = requestUrl;
            return Response.Json(new
            {
                url.Origin,
                url.Protocol,
                url.Host,
                url.Hostname,
                url.Port,
                url.Username,
                url.Password,
                path = url.Pathname,
                query = url.Search,
                fragment = url.Hash,
                request.Redirect,
                hasSignal = request.Signal != null
            });
        }

        if (path == "/text-codec")
        {
            var bytes = new TextEncoder().Encode("hello + edge");
            var encoded = Base64.Encode(bytes);
            var decoded = new TextDecoder(fatal: true).Decode(Base64.Decode(encoded));
            var escaped = Url.EncodeComponent(decoded.Replace("+", " "));
            var forwarded = new Url(new Request(new Url("/accepted?source=codec", requestUrl.Origin), request).Url);
            return Response.Json(new
            {
                encoded,
                decoded,
                escaped,
                forwarded = forwarded.Pathname + forwarded.Search
            });
        }

        if (path == "/cache-lifecycle")
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

        if (path == "/body")
            return new Response("body-value");

        if (path == "/body-form")
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

        if (path == "/body-stream")
            return new Response(request.Body!);

        if (path == "/body-query")
            return new Response(requestUrl.SearchParams);

        if (path == "/stream")
        {
            var bytes = await new Response(request.Body).BytesAsync();
            return Response.Json(new { length = bytes.Length });
        }

        if (path == "/crypto")
        {
            var uuid = Crypto.RandomUuid();
            var random = Crypto.GetRandomValues(new byte[16]);
            var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, "hello");
            var digestStream = new DigestStream(DigestStreamAlgorithm.Sha256);
            var writer = digestStream.GetWriter();
            await writer.WriteAsync(new TextEncoder().Encode("hel"));
            await writer.WriteAsync(new TextEncoder().Encode("lo"));
            await writer.CloseAsync();
            var streamedDigest = await digestStream.Digest;
            var equal = Crypto.Subtle.TimingSafeEqual(random, random);
            var streamEqual = Crypto.Subtle.TimingSafeEqual(digest, streamedDigest);
            return Response.Json(new
            {
                uuid = uuid,
                randomLength = random.Length,
                digestLength = digest.Length,
                equal,
                streamEqual
            });
        }

        if (path == "/timer")
        {
            await Scheduler.WaitAsync(1);
            return new Response("delayed");
        }

        if (path == "/request-cancellation")
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

        if (path == "/websocket")
        {
            var pair = new WebSocketPair();
            pair.Server.Accept();
            pair.Server.Send("hello");
            return new Response("websocket-sent");
        }

        if (path == "/websocket-events")
        {
            var pair = new WebSocketPair();
            pair.Server.Accept();
            var server = pair.Server;
            server.OnMessage(message =>
            {
                server.Send(message.IsText ? message.AsText() : "binary");
                server.Close(1000, "done");
            });
            return new Response { Status = 101, WebSocket = pair.Client };
        }

        if (path == "/websocket-close")
        {
            var pair = new WebSocketPair();
            pair.Server.Accept();
            var key = requestUrl.SearchParams.Get("key")!;
            pair.Server.OnClose(close => context.WaitUntil(environment.KV("KV").PutJsonAsync(key, new
            {
                code = close.Code,
                reason = close.Reason,
                clean = close.WasClean
            })));
            return new Response { Status = 101, WebSocket = pair.Client };
        }

        if (path == "/html")
        {
            var response = new Response("<main><p>hello</p></main>") { Headers = new Headers { ["content-type"] = "text/html; charset=utf-8" } };
            return new HtmlRewriter()
                .On("p", new ParagraphHandler())
                .Transform(response);
        }

        if (path == "/socket-shape")
        {
            var socket = Socket.Connect("example.com:80");
            await socket.CloseAsync();
            return new Response("socket-closed");
        }

        if (path == "/helpers")
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
    public override Task ElementAsync(HtmlElement element)
    {
        element.SetAttribute("data-generated", "csharp");
        return Task.CompletedTask;
    }
}
