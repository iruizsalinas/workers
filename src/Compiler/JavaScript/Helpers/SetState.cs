internal static partial class HelperSource
{
    private static string SetState(Func<string, string> name) => $$"""
        const {{name("setVersions")}} = new WeakMap();
        function {{name("setCreate")}}(set) {
          if ({{name("setVersions")}}.has(set)) return set;
          {{name("setVersions")}}.set(set, 0);
          Object.defineProperty(set, "toJSON", { value() { return Array.from(this); } });
          Object.defineProperty(set, "add", { value(value) {
            if (!Set.prototype.has.call(this, value))
              {{name("setVersions")}}.set(this, {{name("setVersions")}}.get(this) + 1);
            Set.prototype.add.call(this, value);
            return this;
          } });
          Object.defineProperty(set, Symbol.iterator, { value: function* () {
            const version = {{name("setVersions")}}.get(this);
            const iterator = Set.prototype.values.call(this);
            while (true) {
              if ({{name("setVersions")}}.get(this) !== version)
                throw new ({{name("exceptionType")}}("System.InvalidOperationException"))("Collection was modified; enumeration operation may not execute.");
              const item = iterator.next();
              if (item.done) return;
              yield item.value;
            }
          } });
          return set;
        }

        """;
}
