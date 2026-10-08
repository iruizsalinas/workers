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
          if (kind === 7 && (mode === 2 && value instanceof Date || typeof value === "string"
            && /^[0-9]{4}-[0-9]{2}-[0-9]{2}(?:T[0-9]{2}:[0-9]{2}(?::[0-9]{2}(?:\.[0-9]+)?)?(?:Z|[+-][0-9]{2}:[0-9]{2})?)?$/.test(value))) {
            const date = new Date(value);
            if (!Number.isNaN(date.getTime())) return date;
          }
          if (kind === 8 && typeof value === "string"
            && /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(value))
            return value.toLowerCase();
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

    // Formats values the way System.Text.Json writes them with its default options.
    private static string JsonSerializeClr(Func<string, string> name) => $$"""
        function {{name("jsonSerializeClr")}}(value) {
          const escape = code => "\\" + "u" + code.toString(16).toUpperCase().padStart(4, "0");
          const text = token => token.replace(/\\u[0-9a-f]{4}|\\.|[^ -~]|[<>&'+`]/g, match => {
            if (match.length === 6) return escape(Number.parseInt(match.slice(2), 16));
            if (match.length === 2) return match === "\\\"" ? escape(34) : match;
            return escape(match.charCodeAt(0));
          });
          if (typeof JSON.rawJSON !== "function") return JSON.stringify(value);
          return JSON.stringify(value, (key, item) => typeof item === "string"
            ? JSON.rawJSON('"' + text(JSON.stringify(item).slice(1, -1)) + '"')
            : item);
        }
        function {{name("jsonClrNumber")}}(value, single) {
          return typeof value === "number" && Number.isFinite(value) && typeof JSON.rawJSON === "function"
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
