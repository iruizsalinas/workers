using System.Text.Json.Serialization;

namespace Workers.Tests;

public sealed partial class RequestTests
{
    private sealed record SearchQuery(string Q, int Page, bool Debug, string[] Tag);

    private sealed record SearchForm(string Q, int Page, bool Debug, string[] Tag);

    private sealed record MultipartForm(string Q, string[] Tag);

    private sealed record PagedQuery([property: JsonPropertyName("page_size")] int PageSize);

    private sealed record TaggedQuery([property: JsonPropertyName("tag")] string[] Tags);

    private sealed class JsonPayload
    {
        public string ClientId { get; init; } = "";
    }
}
