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
          if (!/^[+-]?(?:(?:\d[\d,]*(?:\.\d*)?|\.\d+)(?:e[+-]?\d+)?|nan|infinity)$/i.test(value))
            throw new TypeError("Invalid floating-point value.");
          // NumberStyles.Float | AllowThousands accepts group separators in the integer part.
          const number = Number(value.replaceAll(",", ""));
          return kind === 2 ? Math.fround(number) : number;
        }

        """;

    private static string NumericFormat(Func<string, string> name) => $$"""
        function {{name("numericFormat")}}(value, format, kind, precision) {
          const code = format[0].toUpperCase();
          if (code === "N" || code === "P" || kind <= 1 && code === "F") {
            const scaled = code === "P" ? value * 100 : value;
            const rounded = {{name("mathRound")}}(scaled, precision, kind === 2 ? 6 : 15);
            let text = Math.abs(rounded) < 1e21 ? rounded.toFixed(precision)
              : BigInt(Math.trunc(rounded)).toString() + (precision > 0 ? "." + "0".repeat(precision) : "");
            if ((scaled < 0 || Object.is(scaled, -0)) && text[0] !== "-") text = "-" + text;
            if (code !== "F") {
              const negative = text[0] === "-", body = negative ? text.slice(1) : text;
              const point = body.indexOf(".");
              const integer = point < 0 ? body : body.slice(0, point), fraction = point < 0 ? "" : body.slice(point);
              let grouped = "";
              for (let index = 0; index < integer.length; index++) {
                if (index !== 0 && (integer.length - index) % 3 === 0) grouped += ",";
                grouped += integer[index];
              }
              text = (negative ? "-" : "") + grouped + fraction;
            }
            return code === "P" ? text + " %" : text;
          }
          if (kind <= 1) {
            const code = format[0].toUpperCase();
            let result = code === "X"
              ? (kind === 0 ? value >>> 0 : value).toString(16)
              : Math.abs(value).toString(10);
            result = result.padStart(precision, "0");
            if (code === "D" && value < 0) result = "-" + result;
            return format[0] === "x" ? result.toLowerCase() : result.toUpperCase();
          }
          const text = {{name("mathRound")}}(value, precision, kind === 2 ? 6 : 15).toFixed(precision);
          return (value < 0 || Object.is(value, -0)) && text[0] !== "-" ? "-" + text : text;
        }

        """;

    // Formats like double.ToString() and float.ToString() on .NET Core 3.0+: shortest round-trip
    // digits, scientific notation outside the general format range, and a preserved negative zero.
    private static string NumberText(Func<string, string> name) => $$"""
        function {{name("numberText")}}(value, single = false) {
          if (Number.isNaN(value)) return "NaN";
          if (value === Infinity) return "Infinity";
          if (value === -Infinity) return "-Infinity";
          if (value === 0) return Object.is(value, -0) ? "-0" : "0";
          let exponential = value.toExponential();
          if (single) {
            for (let precision = 1; precision <= 9; precision++) {
              const candidate = Number(value.toPrecision(precision));
              if (Math.fround(candidate) === value) { exponential = candidate.toExponential(); break; }
            }
          }
          const match = /^(-?)([0-9])(?:[.]([0-9]+))?e([-+][0-9]+)$/.exec(exponential);
          const sign = match[1], digits = match[2] + (match[3] ?? ""), exponent = Number(match[4]);
          if (exponent >= (single ? 9 : 17) || exponent < -4) {
            const mantissa = digits.length === 1 ? digits : digits[0] + "." + digits.slice(1);
            return sign + mantissa + "E" + (exponent < 0 ? "-" : "+") + String(Math.abs(exponent)).padStart(2, "0");
          }
          if (exponent < 0) return sign + "0." + "0".repeat(-exponent - 1) + digits;
          const whole = digits.padEnd(exponent + 1, "0");
          const integer = whole.slice(0, exponent + 1), fraction = whole.slice(exponent + 1);
          return sign + integer + (fraction ? "." + fraction : "");
        }

        """;

    // DateTime and DateTimeOffset formatting with the invariant culture. Values are UTC instants.
    private static string DateFormat(Func<string, string> name) => $$"""
        function {{name("dateFormat")}}(value, format, offset) {
          const standard = {
            d: "MM/dd/yyyy", D: "dddd, dd MMMM yyyy", t: "HH:mm", T: "HH:mm:ss",
            f: "dddd, dd MMMM yyyy HH:mm", F: "dddd, dd MMMM yyyy HH:mm:ss",
            g: "MM/dd/yyyy HH:mm", G: "MM/dd/yyyy HH:mm:ss", M: "MMMM dd", m: "MMMM dd",
            Y: "yyyy MMMM", y: "yyyy MMMM", s: "yyyy'-'MM'-'dd'T'HH':'mm':'ss",
            u: "yyyy'-'MM'-'dd HH':'mm':'ss'Z'", R: "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'", r: "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'"
          };
          if (format.length === 1) {
            if (!Object.hasOwn(standard, format)) throw new RangeError("Input string was not in a correct format.");
            format = standard[format];
          }
          const date = new Date(value);
          const year = date.getUTCFullYear(), month = date.getUTCMonth(), day = date.getUTCDate();
          const hour = date.getUTCHours(), minute = date.getUTCMinutes(), second = date.getUTCSeconds();
          const millisecond = date.getUTCMilliseconds(), weekday = date.getUTCDay();
          const months = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
          const days = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
          const pad = (number, width) => String(number).padStart(width, "0");
          const escape = String.fromCharCode(92);
          let result = "";
          for (let index = 0; index < format.length;) {
            const character = format[index];
            if (character === "'" || character === '"') {
              const end = format.indexOf(character, index + 1);
              if (end < 0) throw new RangeError("Cannot find a matching quote character.");
              result += format.slice(index + 1, end);
              index = end + 1;
              continue;
            }
            if (character === escape) {
              result += format[index + 1] ?? "";
              index += 2;
              continue;
            }
            if (character === "%") {
              index++;
              continue;
            }
            let count = 1;
            while (format[index + count] === character) count++;
            switch (character) {
              case "y": result += count <= 2 ? pad(year % 100, count) : pad(year, count); break;
              case "M": result += count === 1 ? month + 1 : count === 2 ? pad(month + 1, 2) : count === 3 ? months[month].slice(0, 3) : months[month]; break;
              case "d": result += count === 1 ? day : count === 2 ? pad(day, 2) : count === 3 ? days[weekday].slice(0, 3) : days[weekday]; break;
              case "H": result += pad(hour, Math.min(count, 2)); break;
              case "h": result += pad(hour % 12 || 12, Math.min(count, 2)); break;
              case "m": result += pad(minute, Math.min(count, 2)); break;
              case "s": result += pad(second, Math.min(count, 2)); break;
              case "f": case "F": {
                if (count > 7) throw new RangeError("Input string was not in a correct format.");
                let fraction = (pad(millisecond, 3) + "0000").slice(0, count);
                if (character === "F") {
                  fraction = fraction.replace(/0+$/, "");
                  if (fraction.length === 0 && result.endsWith(".")) result = result.slice(0, -1);
                }
                result += fraction;
                break;
              }
              case "t": result += (hour < 12 ? "AM" : "PM").slice(0, count === 1 ? 1 : 2); break;
              case "z": result += count === 1 ? "+0" : count === 2 ? "+00" : "+00:00"; break;
              case "K": result += offset ? "+00:00" : "Z"; break;
              default: result += character.repeat(count);
            }
            index += count;
          }
          return result;
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

