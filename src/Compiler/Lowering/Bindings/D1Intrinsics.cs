using static BindingIntrinsicRegistry;

internal static class D1Intrinsics
{
    public static IReadOnlyDictionary<(string Type, string Method), BindingIntrinsic> Methods { get; } =
        new Dictionary<(string Type, string Method), BindingIntrinsic>
        {
            [Key("Workers.D1Database", "Prepare")] = Direct("prepare"),
            [Key("Workers.D1Database", "BatchAsync")] = Direct("batch"),
            [Key("Workers.D1Database", "ExecAsync")] = Direct("exec"),
            [Key("Workers.D1Database", "WithSession")] = Direct("withSession"),
            [Key("Workers.D1DatabaseSession", "Prepare")] = Direct("prepare"),
            [Key("Workers.D1DatabaseSession", "BatchAsync")] = Direct("batch"),
            [Key("Workers.D1DatabaseSession", "GetBookmark")] = Direct("getBookmark"),
            [Key("Workers.D1PreparedStatement", "Bind")] = Direct("bind"),
            [Key("Workers.D1PreparedStatement", "FirstAsync")] = Direct("first"),
            [Key("Workers.D1PreparedStatement", "RunAsync")] = Direct("run"),
            [Key("Workers.D1PreparedStatement", "AllAsync")] = Direct("all"),
            [Key("Workers.D1PreparedStatement", "RawAsync")] = Direct("raw")
        };
}
