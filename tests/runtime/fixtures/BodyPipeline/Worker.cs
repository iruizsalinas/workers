using Workers;

namespace BodyPipeline;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, ExecutionContext context)
    {
        if (new Url(request.Url).Pathname == "/form" && request.Method == "POST")
            return await InspectFormAsync(request);
        if (new Url(request.Url).Pathname == "/clone" && request.Method == "POST")
            return await InspectCloneAsync(request);
        if (new Url(request.Url).Pathname == "/decompress" && request.Method == "POST")
            return await DecompressAsync(request);
        if (new Url(request.Url).Pathname == "/fingerprint" && request.Method == "POST")
            return await FingerprintAsync(request);
        if (new Url(request.Url).Pathname == "/preview" && request.Method == "POST")
            return await PreviewAsync(request);
        if (new Url(request.Url).Pathname == "/remainder" && request.Method == "POST")
            return await RemainderAsync(request);
        if (new Url(request.Url).Pathname == "/verify" && request.Method == "POST")
            return await VerifyAsync(request);
        if (new Url(request.Url).Pathname == "/stream")
            return Stream(request);
        return new Response("Not found") { Status = 404 };
    }

    private static async Task<Response> InspectFormAsync(Request request)
    {
        var fields = new List<FieldInfo>();
        var files = new List<FileInfo>();
        foreach (var entry in await request.FormDataAsync())
        {
            var file = entry.Value.File;
            if (file is not null)
            {
                var preview = await file.Slice(0, 32).BytesAsync();
                files.Add(new FileInfo(entry.Key, file.Name, file.Size, file.Type, file.LastModified, Hex.Encode(preview)));
                continue;
            }
            fields.Add(new FieldInfo(entry.Key, entry.Value.Text ?? ""));
        }
        return Response.Json(new FormInspection(fields, files));
    }

    private static async Task<Response> InspectCloneAsync(Request request)
    {
        var clone = request.Clone();
        var text = await request.TextAsync();
        var bytes = await clone.BytesAsync();
        var headers = "";
        foreach (var header in request.Headers)
            headers += $"{header.Key}:{header.Value}\n";
        return Response.Json(new { text, byteLength = bytes.Length, request.BodyUsed, cloneBodyUsed = clone.BodyUsed, headers });
    }

    private static async Task<Response> DecompressAsync(Request request)
    {
        var body = request.Body;
        if (body is null)
            return new Response("Missing body") { Status = 400 };
        return Response.Json(new { decompressed = await new Response(body.PipeThrough(new DecompressionStream(CompressionFormat.Gzip))).TextAsync() });
    }

    private static async Task<Response> FingerprintAsync(Request request)
    {
        var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, request.Body!);
        var emptyDigest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, (BodyInit)null!);
        var calls = new int[2];
        var generatedDigest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, CreateBody(calls));
        var byteDigest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, CreateBytes(calls));
        return Response.Json(new
        {
            sha256 = Hex.Encode(digest),
            emptySha256 = Hex.Encode(emptyDigest),
            generatedSha256 = Hex.Encode(generatedDigest),
            byteSha256 = Hex.Encode(byteDigest),
            factoryCalls = calls[0],
            byteFactoryCalls = calls[1]
        });
    }

    private static BodyInit CreateBody(int[] calls)
    {
        calls[0] = calls[0] + 1;
        return "generated";
    }

    private static byte[] CreateBytes(int[] calls)
    {
        calls[1] = calls[1] + 1;
        return new TextEncoder().Encode("generated");
    }

    private static async Task<Response> PreviewAsync(Request request)
    {
        var stream = request.Body;
        if (stream is null)
            return new Response("Missing body") { Status = 400 };
        var reader = stream.GetReader();
        var first = await reader.ReadAsync();
        await reader.CancelAsync("preview complete");
        return Response.Json(new { first.Done, preview = new TextDecoder().Decode(first.Value!) });
    }

    private static async Task<Response> RemainderAsync(Request request)
    {
        var stream = request.Body;
        if (stream is null)
            return new Response("Missing body") { Status = 400 };
        var reader = stream.GetReader();
        var first = await reader.ReadAsync();
        var remainder = "";
        while (!first.Done)
        {
            var next = await reader.ReadAsync();
            if (next.Done) break;
            remainder += new TextDecoder().Decode(next.Value!);
        }
        return Response.Json(new { first = new TextDecoder().Decode(first.Value!), remainder });
    }

    private static async Task<Response> VerifyAsync(Request request)
    {
        var payload = await request.BytesAsync();
        var signature = Hex.Decode(request.Headers.Get("x-signature") ?? "");
        var pending = Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, "body-signing-secret", signature, payload);
        signature = new byte[signature.Length];
        payload = new TextEncoder().Encode("changed");
        return Response.Json(new { valid = await pending });
    }

    private static Response Stream(Request request)
    {
        var value = new Url(request.Url).SearchParams.Get("count") ?? "10";
        var count = (int)Numbers.Min(Numbers.Max(int.Parse(value), 1), 100);
        var stream = ReadableStream.From(CreateLines(count));
        if ((request.Headers.Get("accept-encoding") ?? "").Contains("gzip"))
            return new Response(stream.PipeThrough(new CompressionStream(CompressionFormat.Gzip))) { EncodeBody = ResponseBodyEncoding.Manual, Headers =
                {
                    ["content-type"] = "application/x-ndjson; charset=utf-8",
                    ["content-encoding"] = "gzip",
                    ["vary"] = "Accept-Encoding"
                } };
        return new Response(stream) { Headers = { ["content-type"] = "application/x-ndjson; charset=utf-8", ["cache-control"] = "no-store" }};
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> CreateLines(int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return new TextEncoder().Encode($"{{\"index\":{index},\"id\":\"{Crypto.RandomUuid()}\",\"timestamp\":\"{new Date().ToIsoString()}\"}}\n");
            await Scheduler.WaitAsync(5);
        }
    }
}

public sealed record FieldInfo(string Name, string Value);
public sealed record FileInfo(string Field, string Name, long Size, string Type, double LastModified, string FirstBytes);
public sealed record FormInspection(IReadOnlyList<FieldInfo> Fields, IReadOnlyList<FileInfo> Files);
