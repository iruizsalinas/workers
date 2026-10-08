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
          return (value ?? []).map({{name("rpcValue")}});
        }
        // Workers RPC rejects instances of user-defined classes, so compiled C# class and record
        // instances cross as plain objects with the same fields. Platform values pass through.
        const {{name("rpcClasses")}} = new WeakMap();
        function {{name("rpcValue")}}(value) {
          if (value === null || typeof value !== "object") return value;
          if (Array.isArray(value)) return value.map({{name("rpcValue")}});
          if (value instanceof Promise) return value.then({{name("rpcValue")}});
          if (value instanceof Set) return new Set(Array.from(value, {{name("rpcValue")}}));
          if (value instanceof Map)
            return new Map(Array.from(value, ([key, item]) => [{{name("rpcValue")}}(key), {{name("rpcValue")}}(item)]));
          const prototype = Object.getPrototypeOf(value);
          let compiled = prototype === Object.prototype || prototype === null;
          if (!compiled && !(value instanceof Error) && typeof prototype.constructor === "function") {
            compiled = {{name("rpcClasses")}}.get(prototype.constructor);
            if (compiled === undefined) {
              compiled = Function.prototype.toString.call(prototype.constructor).startsWith("class");
              {{name("rpcClasses")}}.set(prototype.constructor, compiled);
            }
          }
          if (!compiled) return value;
          const result = prototype === null ? Object.create(null) : {};
          for (const key of Object.keys(value))
            Object.defineProperty(result, key, { value: {{name("rpcValue")}}(value[key]), enumerable: true, writable: true, configurable: true });
          return result;
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
