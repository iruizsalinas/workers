using Workers;

namespace BodyPipeline;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        if (request.Path == "/form" && request.Method == "POST")
            return await InspectFormAsync(request);
        if (request.Path == "/clone" && request.Method == "POST")
            return await InspectCloneAsync(request);
        if (request.Path == "/decompress" && request.Method == "POST")
            return await DecompressAsync(request);
        if (request.Path == "/fingerprint" && request.Method == "POST")
            return await FingerprintAsync(request);
        if (request.Path == "/preview" && request.Method == "POST")
            return await PreviewAsync(request);
        if (request.Path == "/remainder" && request.Method == "POST")
            return await RemainderAsync(request);
        if (request.Path == "/verify" && request.Method == "POST")
            return await VerifyAsync(request);
        if (request.Path == "/stream")
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
                var preview = await file.SliceBytesAsync(0, 32);
                files.Add(new FileInfo(entry.Key, file.FileName, file.Size, file.ContentType, file.LastModified, TextCodec.EncodeHex(preview)));
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
        return Response.Json(new { decompressed = await new Response(body.Decompress(CompressionFormat.Gzip)).TextAsync() });
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
            sha256 = TextCodec.EncodeHex(digest),
            emptySha256 = TextCodec.EncodeHex(emptyDigest),
            generatedSha256 = TextCodec.EncodeHex(generatedDigest),
            byteSha256 = TextCodec.EncodeHex(byteDigest),
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
        return TextCodec.EncodeUtf8("generated");
    }

    private static async Task<Response> PreviewAsync(Request request)
    {
        var stream = request.Body;
        if (stream is null)
            return new Response("Missing body") { Status = 400 };
        var reader = stream.GetReader();
        var first = await reader.ReadAsync();
        await reader.CancelAsync("preview complete");
        return Response.Json(new { first.Done, preview = TextCodec.DecodeUtf8(first.Value!) });
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
            remainder += TextCodec.DecodeUtf8(next.Value!);
        }
        return Response.Json(new { first = TextCodec.DecodeUtf8(first.Value!), remainder });
    }

    private static async Task<Response> VerifyAsync(Request request)
    {
        var payload = await request.BytesAsync();
        var signature = TextCodec.DecodeHex(request.Headers.Get("x-signature") ?? "");
        var pending = Crypto.VerifyHmacAsync(DigestAlgorithm.Sha256, "body-signing-secret", signature, payload);
        signature = new byte[signature.Length];
        payload = TextCodec.EncodeUtf8("changed");
        return Response.Json(new { valid = await pending });
    }

    private static Response Stream(Request request)
    {
        var value = request.QueryParameters.Get("count") ?? "10";
        var count = (int)Numbers.Min(Numbers.Max(int.Parse(value), 1), 100);
        var stream = ReadableStream.FromAsyncEnumerable(CreateLines(count));
        if ((request.Headers.Get("accept-encoding") ?? "").Contains("gzip"))
            return new Response(stream.Compress(CompressionFormat.Gzip)) { EncodeBody = ResponseBodyEncoding.Manual, Headers =
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
            yield return TextCodec.EncodeUtf8($"{{\"index\":{index},\"id\":\"{Crypto.RandomUuid()}\",\"timestamp\":\"{new Date().ToIsoString()}\"}}\n");
            await Timers.WaitAsync(5);
        }
    }
}

public sealed record FieldInfo(string Name, string Value);
public sealed record FileInfo(string Field, string Name, long Size, string Type, long LastModified, string FirstBytes);
public sealed record FormInspection(IReadOnlyList<FieldInfo> Fields, IReadOnlyList<FileInfo> Files);
