internal static partial class HelperSource
{
    // Modes: 0 follows System.Text.Json defaults, 1 follows JsonSerializerDefaults.Web (numbers may be
    // quoted), 2 reads storage rows where SQLite and structured-clone values are already typed, and 3
    // binds query strings where every value is text and names map to all of their values.
    private static string JsonDeserializeValue(Func<string, string> name) => $$"""
        function {{name("jsonDeserializeValue")}}(value, mode, kind, minimum = 0, maximum = 0) {
          if (mode === 3 && Array.isArray(value)) value = value.length === 0 ? null : value[0];
          if (mode === 3 && kind === 1 && typeof value === "string" && /^(?:true|false)$/i.test(value))
            return value.toLowerCase() === "true";
          if ((mode === 1 || mode === 3) && kind >= 2 && kind <= 4 && typeof value === "string"
            && /^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$/.test(value)) value = Number(value);
          if (mode === 2 && kind === 1 && (value === 0 || value === 1)) return value === 1;
          // Database rows carry 64-bit and exact numerics as strings or bigints, and binary as buffers
          // (or, from D1, as arrays of byte values).
          if (mode === 2 && kind >= 2 && kind <= 4 && (typeof value === "bigint" || typeof value === "string"
            && /^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$/.test(value))) value = Number(value);
          if (mode === 2 && kind === 6 && (ArrayBuffer.isView(value) || value instanceof ArrayBuffer))
            return ArrayBuffer.isView(value)
              ? new Uint8Array(value.buffer.slice(value.byteOffset, value.byteOffset + value.byteLength))
              : new Uint8Array(value.slice(0));
          if (mode === 2 && kind === 6 && Array.isArray(value)
            && value.every(item => Number.isInteger(item) && item >= 0 && item <= 255))
            return Uint8Array.from(value);
          if (kind === 0 && (value === null || typeof value === "string")) return value;
          if (kind === 1 && typeof value === "boolean") return value;
          if (kind === 2 && Number.isInteger(value) && value >= minimum && value <= maximum) return value;
          if ((kind === 3 || kind === 4) && typeof value === "number") return kind === 3 ? Math.fround(value) : value;
          if (kind === 5 && typeof value === "string" && value.length === 1) return value;
          if (kind === 6 && value === null) return null;
          if (kind === 6 && typeof value === "string") {
            const base64 = value.replace(/[ \t\r\n]/g, "");
            if (!/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(base64))
              throw new TypeError("Invalid JSON base64 value.");
            return Uint8Array.from(atob(base64), character => character.charCodeAt(0));
          }
          if (kind === 7) {
            if (mode === 2 && value instanceof Date && !Number.isNaN(value.getTime())) return new Date(value);
            const parts = typeof value === "string" && /^([0-9]{4})-([0-9]{2})-([0-9]{2})(?:T([0-9]{2}):([0-9]{2})(?::([0-9]{2})(?:\.([0-9]{0,16}))?)?(?:Z|[+-]([0-9]{2}):([0-9]{2}))?)?$/.exec(value);
            if (parts) {
              const year = Number(parts[1]), month = Number(parts[2]), day = Number(parts[3]);
              const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
              const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
              const offsetHour = Number(parts[8] ?? 0), offsetMinute = Number(parts[9] ?? 0);
              if (year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= days[month - 1]
                && Number(parts[4] ?? 0) <= 23 && Number(parts[5] ?? 0) <= 59 && Number(parts[6] ?? 0) <= 59
                && offsetHour <= 14 && offsetMinute <= 59 && (offsetHour < 14 || offsetMinute === 0)) {
                const date = new Date(parts[7] === "" ? value.replace(/\.(?=Z|[+-]|$)/, "") : value);
                if (!Number.isNaN(date.getTime()) && date.getTime() >= -62135596800000 && date.getTime() <= 253402300799999) return date;
              }
            }
          }
          if (kind === 8 && typeof value === "string"
            && /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(value))
            return value.toLowerCase();
          // DateOnly reads exactly yyyy-MM-dd. Database drivers may return DATE columns as midnight UTC dates.
          if (kind === 9) {
            if (mode === 2 && value instanceof Date && Number.isFinite(value.getTime())
              && (value.getTime() % 86400000 + 86400000) % 86400000 === 0 && value.getUTCFullYear() >= 1 && value.getUTCFullYear() <= 9999)
              value = String(value.getUTCFullYear()).padStart(4, "0") + "-" + String(value.getUTCMonth() + 1).padStart(2, "0")
                + "-" + String(value.getUTCDate()).padStart(2, "0");
            const parts = typeof value === "string" && /^([0-9]{4})-([0-9]{2})-([0-9]{2})$/.exec(value);
            if (parts) {
              const year = Number(parts[1]), month = Number(parts[2]), day = Number(parts[3]);
              const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
              if (year >= 1 && month >= 1 && month <= 12 && day >= 1
                && day <= [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1]) return value;
            }
          }
          // TimeOnly reads h:m[:s[.f]] with up to seven fraction digits, like the System.Text.Json converter.
          if (kind === 10) {
            const parts = typeof value === "string" && value.length >= 3 && value.length <= 16
              && /^([0-9]+):([0-9]+)(?::([0-9]+)(?:\.([0-9]{1,7}))?)?$/.exec(value);
            if (parts) {
              const hour = Number(parts[1]), minute = Number(parts[2]), second = Number(parts[3] ?? 0);
              const fraction = Number((parts[4] ?? "").padEnd(7, "0"));
              if (hour <= 23 && minute <= 59 && second <= 59) {
                const pad = number => String(number).padStart(2, "0");
                return pad(hour) + ":" + pad(minute) + ":" + pad(second) + (fraction ? "." + String(fraction).padStart(7, "0") : "");
              }
            }
          }
          throw new TypeError("JSON value has an incompatible type or range.");
        }
        function {{name("jsonDeserializeArray")}}(value, convert, mode = 0) {
          if (value === null) return null;
          if (mode === 3 && !Array.isArray(value)) value = [value];
          if (!Array.isArray(value)) throw new TypeError("Expected a JSON array.");
          return value.map(convert);
        }
        function {{name("jsonDeserializeDictionary")}}(value, convert) {
          if (value === null) return null;
          if (typeof value !== "object" || Array.isArray(value)) throw new TypeError("Expected a JSON object.");
          const result = Object.create(null);
          for (const key of Object.keys(value)) result[key] = convert(value[key]);
          return result;
        }
        function {{name("jsonDeserializeNullable")}}(value, convert) {
          return value === null ? null : convert(value);
        }
        function {{name("jsonFold")}}(value) {
          const result = Object.create(null);
          for (const key of Object.keys(value)) result[key.toLowerCase()] = value[key];
          return result;
        }

        """;

