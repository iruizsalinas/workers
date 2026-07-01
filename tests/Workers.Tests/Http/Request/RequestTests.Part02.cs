using System.Text;
using Xunit;

namespace Workers.Tests;

public sealed partial class RequestTests
{
    [Fact]
    public void RequestJsonUsesWebDefaults()
    {
        var request = Request.Post(
            "https://example.com/value",
            Body.Json(new JsonPayload { ClientId = "frontend" }));

        Assert.Equal("""{"clientId":"frontend"}""", request.Text());
        Assert.Equal("frontend", request.Json<JsonPayload>()!.ClientId);
    }

    [Fact]
    public void RequestBuilderPreservesExplicitContentType()
    {
        var request = Request.Builder("https://example.com/value")
            .WithMethod("POST")
            .WithHeader("content-type", "application/merge-patch+json")
            .WithJson(new { ok = true })
            .Build();

        Assert.Equal("application/merge-patch+json", request.Headers.Get("content-type"));
    }

    [Fact]
    public void RequestBuilderReplacesHeadersAndAppliesBodyContentType()
    {
        var headers = new Headers().Set("x-test", "one");

        var request = Request.Builder("https://example.com/value")
            .WithMethod("POST")
            .WithJson(new { ok = true })
            .WithHeaders(headers)
            .Build();

        headers.Set("x-test", "changed");

        Assert.Equal("one", request.Headers.Get("x-test"));
        Assert.Equal("application/json", request.Headers.Get("content-type"));
    }

    [Fact]
    public void RequestBuilderUpdatesBodyContentTypeWhenBodyChanges()
    {
        var request = Request.Builder("https://example.com/value")
            .WithMethod("POST")
            .WithText("hello")
            .WithBytes([1, 2, 3], "application/custom")
            .Build();

        Assert.Equal("application/custom", request.Headers.Get("content-type"));
        Assert.Equal([1, 2, 3], request.Body.Bytes.ToArray());
    }

    [Fact]
    public void RequestBuilderRemovesAutomaticContentTypeForEmptyBody()
    {
        var request = Request.Builder("https://example.com/value")
            .WithText("hello")
            .WithBody(Body.Empty)
            .Build();

        Assert.True(request.Body.IsEmpty);
        Assert.False(request.Headers.Contains("content-type"));
    }

    [Fact]
    public void QueryParametersParseDecodedRepeatedAndFlagValues()
    {
        var request = Request.Get("https://example.com/search?q=Ada+Lovelace&tag=math&tag=poetry&empty=&debug&encoded=%E2%9C%93");

        var query = request.QueryParameters;

        Assert.Equal("Ada Lovelace", query.Get("q"));
        Assert.Equal(["math", "poetry"], query.GetAll("tag"));
        Assert.Equal("", query.Get("empty"));
        Assert.Equal("", query.Get("debug"));
        Assert.Equal("✓", query.Get("encoded"));
        Assert.True(query.Contains("debug"));
        Assert.True(query.TryGet("q", out var value));
        Assert.Equal("Ada Lovelace", value);
        Assert.Equal("Ada Lovelace", query.GetRequired("q"));
        Assert.False(query.Contains("missing"));
        Assert.False(query.TryGet("missing", out var missing));
        Assert.Null(missing);
        Assert.Null(query.Get("missing"));
        Assert.Throws<WorkersException>(() => query.GetRequired("missing"));
        Assert.Equal(
            ["q", "tag", "tag", "empty", "debug", "encoded"],
            query.Entries.Select(static entry => entry.Name));
    }

    [Fact]
    public void QueryParametersReflectUrlVariants()
    {
        var original = Request.Get("https://example.com/search?q=old");
        var moved = original.WithUrl("https://example.com/search?q=new&tag=one");

        Assert.Equal("old", original.QueryParameters.Get("q"));
        Assert.Equal("new", moved.QueryParameters.Get("q"));
        Assert.Equal(["one"], moved.QueryParameters.GetAll("tag"));
        Assert.Empty(Request.Get("https://example.com/search").QueryParameters.Entries);
    }

    [Fact]
    public void QueryParametersDeserializeTypedRecords()
    {
        var request = Request.Get("https://example.com/search?q=Ada&page=2&debug&tag=math&tag=poetry");

        var query = request.Query<SearchQuery>();

        Assert.Equal("Ada", query.Q);
        Assert.Equal(2, query.Page);
        Assert.True(query.Debug);
        Assert.Equal(["math", "poetry"], query.Tag);
    }

    [Fact]
    public void QueryParametersDeserializeJsonPropertyNames()
    {
        var request = Request.Get("https://example.com/search?page_size=25");

        var query = request.Query<PagedQuery>();

        Assert.Equal(25, query.PageSize);
    }

    [Fact]
    public void QueryParametersDeserializeRepeatedJsonPropertyNames()
    {
        var request = Request.Get("https://example.com/search?tag=math&tag=poetry");

        var query = request.Query<TaggedQuery>();

        Assert.Equal(["math", "poetry"], query.Tags);
    }

    [Fact]
    public void QueryParametersRejectInvalidTypedValues()
    {
        var request = Request.Get("https://example.com/search?page=nope");

        Assert.Throws<WorkersException>(() => request.Query<SearchQuery>());
    }

