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
        function {{name("stringBuilderSetText")}}(builder, text) {
          builder.parts = text.length === 0 ? [] : [text];
          builder.length = text.length;
          return builder;
        }
        function {{name("stringBuilderAppendValue")}}(builder, value, kind) {
          return {{name("stringBuilderAppend")}}(builder,
            kind === 1 ? (value ? "True" : "False") : String(value), false);
        }
        function {{name("stringBuilderAppendRepeat")}}(builder, value, count) {
          if (!Number.isInteger(count) || count < 0)
            throw new RangeError("Repeat count cannot be negative.");
          return {{name("stringBuilderAppend")}}(builder, value.repeat(count), false);
        }
        function {{name("stringBuilderInsert")}}(builder, index, value) {
          if (!Number.isInteger(index) || index < 0 || index > builder.length)
            throw new RangeError("Index was outside the bounds of the StringBuilder.");
          const current = {{name("stringBuilderText")}}(builder), text = value ?? "";
          return {{name("stringBuilderSetText")}}(builder,
            current.slice(0, index) + text + current.slice(index));
        }
        function {{name("stringBuilderRemove")}}(builder, start, count) {
          if (!Number.isInteger(start) || !Number.isInteger(count)
              || start < 0 || count < 0 || start + count > builder.length)
            throw new RangeError("Range was outside the bounds of the StringBuilder.");
          const current = {{name("stringBuilderText")}}(builder);
          return {{name("stringBuilderSetText")}}(builder,
            current.slice(0, start) + current.slice(start + count));
        }
        function {{name("stringBuilderReplace")}}(builder, oldValue, newValue) {
          if (oldValue == null) throw new TypeError("Value cannot be null.");
          if (oldValue.length === 0) throw new RangeError("Value cannot be empty.");
          return {{name("stringBuilderSetText")}}(builder,
            {{name("stringBuilderText")}}(builder).replaceAll(oldValue, newValue ?? ""));
        }
        function {{name("stringBuilderLength")}}(builder, length) {
          if (!Number.isInteger(length) || length < 0 || length > 2147483647)
            throw new RangeError("Length is outside the supported range.");
          const current = {{name("stringBuilderText")}}(builder);
          {{name("stringBuilderSetText")}}(builder,
            length <= current.length ? current.slice(0, length) : current + "\0".repeat(length - current.length));
          return length;
        }
        function {{name("stringBuilderAppendJoin")}}(builder, separator, values) {
          if (values == null) throw new TypeError("Values cannot be null.");
          let first = true;
          for (const value of values) {
            if (!first) {{name("stringBuilderAppend")}}(builder, separator, false);
            {{name("stringBuilderAppend")}}(builder, value, false);
            first = false;
          }
          return builder;
        }

        """;
}

