internal static partial class HelperSource
{
    private static string LinqAny(Func<string, string> name) => $$"""
        function {{name("linqAny")}}(source, predicate, hasPredicate) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let index = 0;
          for (const value of {{name("linqValues")}}(source))
            if (predicate === null || predicate(value, index++)) return true;
          return false;
        }

        """;

    private static string LinqAll(Func<string, string> name) => $$"""
        function {{name("linqAll")}}(source, predicate) {
          if (source == null || predicate == null) throw new TypeError("LINQ argument cannot be null.");
          let index = 0;
          for (const value of {{name("linqValues")}}(source)) if (!predicate(value, index++)) return false;
          return true;
        }

        """;

    private static string LinqCount(Func<string, string> name) => $$"""
        function {{name("linqCount")}}(source, predicate, hasPredicate) {
          if (source == null || (hasPredicate && predicate == null))
            throw new TypeError("LINQ argument cannot be null.");
          let count = 0, index = 0;
          for (const value of {{name("linqValues")}}(source))
            if (predicate === null || predicate(value, index++)) {
            if (count === 2147483647) throw new RangeError("Enumerable count overflow.");
            count++;
          }
          return count;
        }

        """;

    private static string LinqContains(Func<string, string> name) => $$"""
        function {{name("linqContains")}}(source, target) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          for (const value of {{name("linqValues")}}(source))
            if (value === target || (value !== value && target !== target)) return true;
          return false;
        }

        """;

    private static string LinqDistinct(Func<string, string> name) => $$"""
        function {{name("linqDistinct")}}(source) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            const seen = new Set();
            for (const value of {{name("linqValues")}}(source))
              if (!seen.has(value)) { seen.add(value); yield value; }
          }
          };
        }

        """;

    private static string LinqDistinctBy(Func<string, string> name) => $$"""
        function {{name("linqDistinctBy")}}(source, keySelector) {
          if (source == null || keySelector == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            const seen = new Set();
            for (const value of {{name("linqValues")}}(source)) {
              const key = keySelector(value);
              if (!seen.has(key)) { seen.add(key); yield value; }
            }
          }
          };
        }

        """;

    private static string LinqSequenceEqual(Func<string, string> name) => $$"""
        function {{name("linqSequenceEqual")}}(first, second) {
          if (first == null || second == null) throw new TypeError("LINQ source cannot be null.");
          const left = {{name("linqValues")}}(first), right = {{name("linqValues")}}(second);
          try {
            while (true) {
              const a = left.next(), b = right.next();
              if (a.done || b.done) return a.done === b.done;
              if (!(a.value === b.value || (a.value !== a.value && b.value !== b.value))) return false;
            }
          } finally {
            left.return?.(); right.return?.();
          }
        }

        """;

}
