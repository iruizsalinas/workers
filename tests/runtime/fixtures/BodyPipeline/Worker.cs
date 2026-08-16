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