    // Formats values the way System.Text.Json writes them with its default options. It follows
    // JSON.stringify (toJSON, skipped undefined members, raw JSON) but escapes property names and string
    // values like JavaScriptEncoder.Default, which also replaces lone surrogates. Raw JSON, such as the
    // projected dates, is written as is.
    private static string JsonSerializeClr(Func<string, string> name) => $$"""
        function {{name("jsonClrString")}}(value) {
          if (!/[^ -~]|["\\<>&'+`]/.test(value)) return '"' + value + '"';
          const escape = code => "\\u" + code.toString(16).toUpperCase().padStart(4, "0");
          const short = { 8: "\\b", 9: "\\t", 10: "\\n", 12: "\\f", 13: "\\r", 92: "\\\\" };
          let result = '"';
          for (let index = 0; index < value.length; index++) {
            const code = value.charCodeAt(index);
            if (code >= 0xD800 && code <= 0xDBFF && index + 1 < value.length
                && value.charCodeAt(index + 1) >= 0xDC00 && value.charCodeAt(index + 1) <= 0xDFFF) {
              result += escape(code) + escape(value.charCodeAt(++index));
            } else if (code >= 0xD800 && code <= 0xDFFF) result += escape(0xFFFD);
            else if (Object.hasOwn(short, code)) result += short[code];
            else if (code < 0x20 || code > 0x7E || "\"<>&'+`".includes(value[index])) result += escape(code);
            else result += value[index];
          }
          return result + '"';
        }
        function {{name("jsonSerializeClr")}}(value, indented = false) {
          const write = (holder, key, item, indent) => {
            if (item !== null && (typeof item === "object" || typeof item === "bigint") && typeof item.toJSON === "function")
              item = item.toJSON(key);
            if (item === null) return "null";
            if (typeof JSON.isRawJSON === "function" && JSON.isRawJSON(item)) return item.rawJSON;
            switch (typeof item) {
              case "string": return {{name("jsonClrString")}}(item);
              case "boolean": return item ? "true" : "false";
              case "number":
                if (!Number.isFinite(item)) throw new TypeError("Nonfinite numbers cannot be written as JSON.");
                return String(item);
              case "object": break;
              case "bigint": throw new TypeError("Do not know how to serialize a BigInt.");
              default: return undefined;
            }
            if (item instanceof Number || item instanceof String || item instanceof Boolean)
              return write(holder, key, item.valueOf(), indent);
            const inner = indented ? indent + "  " : "";
            const open = indented ? "\n" + inner : "", close = indented ? "\n" + indent : "";
            const separator = indented ? ",\n" + inner : ",";
            if (Array.isArray(item)) {
              if (item.length === 0) return "[]";
              const parts = Array.from(item, (element, index) => write(item, String(index), element, inner) ?? "null");
              return "[" + open + parts.join(separator) + close + "]";
            }
            const parts = [];
            for (const property of Object.keys(item)) {
              const written = write(item, property, item[property], inner);
              if (written !== undefined) parts.push({{name("jsonClrString")}}(property) + (indented ? ": " : ":") + written);
            }
            return parts.length === 0 ? "{}" : "{" + open + parts.join(separator) + close + "}";
          };
          return write({ "": value }, "", value, "");
        }
        function {{name("jsonClrNumber")}}(value, single) {
          if (typeof value === "number" && !Number.isFinite(value))
            throw new TypeError("Nonfinite numbers cannot be written as JSON.");
          return typeof value === "number" && typeof JSON.rawJSON === "function"
            ? JSON.rawJSON({{name("numberText")}}(value, single))
            : value;
        }
        function {{name("jsonClrDate")}}(value) {
          if (value == null) return null;
          const text = value.toISOString();
          const fraction = text.slice(20, 23).replace(/0+$/, "");
          const formatted = text.slice(0, 19) + (fraction ? "." + fraction : "") + "+00:00";
          return typeof JSON.rawJSON === "function" ? JSON.rawJSON('"' + formatted + '"') : formatted;
        }
        function {{name("jsonClrTimeSpan")}}(value) {
          if (value == null) return null;
          const sign = value < 0 ? "-" : "";
          let ticks = Math.round(Math.abs(value) * 10000);
          const days = Math.floor(ticks / 864000000000);
          ticks -= days * 864000000000;
          const hours = Math.floor(ticks / 36000000000);
          ticks -= hours * 36000000000;
          const minutes = Math.floor(ticks / 600000000);
          ticks -= minutes * 600000000;
          const seconds = Math.floor(ticks / 10000000);
          ticks -= seconds * 10000000;
          const pad = number => String(number).padStart(2, "0");
          return sign + (days ? days + "." : "") + pad(hours) + ":" + pad(minutes) + ":" + pad(seconds)
            + (ticks ? "." + String(ticks).padStart(7, "0") : "");
        }
        function {{name("jsonClrBytes")}}(value) {
          if (value == null) return null;
          let text = "";
          for (const byte of value) text += String.fromCharCode(byte);
          return btoa(text);
        }

        """;

