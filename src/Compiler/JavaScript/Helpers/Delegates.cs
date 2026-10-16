internal static partial class HelperSource
{
    private static string Delegates(Func<string, string> name) => $$"""
        const {{name("delegateList")}} = Symbol("delegate invocation list");
        const {{name("delegateIdentity")}} = Symbol("delegate method identity");
        const {{name("delegateType")}} = Symbol("delegate type");
        function {{name("delegateTyped")}}(value, type) {
          if (value != null && value[{{name("delegateType")}}] == null)
            Object.defineProperty(value, {{name("delegateType")}}, { value: type });
          return value;
        }
        function {{name("delegateEntries")}}(value) {
          return value == null ? [] : value[{{name("delegateList")}}] ?? [{ target: null, method: value, invoke: value }];
        }
        function {{name("delegateFromEntries")}}(entries) {
          if (entries.length === 0) return null;
          const callable = (...args) => {
            let result;
            for (const entry of entries) result = entry.invoke(...args);
            return result;
          };
          Object.defineProperty(callable, {{name("delegateList")}}, { value: entries });
          return callable;
        }
        function {{name("delegateCreate")}}(target, method, invoke) {
          method = method[{{name("delegateIdentity")}}]?.(target) ?? method;
          return {{name("delegateFromEntries")}}([{ target, method, invoke }]);
        }
        function {{name("delegateCopyArguments")}}(value, copy) {
          return {{name("delegateFromEntries")}}({{name("delegateEntries")}}(value).map(entry => ({
            ...entry, invoke: (...args) => entry.invoke(...copy(args))
          })));
        }
        function {{name("delegateEntryEquals")}}(a, b) { return a.target === b.target && a.method === b.method; }
        function {{name("delegateEquals")}}(a, b) {
          if (a != null && b != null && a[{{name("delegateType")}}] !== b[{{name("delegateType")}}]) return false;
          const left = {{name("delegateEntries")}}(a), right = {{name("delegateEntries")}}(b);
          return left.length === right.length && left.every((entry, i) => {{name("delegateEntryEquals")}}(entry, right[i]));
        }
        function {{name("delegateCombine")}}(a, b) {
          if (a == null) return b;
          if (b == null) return a;
          if (a[{{name("delegateType")}}] !== b[{{name("delegateType")}}]) throw new ({{name("exceptionType")}}("System.ArgumentException"))("Delegates must be of the same type.");
          return {{name("delegateTyped")}}({{name("delegateFromEntries")}}([...{{name("delegateEntries")}}(a), ...{{name("delegateEntries")}}(b)]), a[{{name("delegateType")}}]);
        }
        function {{name("delegateRemove")}}(a, b) {
          if (a == null || b == null) return a;
          if (a[{{name("delegateType")}}] !== b[{{name("delegateType")}}]) throw new ({{name("exceptionType")}}("System.ArgumentException"))("Delegates must be of the same type.");
          const left = {{name("delegateEntries")}}(a), right = {{name("delegateEntries")}}(b);
          for (let i = left.length - right.length; i >= 0; i--)
            if (right.every((entry, j) => {{name("delegateEntryEquals")}}(entry, left[i + j])))
              return {{name("delegateTyped")}}({{name("delegateFromEntries")}}([...left.slice(0, i), ...left.slice(i + right.length)]), a[{{name("delegateType")}}]);
          return a;
        }

        """;
}
