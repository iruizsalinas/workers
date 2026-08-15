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
            return Response.Text("Not found", 404);

        var key = Uri.UnescapeDataString(request.Path.Substring("/files/".Length));
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
                return Response.Empty(204);
            default:
                return Response.Text("Method not allowed", 405)
                    .WithHeader("allow", "GET, HEAD, PUT, DELETE");
        }
    }

    private static async Task<Response> UploadAsync(Request request, IR2Bucket bucket, string key)
    {
        var body = request.BodyStream();
        if (body is null)
            return Response.Text("Missing body", 400);

        var streams = body.Tee();
        var digestTask = DigestAsync(streams[1]);
        var uploadTask = bucket.PutObjectAsync(key, streams[0], new R2PutOptions
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
        var digestStream = Crypto.CreateDigestStream(DigestAlgorithm.Sha256);
        await body.PipeToAsync(digestStream);
        var digest = await digestStream.DigestAsync();
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static async Task<Response> DownloadAsync(IR2Bucket bucket, string key)
    {
        var item = await bucket.GetObjectAsync(key);
        if (item is null)
            return Response.Text("Not found", 404);

        var headers = new Headers();
        item.WriteHttpMetadata(headers);
        var response = Response.FromStream(item.Body, headers)
            .WithHeader("etag", item.HttpEtag)
            .WithHeader("x-r2-key", item.Key)
            .WithHeader("x-uploaded-at", item.Uploaded.ToString("O"));
        var uploadedBy = item.CustomMetadata["uploadedBy"];
        return response.WithHeader("x-uploaded-by", uploadedBy);
    }

    private static async Task<Response> HeadAsync(IR2Bucket bucket, string key)
    {
        var item = await bucket.HeadAsync(key);
        if (item is null)
            return Response.Empty(404);

        return Response.Empty()
            .WithHeader("etag", item.HttpEtag)
            .WithHeader("content-length", item.Size.ToString());
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
                    item.Uploaded.ToString("O"),
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
