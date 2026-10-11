using Workers;

namespace FileGateway;

public static class Worker
{
    [Fetch]
    public static async Task<Response> FetchAsync(Request request, Env environment, Context context)
    {
        var bucket = environment.R2("FILES");
        if (request.Path == "/files" && request.Method == "GET")
        {
            var objects = await ListAllAsync(bucket, request.QueryParameters.Get("prefix") ?? "");
            return Response.Json(new { count = objects.Count, objects });
        }

        if (!request.Path.StartsWith("/files/"))
            return new Response("Not found") { Status = 404 };

        var key = Url.DecodeComponent(request.Path.Substring("/files/".Length));
        switch (request.Method)
        {
            case "PUT":
                return await UploadAsync(request, bucket, key);
            case "GET":
                return await DownloadAsync(bucket, key);
            case "HEAD":
                return await HeadAsync(bucket, key);
            case "DELETE":
                await bucket.DeleteAsync(key);
                return new Response { Status = 204 };
            default:
                return new Response("Method not allowed") { Status = 405, Headers = { ["allow"] = "GET, HEAD, PUT, DELETE" } };
        }
    }

    private static async Task<Response> UploadAsync(Request request, IR2Bucket bucket, string key)
    {
        var body = request.Body;
        if (body is null)
            return new Response("Missing body") { Status = 400 };

        var streams = body.Tee();
        var digestTask = DigestAsync(streams[1]);
        var uploadTask = bucket.PutAsync(key, streams[0], new R2PutOptions
        {
            HttpMetadata = new R2HttpMetadata(
                ContentType: request.Headers.Get("content-type") ?? "application/octet-stream"),
            CustomMetadata = new Dictionary<string, string>
            {
                ["uploadedBy"] = request.Headers.Get("x-user") ?? "anonymous"
            }
        });

        var stored = await uploadTask;
        var digest = await digestTask;
        return Response.Json(new { key, size = stored?.Size, etag = stored?.Etag, sha256 = digest });
    }

    private static async Task<string> DigestAsync(ReadableStream body)
    {
        var digestStream = Crypto.CreateDigestStream(DigestStreamAlgorithm.Sha256);
        await body.PipeToAsync(digestStream);
        var digest = await digestStream.DigestAsync();
        return TextCodec.EncodeHex(digest).ToLowerInvariant();
    }

    private static async Task<Response> DownloadAsync(IR2Bucket bucket, string key)
    {
        var item = await bucket.GetAsync(key);
        if (item is null)
            return new Response("Not found") { Status = 404 };

        var headers = new Headers();
        item.WriteHttpMetadata(headers);
        var response = new Response(item.Body)
        {
            Headers = new Headers(headers)
            {
                ["etag"] = item.HttpEtag,
                ["x-r2-key"] = item.Key,
                ["x-uploaded-at"] = item.Uploaded.ToIsoString()
            }
        };
        var uploadedBy = item.CustomMetadata["uploadedBy"];
        response.Headers["x-uploaded-by"] = uploadedBy;
        return response;
    }

    private static async Task<Response> HeadAsync(IR2Bucket bucket, string key)
    {
        var item = await bucket.HeadAsync(key);
        if (item is null)
            return new Response { Status = 404 };

        return new Response { Headers = { ["etag"] = item.HttpEtag, ["content-length"] = item.Size.ToString() } };
    }

    private static async Task<IReadOnlyList<FileItem>> ListAllAsync(IR2Bucket bucket, string prefix)
    {
        var objects = new List<FileItem>();
        string? cursor = null;
        do
        {
            var page = await bucket.ListAsync(new R2ListOptions
            {
                Prefix = prefix,
                Cursor = cursor,
                Limit = 100,
                Include = ["httpMetadata", "customMetadata"]
            });
            foreach (var item in page.Objects)
                objects.Add(new FileItem(
                    item.Key,
                    item.Size,
                    item.Etag,
                    item.Uploaded.ToIsoString(),
                    item.CustomMetadata));
            cursor = page.Truncated ? page.Cursor : null;
        }
        while (cursor is not null);

        return objects;
    }
}

public sealed record FileItem(
    string Key,
    ulong Size,
    string Etag,
    string Uploaded,
    IReadOnlyDictionary<string, string> Metadata);
