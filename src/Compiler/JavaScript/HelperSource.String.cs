internal static partial class HelperSource
{
    private static string StringContains(Func<string, string> name) => $$"""
        function {{name("stringContains")}}(source, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          return source.includes(value);
        }

        """;

    private static string StringStartsWith(Func<string, string> name) => $$"""
        function {{name("stringStartsWith")}}(source, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          return source.startsWith(value);
        }

        """;

    private static string StringEndsWith(Func<string, string> name) => $$"""
        function {{name("stringEndsWith")}}(source, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          return source.endsWith(value);
        }

        """;

    private static string StringSubstring(Func<string, string> name) => $$"""
        function {{name("stringSubstring")}}(source, start, length) {
          if (start < 0 || start > source.length || length != null && (length < 0 || start + length > source.length))
            throw new RangeError("Substring range is outside the string.");
          return length == null ? source.slice(start) : source.slice(start, start + length);
        }

        """;

    private static string StringReplace(Func<string, string> name) => $$"""
        function {{name("stringReplace")}}(source, oldValue, newValue) {
          if (oldValue == null) throw new TypeError("Value cannot be null.");
          if (oldValue.length === 0) throw new RangeError("Value cannot be empty.");
          return source.replaceAll(oldValue, newValue ?? "");
        }

        """;
}
