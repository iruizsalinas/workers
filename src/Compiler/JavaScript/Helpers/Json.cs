internal static partial class HelperSource
{
    private static string JsonDeserializeValue(Func<string, string> name) => $$"""
        function {{name("jsonDeserializeValue")}}(value, kind, minimum = 0, maximum = 0) {
          if (kind === 0 && (value === null || typeof value === "string")) return value;
          if (kind === 1 && typeof value === "boolean") return value;
          if (kind === 2 && Number.isInteger(value) && value >= minimum && value <= maximum) return value;
          if ((kind === 3 || kind === 4) && typeof value === "number") return value;
          if (kind === 5 && typeof value === "string" && value.length === 1) return value;
          if (kind === 6 && value === null) return null;
          if (kind === 6 && typeof value === "string") {
            const base64 = value.replace(/[ \t\r\n]/g, "");
            if (!/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(base64))
              throw new TypeError("Invalid JSON base64 value.");
            return Uint8Array.from(atob(base64), character => character.charCodeAt(0));
          }
          throw new TypeError("JSON value has an incompatible type or range.");
        }
        function {{name("jsonDeserializeArray")}}(value, convert) {
          if (value === null) return null;
          if (!Array.isArray(value)) throw new TypeError("Expected a JSON array.");
          return value.map(convert);
        }
        function {{name("jsonDeserializeNullable")}}(value, convert) {
          return value === null ? null : convert(value);
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
