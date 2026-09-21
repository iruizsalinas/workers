internal static partial class HelperSource
{
    private static string NumericParse(Func<string, string> name) => $$"""
        function {{name("numericParse")}}(input, kind) {
          if (input == null) throw new TypeError("Value cannot be null.");
          const value = input.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/gu, "");
          if (kind === 4) {
            if (/^true$/i.test(value)) return true;
            if (/^false$/i.test(value)) return false;
            throw new TypeError("Invalid Boolean value.");
          }
          if (kind <= 1) {
            if (!/^[+-]?\d+$/.test(value)) throw new TypeError("Invalid integer value.");
            const number = Number(value), minimum = kind === 0 ? -2147483648 : 0;
            const maximum = kind === 0 ? 2147483647 : 4294967295;
            if (!Number.isSafeInteger(number) || number < minimum || number > maximum)
              throw new RangeError("Integer value is out of range.");
            return kind === 0 ? number | 0 : number >>> 0;
          }
          if (!/^[+-]?(?:(?:\d+(?:\.\d*)?|\.\d+)(?:e[+-]?\d+)?|nan|infinity)$/i.test(value))
            throw new TypeError("Invalid floating-point value.");
          const number = Number(value);
          return kind === 2 ? Math.fround(number) : number;
        }

        """;

    private static string NumericFormat(Func<string, string> name) => $$"""
        function {{name("numericFormat")}}(value, format, kind, precision) {
          if (kind <= 1) {
            const code = format[0].toUpperCase();
            let result = code === "X"
              ? (kind === 0 ? value >>> 0 : value).toString(16)
              : Math.abs(value).toString(10);
            result = result.padStart(precision, "0");
            if (code === "D" && value < 0) result = "-" + result;
            return format[0] === "x" ? result.toLowerCase() : result.toUpperCase();
          }
          return {{name("mathRound")}}(value, precision, kind === 2 ? 6 : 15).toFixed(precision);
        }

        """;

    private static string MathAbsInt(Func<string, string> name) => $$"""
        function {{name("mathAbsInt")}}(value) {
          if (value === -2147483648) throw new RangeError("Absolute value is out of range.");
          return Math.abs(value) | 0;
        }

        """;

    private static string MathClamp(Func<string, string> name) => $$"""
        function {{name("mathClamp")}}(value, minimum, maximum) {
          if (minimum > maximum) throw new RangeError("Minimum cannot exceed maximum.");
          return Math.min(Math.max(value, minimum), maximum);
        }

        """;

    private static string MathRound(Func<string, string> name) => $$"""
        function {{name("mathRound")}}(value, digits, maximumDigits) {
          if (!Number.isInteger(digits) || digits < 0 || digits > maximumDigits)
            throw new RangeError("Rounding digits are out of range.");
          const scale = 10 ** digits, scaled = value * scale;
          if (!Number.isFinite(scaled)) return value;
          const floor = Math.floor(scaled), fraction = scaled - floor;
          const rounded = fraction < 0.5 ? floor : fraction > 0.5 ? floor + 1
            : floor % 2 === 0 ? floor : floor + 1;
          const result = rounded / scale;
          return result === 0 && (value < 0 || Object.is(value, -0)) ? -0 : result;
        }

        """;

    private static string MathSign(Func<string, string> name) => $$"""
        function {{name("mathSign")}}(value) {
          if (Number.isNaN(value)) throw new RangeError("NaN has no sign.");
          return value < 0 ? -1 : value > 0 ? 1 : 0;
        }

        """;

    private static string MathLog(Func<string, string> name) => $$"""
        function {{name("mathLog")}}(value, base) {
          if (base <= 0 || base === 1 || !Number.isFinite(base)) return NaN;
          return Math.log(value) / Math.log(base);
        }

        """;
}

