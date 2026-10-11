internal static partial class HelperSource
{
    private static string JsonExpect(Func<string, string> name) => $$"""
        function {{name("jsonExpect")}}(value, type) {
          if (typeof value !== type) throw new TypeError(`Expected a JSON ${type}, got ${value === null ? "null" : typeof value}.`);
          return value;
        }

        """;
}
