internal static partial class HelperSource
{
    // int, uint, double and bool Parse. Text is trimmed; integers are plain decimal digits within the
    // type's range, doubles use JavaScript Number(), and booleans are "true" or "false" in any case.
    private static string NumericParse(Func<string, string> name) => $$"""
        function {{name("numericParse")}}(input, kind) {
          const text = input.trim();
          if (kind === 3) {
            const value = text.toLowerCase();
            if (value === "true" || value === "false") return value === "true";
            throw new TypeError("Invalid Boolean value.");
          }
          if (kind <= 1) {
            if (!/^[+-]?\d+$/.test(text)) throw new TypeError("Invalid integer value.");
            const number = Number(text);
            if (kind === 0 ? number < -2147483648 || number > 2147483647 : number < 0 || number > 4294967295)
              throw new RangeError("Integer value is out of range.");
            return kind === 0 ? number | 0 : number >>> 0;
          }
          const number = Number(text);
          if (text === "" || Number.isNaN(number)) throw new TypeError("Invalid number.");
          return number;
        }

        """;
}
