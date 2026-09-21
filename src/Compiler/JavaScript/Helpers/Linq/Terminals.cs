internal static partial class HelperSource
{
    private static string LinqJoin(Func<string, string> name) => $$"""
        function {{name("linqJoin")}}(outer, inner, outerKey, innerKey, selector, grouped) {
          if (outer == null || inner == null || outerKey == null || innerKey == null || selector == null)
            throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            const lookup = new Map();
            for (const value of {{name("linqValues")}}(inner)) {
              const key = innerKey(value);
              let values = lookup.get(key);
              if (values === undefined) lookup.set(key, values = []);
              values.push(value);
            }
            for (const value of {{name("linqValues")}}(outer)) {
              const matches = lookup.get(outerKey(value)) ?? [];
              if (grouped) yield selector(value, matches);
              else for (const match of matches) yield selector(value, match);
            }
          }
          };
        }

        """;

    private static string LinqElementAt(Func<string, string> name) => $$"""
        function {{name("linqElementAt")}}(source, index, defaultValue, orDefault) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          if (index >= 0) {
            let current = 0;
            for (const value of {{name("linqValues")}}(source))
              if (current++ === index) return value;
          }
          if (orDefault) return defaultValue;
          throw new RangeError("Index was outside the bounds of the sequence.");
        }

        """;

    private static string LinqFirst(Func<string, string> name) => $$"""
        function {{name("linqFirst")}}(source, predicate, hasPredicate, defaultValue, orDefault) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let index = 0;
          for (const value of {{name("linqValues")}}(source))
            if (predicate === null || predicate(value, index++)) return value;
          if (orDefault) return defaultValue;
          throw new RangeError("Sequence contains no matching element.");
        }

        """;

    private static string LinqLast(Func<string, string> name) => $$"""
        function {{name("linqLast")}}(source, predicate, hasPredicate, defaultValue, orDefault) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let found = false, result = defaultValue, index = 0;
          for (const value of {{name("linqValues")}}(source))
            if (predicate === null || predicate(value, index++)) {
            found = true; result = value;
          }
          if (found || orDefault) return result;
          throw new RangeError("Sequence contains no matching element.");
        }

        """;

    private static string LinqSingle(Func<string, string> name) => $$"""
        function {{name("linqSingle")}}(source, predicate, hasPredicate, defaultValue, orDefault) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let found = false, result = defaultValue, index = 0;
          for (const value of {{name("linqValues")}}(source))
            if (predicate === null || predicate(value, index++)) {
            if (found) throw new RangeError("Sequence contains more than one matching element.");
            found = true; result = value;
          }
          if (found || orDefault) return result;
          throw new RangeError("Sequence contains no matching element.");
        }

        """;

    private static string LinqToArray(Func<string, string> name) => $$"""
        function {{name("linqToArray")}}(source) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return Array.from({{name("linqValues")}}(source));
        }

        """;

    private static string LinqValues(Func<string, string> name) => $$"""
        function* {{name("linqValues")}}(source) {
          if (typeof source === "string") {
            for (let index = 0; index < source.length; index++) yield source[index];
            return;
          }
          if (source[Symbol.iterator] != null) {
            yield* source;
            return;
          }
          yield* Object.entries(source);
        }

        """;
}
