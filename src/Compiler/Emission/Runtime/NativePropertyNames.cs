// Runtime fields whose names are not the camelCase form of the API property, used both to read runtime objects
// and to write structural option objects.
internal static class NativePropertyNames
{
    // Types whose runtime fields are all snake_case.
    private static readonly HashSet<string> SnakeCaseTypes =
    [
        "Workers.D1Meta", "Workers.D1Timings", "Workers.AiGatewayLog", "Workers.AiModelsSearchParams"
    ];

    private static readonly Dictionary<(string Type, string Property), string> Renamed = new()
    {
        [("Workers.HtmlElement", "NamespaceUri")] = "namespaceURI",
        [("Workers.TextDecoder", "IgnoreBom")] = "ignoreBOM",
        [("Workers.KVNamespaceListResult", "ListComplete")] = "list_complete",
        [("Workers.JsonWebKey", "KeyOps")] = "key_ops",
        [("Workers.RequestInitCfPropertiesVaryHeader", "MediaTypes")] = "media_types",
        [("Workers.CloudflareAccessIdentity", "UserUuid")] = "user_uuid",
        [("Workers.CloudflareAccessIdentity", "AccountId")] = "account_id",
        [("Workers.CloudflareAccessIdentity", "IsWarp")] = "is_warp",
        [("Workers.CloudflareAccessIdentity", "IsGateway")] = "is_gateway",
        [("Workers.AiModelsSearchProperty", "PropertyId")] = "property_id"
    };

    /// <summary>The runtime name of a property, or null when it is the camelCase form of the API name.</summary>
    public static string? Get(string type, string property) =>
        Renamed.TryGetValue((type, property), out var name) ? name
        : SnakeCaseTypes.Contains(type) ? SnakeCase(property)
        : null;

    private static string SnakeCase(string value) =>
        string.Concat(value.Select((character, index) => char.IsUpper(character)
            ? (index == 0 ? "" : "_") + char.ToLowerInvariant(character)
            : character.ToString()));
}
