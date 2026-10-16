internal static partial class HelperSource
{
    // int, uint, double and bool Parse. Text is trimmed; integers are plain decimal digits within the
    // type's range. Validate the decimal grammar before Number(), which also accepts
    // JavaScript-only hexadecimal/binary/octal notation and different whitespace.
    private static string NumericParse(Func<string, string> name) => $$"""
        function {{name("numericParse")}}(input, kind) {
          if (input == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Value cannot be null. (Parameter 's')");
          const text = kind === 3
            ? input.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/g, "")
            : input.replace(/\0+$/, "").replace(/^[\u0009-\u000d\u0020]+|[\u0009-\u000d\u0020]+$/g, "");
          if (kind === 3) {
            const value = text.toLowerCase();
            if (value === "true" || value === "false") return value === "true";
            throw new ({{name("exceptionType")}}("System.FormatException"))("Invalid Boolean value.");
          }
          if (kind <= 1) {
            if (!/^[+-]?\d+$/.test(text)) throw new ({{name("exceptionType")}}("System.FormatException"))("Invalid integer value.");
            const number = Number(text);
            if (kind === 0 ? number < -2147483648 || number > 2147483647 : number < 0 || number > 4294967295)
              throw new ({{name("exceptionType")}}("System.OverflowException"))("Integer value is out of range.");
            return kind === 0 ? number | 0 : number >>> 0;
          }
          if (/^[+-]?nan$/i.test(text)) return NaN;
          if (/^[+-]?(?:infinity|∞)$/i.test(text)) return text[0] === "-" ? -Infinity : Infinity;
          if (!/^[+-]?(?:\d[\d,]*(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?$/.test(text))
            throw new ({{name("exceptionType")}}("System.FormatException"))("Invalid number.");
          return Number(text.replaceAll(",", ""));
        }

        """;
}
