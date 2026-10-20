internal static partial class HelperSource
{
    private static string RpcArguments(Func<string, string> name) => $$"""
        function {{name("rpcArguments")}}(value) {
          const seen = new WeakMap();
          return (value ?? []).map(item => {{name("rpcValue")}}(item, seen));
        }
        // Workers RPC rejects instances of user-defined classes, so compiled C# class and record
        // instances cross as plain objects with the same fields. Platform values pass through.
        const {{name("rpcClasses")}} = new WeakSet();
        function {{name("rpcValue")}}(value, seen = new WeakMap()) {
          if (value === null || typeof value !== "object") return value;
          if (seen.has(value)) return seen.get(value);
          if ({{name("keyCollectionKinds")}}.has(value)) {
            const set = {{name("keyCollectionKinds")}}.get(value);
            const result = set ? new Set() : new Map();
            seen.set(value, result);
            for (const item of value) {
              if (set) result.add({{name("rpcValue")}}(item, seen));
              else result.set({{name("rpcValue")}}(item[0], seen), {{name("rpcValue")}}(item[1], seen));
            }
            return result;
          }
          if (Array.isArray(value)) {
            const result = new Array(value.length);
            seen.set(value, result);
            value.forEach((item, index) => result[index] = {{name("rpcValue")}}(item, seen));
            return result;
          }
          if (value instanceof Promise) {
            const result = value.then(item => {{name("rpcValue")}}(item, seen));
            seen.set(value, result);
            return result;
          }
          if (value instanceof Set) {
            const result = new Set();
            seen.set(value, result);
            for (const item of value) result.add({{name("rpcValue")}}(item, seen));
            return result;
          }
          if (value instanceof Map) {
            const result = new Map();
            seen.set(value, result);
            for (const [key, item] of value) result.set({{name("rpcValue")}}(key, seen), {{name("rpcValue")}}(item, seen));
            return result;
          }
          const prototype = Object.getPrototypeOf(value);
          const compiled = prototype === Object.prototype || prototype === null
            || !(value instanceof Error) && {{name("rpcClasses")}}.has(prototype);
          if (!compiled) return value;
          const result = prototype === null ? Object.create(null) : {};
          seen.set(value, result);
          for (const key of Object.keys(value))
            Object.defineProperty(result, key, { value: {{name("rpcValue")}}(value[key], seen), enumerable: true, writable: true, configurable: true });
          return result;
        }

        """;

}
