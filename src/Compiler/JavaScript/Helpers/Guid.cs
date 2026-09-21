internal static partial class HelperSource
{
    private static string GuidParse(Func<string, string> name) => $$"""
        function {{name("guidParse")}}(input) {
          if (input == null) throw new TypeError("Value cannot be null.");
          const value = input.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/gu, "");
          const compact = value.match(/^[{(]?([0-9a-f]{8})-([0-9a-f]{4})-([0-9a-f]{4})-([0-9a-f]{4})-([0-9a-f]{12})[})]?$/i);
          if (compact && ((value[0] === "{" && value.at(-1) === "}")
              || (value[0] === "(" && value.at(-1) === ")")
              || (value[0] !== "{" && value[0] !== "(" && value.at(-1) !== "}" && value.at(-1) !== ")")))
            return compact.slice(1).join("").replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/, "$1-$2-$3-$4-$5").toLowerCase();
          if (/^[0-9a-f]{32}$/i.test(value))
            return value.replace(/^(.{8})(.{4})(.{4})(.{4})(.{12})$/, "$1-$2-$3-$4-$5").toLowerCase();
          const hex = value.match(/^\{\s*0x([0-9a-f]{8})\s*,\s*0x([0-9a-f]{4})\s*,\s*0x([0-9a-f]{4})\s*,\s*\{\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*,\s*0x([0-9a-f]{2})\s*\}\s*\}$/i);
          if (hex) return (hex[1] + "-" + hex[2] + "-" + hex[3] + "-" + hex[4] + hex[5]
            + "-" + hex.slice(6).join("")).toLowerCase();
          throw new TypeError("Invalid Guid value.");
        }

        """;

    private static string GuidFormat(Func<string, string> name) => $$"""
        function {{name("guidFormat")}}(value, format) {
          const code = format == null || format === "" ? "d" : format.toLowerCase();
          if (code === "d") return value;
          const compact = value.replaceAll("-", "");
          if (code === "n") return compact;
          if (code === "b") return "{" + value + "}";
          if (code === "p") return "(" + value + ")";
          throw new TypeError("Unsupported Guid format.");
        }

        """;
}