    private static string JsonElementValueKind(Func<string, string> name) => $$"""
        function {{name("jsonElementValueKind")}}(value) {
          if (value === null) return 7;
          if (Array.isArray(value)) return 2;
          switch (typeof value) {
            case "object": return 1;
            case "string": return 3;
            case "number": return 4;
            case "boolean": return value ? 5 : 6;
            default: return 0;
          }
        }

        """;

    private static string JsonElementGetValue(Func<string, string> name) => $$"""
        function {{name("jsonElementGetValue")}}(value, operation) {
          if (operation === 0) {
            if (value === null || typeof value === "string") return value;
          } else if (operation === 1) {
            if (typeof value === "boolean") return value;
          } else if (operation === 2) {
            if (Number.isInteger(value) && value >= -2147483648 && value <= 2147483647) return value | 0;
          } else if (operation === 3) {
            if (typeof value === "number") return value;
          } else if (operation === 4) {
            if (Array.isArray(value)) return value.length;
          } else if (operation === 5) {
            if (Array.isArray(value)) return value;
          }
          throw new TypeError("JSON value has an incompatible kind.");
        }

        """;

    private static string JsonElementGetProperty(Func<string, string> name) => $$"""
        function {{name("jsonElementGetProperty")}}(value, property) {
          if (value === null || Array.isArray(value) || typeof value !== "object"
              || !Object.prototype.hasOwnProperty.call(value, property))
            throw new TypeError("JSON property was not found.");
          return value[property];
        }

        """;

    private static string JsonElementGetIndex(Func<string, string> name) => $$"""
        function {{name("jsonElementGetIndex")}}(value, index) {
          if (!Array.isArray(value)) throw new TypeError("JSON value is not an array.");
          if (!Number.isInteger(index) || index < 0 || index >= value.length)
            throw new RangeError("JSON array index is out of range.");
          return value[index];
        }

        """;
}