    [Fact]
    public void FormDataParsesUrlEncodedFields()
    {
        var request = Request.Post(
            "https://example.com/upload",
            Body.Text("name=Ada+Lovelace&tag=math&tag=poetry&empty=&encoded=%E2%9C%93", "application/x-www-form-urlencoded"));

        var form = request.FormData();

        Assert.Equal("Ada Lovelace", form.GetField("name"));
        Assert.Equal("", form.GetField("empty"));
        Assert.Equal("✓", form.GetField("encoded"));
        Assert.Equal(["math", "poetry"], form.GetAll("tag").Cast<FormField>().Select(static field => field.Value));
        Assert.True(form.Contains("name"));
        Assert.True(form.TryGet("name", out var entry));
        Assert.IsType<FormField>(entry);
        Assert.True(form.TryGetField("name", out var name));
        Assert.Equal("Ada Lovelace", name);
        Assert.Equal("Ada Lovelace", form.GetRequiredField("name"));
        Assert.False(form.TryGetField("missing", out var missingField));
        Assert.Null(missingField);
        Assert.Null(form.Get("missing"));
        Assert.Throws<WorkersException>(() => form.GetRequiredField("missing"));
        Assert.All(form.Entries, static entry => Assert.IsType<FormField>(entry));
    }

    [Fact]
    public void FormDataDeserializesTypedUrlEncodedFields()
    {
        var request = Request.Post(
            "https://example.com/search",
            Body.Text("q=Ada&page=2&debug&tag=math&tag=poetry&page_size=25", "application/x-www-form-urlencoded"));

        var form = request.Form<SearchForm>();
        var paged = request.FormData().As<PagedQuery>();
        var tagged = request.FormData().As<TaggedQuery>();

        Assert.Equal("Ada", form.Q);
        Assert.Equal(2, form.Page);
        Assert.True(form.Debug);
        Assert.Equal(["math", "poetry"], form.Tag);
        Assert.Equal(25, paged.PageSize);
        Assert.Equal(["math", "poetry"], tagged.Tags);
    }

    [Fact]
    public void FormDataParsesMultipartFieldsAndFiles()
    {
        const string boundary = "----workers-boundary";
        var body = string.Join(
            "\r\n",
            [
                $"--{boundary}",
                "Content-Disposition: form-data; name=\"title\"",
                "",
                "Report",
                $"--{boundary}",
                "Content-Disposition: form-data; name=\"upload\"; filename=\"report;final.txt\"",
                "Content-Type: text/plain",
                "",
                "hello file",
                $"--{boundary}--",
                ""
            ]);
        var request = Request.Post(
            "https://example.com/upload",
            Body.FromBytes(Encoding.UTF8.GetBytes(body), $"multipart/form-data; boundary=\"{boundary}\""));

        var form = request.FormData();
        var title = Assert.IsType<FormField>(form.Get("title"));
        var file = Assert.IsType<FormFile>(form.Get("upload"));

        Assert.Equal(2, form.Entries.Count);
        Assert.Equal("Report", title.Value);
        Assert.Equal("report;final.txt", file.FileName);
        Assert.Equal("text/plain", file.ContentType);
        Assert.Equal("hello file", file.Text());
        Assert.Equal(Encoding.UTF8.GetBytes("hello file"), file.Bytes.ToArray());
        Assert.Same(file, form.GetFile("upload"));
        Assert.True(form.TryGetFile("upload", out var upload));
        Assert.Same(file, upload);
        Assert.Same(file, form.GetRequiredFile("upload"));
        Assert.Null(form.GetFile("title"));
        Assert.False(form.TryGetFile("title", out var notAFile));
        Assert.Null(notAFile);
        Assert.Throws<WorkersException>(() => form.GetRequiredFile("title"));
    }

    [Fact]
    public void FormDataDeserializesTypedMultipartTextFieldsAndIgnoresFiles()
    {
        const string boundary = "----workers-boundary";
        var body = string.Join(
            "\r\n",
            [
                $"--{boundary}",
                "Content-Disposition: form-data; name=\"q\"",
                "",
                "Ada",
                $"--{boundary}",
                "Content-Disposition: form-data; name=\"tag\"",
                "",
                "math",
                $"--{boundary}",
                "Content-Disposition: form-data; name=\"tag\"",
                "",
                "poetry",
                $"--{boundary}",
                "Content-Disposition: form-data; name=\"upload\"; filename=\"ignored.txt\"",
                "Content-Type: text/plain",
                "",
                "not a scalar",
                $"--{boundary}--",
                ""
            ]);
        var request = Request.Post(
            "https://example.com/upload",
            Body.FromBytes(Encoding.UTF8.GetBytes(body), $"multipart/form-data; boundary={boundary}"));

        var form = request.Form<MultipartForm>();

        Assert.Equal("Ada", form.Q);
        Assert.Equal(["math", "poetry"], form.Tag);
    }

    [Fact]
    public void FormDataRejectsInvalidTypedValues()
    {
        var request = Request.Post(
            "https://example.com/search",
            Body.Text("page=nope", "application/x-www-form-urlencoded"));

        Assert.Throws<WorkersException>(() => request.Form<SearchForm>());
    }

    [Fact]
    public void FormDataRejectsUnsupportedOrInvalidContentTypes()
    {
        var json = Request.Post(
            "https://example.com/upload",
            Body.Json(new { ok = true }));
        var missingBoundary = Request.Post(
            "https://example.com/upload",
            Body.Text("--missing", "multipart/form-data"));
        var missingContentType = new Request(new Uri("https://example.com/upload"), "POST");

        Assert.Throws<WorkersException>(() => json.FormData());
        Assert.Throws<WorkersException>(() => missingBoundary.FormData());
        Assert.Throws<WorkersException>(() => missingContentType.FormData());
    }
}
