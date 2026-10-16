internal static partial class HelperSource
{
    // System.Text.Json ignores ValueTuple's public fields unless IncludeFields is enabled.
    private static string Tuple(Func<string, string> name) => $$"""
        function {{name("tupleCreate")}}(values) {
          Object.defineProperty(values, "toJSON", { value() { return {}; } });
          return values;
        }

        """;
}
