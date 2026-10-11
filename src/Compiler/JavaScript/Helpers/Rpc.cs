internal static partial class HelperSource
{
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

}
