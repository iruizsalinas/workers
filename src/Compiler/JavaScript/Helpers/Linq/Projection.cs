internal static partial class HelperSource
{
    private static string LinqWhere(Func<string, string> name) => $$"""
        function {{name("linqWhere")}}(source, predicate) {
          if (source == null || predicate == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let index = 0;
            for (const value of {{name("linqValues")}}(source)) if (predicate(value, index++)) yield value;
          }
          };
        }

        """;

    private static string LinqSelect(Func<string, string> name) => $$"""
        function {{name("linqSelect")}}(source, selector) {
          if (source == null || selector == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let index = 0;
            for (const value of {{name("linqValues")}}(source)) yield selector(value, index++);
          }
          };
        }

        """;

    private static string LinqSelectMany(Func<string, string> name) => $$"""
        function {{name("linqSelectMany")}}(source, collectionSelector, resultSelector, hasResultSelector) {
          if (source == null || collectionSelector == null || (hasResultSelector && resultSelector == null))
            throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let index = 0;
            for (const outer of {{name("linqValues")}}(source)) {
              const inner = collectionSelector(outer, index++);
              for (const value of {{name("linqValues")}}(inner))
                yield hasResultSelector ? resultSelector(outer, value) : value;
            }
          }
          };
        }

        """;

    private static string LinqAppend(Func<string, string> name) => $$"""
        function {{name("linqAppend")}}(source, value) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            yield* {{name("linqValues")}}(source); yield value;
          }
          };
        }

        """;

    private static string LinqPrepend(Func<string, string> name) => $$"""
        function {{name("linqPrepend")}}(source, value) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            yield value; yield* {{name("linqValues")}}(source);
          }
          };
        }

        """;

    private static string LinqSkip(Func<string, string> name) => $$"""
        function {{name("linqSkip")}}(source, count) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            let remaining = Math.max(0, count);
            for (const value of {{name("linqValues")}}(source)) if (remaining > 0) remaining--; else yield value;
          }
          };
        }

        """;

    private static string LinqTake(Func<string, string> name) => $$"""
        function {{name("linqTake")}}(source, count) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            let remaining = count;
            if (remaining <= 0) return;
            for (const value of {{name("linqValues")}}(source)) {
              yield value;
              if (--remaining === 0) return;
            }
          }
          };
        }

        """;

    private static string LinqSkipWhile(Func<string, string> name) => $$"""
        function {{name("linqSkipWhile")}}(source, predicate) {
          if (source == null || predicate == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let yielding = false, index = 0;
            for (const value of {{name("linqValues")}}(source)) {
              if (!yielding && !predicate(value, index++)) yielding = true;
              if (yielding) yield value;
            }
          }
          };
        }

        """;

    private static string LinqTakeWhile(Func<string, string> name) => $$"""
        function {{name("linqTakeWhile")}}(source, predicate) {
          if (source == null || predicate == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            let index = 0;
            for (const value of {{name("linqValues")}}(source)) {
              if (!predicate(value, index++)) return;
              yield value;
            }
          }
          };
        }

        """;

    private static string LinqConcat(Func<string, string> name) => $$"""
        function {{name("linqConcat")}}(first, second) {
          if (first == null || second == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            yield* {{name("linqValues")}}(first); yield* {{name("linqValues")}}(second);
          }
          };
        }

        """;

}
