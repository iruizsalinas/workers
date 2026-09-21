internal static partial class HelperSource
{
    private static string StringBuilder(Func<string, string> name) => $$"""
        function {{name("stringBuilder")}}(value = null) {
          const text = value ?? "";
          return { parts: text.length === 0 ? [] : [text], length: text.length };
        }
        function {{name("stringBuilderAppend")}}(builder, value, line) {
          const text = value ?? "";
          if (text.length !== 0) builder.parts.push(text);
          if (line) builder.parts.push("\n");
          builder.length += text.length + (line ? 1 : 0);
          return builder;
        }
        function {{name("stringBuilderClear")}}(builder) {
          builder.parts.length = 0;
          builder.length = 0;
          return builder;
        }
        function {{name("stringBuilderText")}}(builder) {
          return builder.parts.join("");
        }

        """;
}
