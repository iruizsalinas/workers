using System.Text;
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
        return Response.Text("Not found", 404);
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
                files.Add(new FileInfo(entry.Key, file.FileName, file.Size, file.ContentType, file.LastModified, Convert.ToHexString(preview)));
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
        var body = request.BodyStream();
        if (body is null)
            return Response.Text("Missing body", 400);
        return Response.Json(new { decompressed = await Response.FromStream(body.Decompress(CompressionFormat.Gzip)).TextAsync() });
    }

    private static async Task<Response> FingerprintAsync(Request request)
    {
        var digest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, request.Body);
        var emptyDigest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, Body.Empty);
        var calls = new int[2];
        var generatedDigest = await Crypto.DigestAsync(DigestAlgorithm.Sha256, CreateBody(calls));
        var byteDigest = await Crypto.DigestBytesAsync(DigestAlgorithm.Sha256, CreateBytes(calls));
        return Response.Json(new
        {
            sha256 = Convert.ToHexString(digest),
            emptySha256 = Convert.ToHexString(emptyDigest),
            generatedSha256 = Convert.ToHexString(generatedDigest),
            byteSha256 = Convert.ToHexString(byteDigest),
            factoryCalls = calls[0],
            byteFactoryCalls = calls[1]
        });
    }

    private static Body CreateBody(int[] calls)
    {
        calls[0] = calls[0] + 1;
        return Body.Text("generated");
    }

    private static byte[] CreateBytes(int[] calls)
    {
        calls[1] = calls[1] + 1;
        return Encoding.UTF8.GetBytes("generated");
    }

    private static async Task<Response> PreviewAsync(Request request)
    {
        var stream = request.BodyStream();
        if (stream is null)
            return Response.Text("Missing body", 400);
        var first = await stream.ReadAsync();
        await stream.CancelAsync();
        return Response.Json(new { first.Done, preview = TextCodec.DecodeUtf8(first.Bytes) });
    }

    private static async Task<Response> RemainderAsync(Request request)
    {
        var stream = request.BodyStream();
        if (stream is null)
            return Response.Text("Missing body", 400);
        var first = await stream.ReadAsync();
        var remainder = await stream.ReadAllBytesAsync();
        return Response.Json(new { first = TextCodec.DecodeUtf8(first.Bytes), remainder = TextCodec.DecodeUtf8(remainder) });
    }

    private static async Task<Response> VerifyAsync(Request request)
    {
        var payload = await request.BytesAsync();
        var signature = Convert.FromHexString(request.Headers.Get("x-signature") ?? "");
        var pending = Crypto.VerifyHmacSha256Async("body-signing-secret", signature, payload);
        signature = new byte[signature.Length];
        payload = Encoding.UTF8.GetBytes("changed");
        return Response.Json(new { valid = await pending });
    }

    private static Response Stream(Request request)
    {
        var value = request.QueryParameters.Get("count") ?? "10";
        var count = Math.Min(Math.Max(int.Parse(value), 1), 100);
        var stream = ReadableStream.FromAsyncEnumerable(CreateLines(count));
        if ((request.Headers.Get("accept-encoding") ?? "").Contains("gzip"))
            return Response.FromStream(stream.Compress(CompressionFormat.Gzip))
                .WithHeader("content-type", "application/x-ndjson; charset=utf-8")
                .WithHeader("content-encoding", "gzip")
                .AppendHeader("vary", "Accept-Encoding");
        return Response.FromStream(stream)
            .WithHeader("content-type", "application/x-ndjson; charset=utf-8")
            .WithHeader("cache-control", "no-store");
    }

    private static async IAsyncEnumerable<ReadOnlyMemory<byte>> CreateLines(int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return Encoding.UTF8.GetBytes($"{{\"index\":{index},\"id\":\"{Guid.NewGuid()}\",\"timestamp\":\"{DateTimeOffset.UtcNow:O}\"}}\n");
            await Task.Delay(5);
        }
    }
}

public sealed record FieldInfo(string Name, string Value);
public sealed record FileInfo(string Field, string Name, long Size, string Type, long LastModified, string FirstBytes);
public sealed record FormInspection(IReadOnlyList<FieldInfo> Fields, IReadOnlyList<FileInfo> Files);
