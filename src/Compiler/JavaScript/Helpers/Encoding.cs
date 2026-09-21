internal static partial class HelperSource
{
    private static string Base64(Func<string, string> name) => $$"""
        function {{name("base64Encode")}}(bytes) {
          let binary = "";
          for (const byte of bytes) binary += String.fromCharCode(byte);
          return btoa(binary);
        }
        function {{name("base64Decode")}}(value) {
          return Uint8Array.from(atob(value), character => character.charCodeAt(0));
        }

        """;

    private static string RpcArguments(Func<string, string> name) => $$"""
        function {{name("rpcArguments")}}(value) {
          return value ?? [];
        }

        """;

    private static string HexDecode(Func<string, string> name) => $$"""
        function {{name("hexDecode")}}(value) {
          if (value.length % 2 !== 0 || !/^[0-9a-f]*$/i.test(value)) throw new TypeError("Invalid hexadecimal value.");
          const bytes = new Uint8Array(value.length / 2);
          for (let index = 0; index < bytes.length; index++)
            bytes[index] = Number.parseInt(value.slice(index * 2, index * 2 + 2), 16);
          return bytes;
        }

        """;

    private static string EscapeDataString(Func<string, string> name) => $$"""
        function {{name("escapeDataString")}}(value) {
          return encodeURIComponent(value).replace(/[!'()*]/g, character =>
            `%${character.charCodeAt(0).toString(16).toUpperCase()}`);
        }

        """;

    private static string JsonElementToString(Func<string, string> name) => $$"""
        function {{name("jsonElementToString")}}(value) {
          if (value == null) return "";
          if (typeof value === "string") return value;
          if (typeof value === "boolean") return value ? "True" : "False";
          return JSON.stringify(value);
        }

        """;
}
