internal static partial class HelperSource
{
    private static string LinqReverse(Func<string, string> name) => $$"""
        function {{name("linqReverse")}}(source) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            const values = Array.from({{name("linqValues")}}(source));
            for (let index = values.length - 1; index >= 0; index--) yield values[index];
          }
          };
        }

        """;

    private static string LinqDefaultIfEmpty(Func<string, string> name) => $$"""
        function {{name("linqDefaultIfEmpty")}}(source, defaultValue) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          return { *[Symbol.iterator]() {
            let found = false;
            for (const value of {{name("linqValues")}}(source)) { found = true; yield value; }
            if (!found) yield defaultValue;
          }
          };
        }

        """;

    private static string LinqChunk(Func<string, string> name) => $$"""
        function {{name("linqChunk")}}(source, size) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          if (size < 1) throw new RangeError("Chunk size must be positive.");
          return { *[Symbol.iterator]() {
            let chunk = [];
            for (const value of {{name("linqValues")}}(source)) {
              chunk.push(value);
              if (chunk.length === size) { yield chunk; chunk = []; }
            }
            if (chunk.length !== 0) yield chunk;
          }
          };
        }

        """;

    private static string LinqZip(Func<string, string> name) => $$"""
        function {{name("linqZip")}}(first, second, selector) {
          if (first == null || second == null || selector == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            const left = {{name("linqValues")}}(first), right = {{name("linqValues")}}(second);
            try {
              while (true) {
                const leftItem = left.next(), rightItem = right.next();
                if (leftItem.done || rightItem.done) return;
                yield selector(leftItem.value, rightItem.value);
              }
            } finally {
              left.return?.();
              right.return?.();
            }
          }
          };
        }

        """;

    private static string LinqAggregate(Func<string, string> name) => $$"""
        function {{name("linqAggregate")}}(source, accumulator, seed, hasSeed, resultSelector) {
          if (source == null || accumulator == null) throw new TypeError("LINQ argument cannot be null.");
          const iterator = {{name("linqValues")}}(source);
          let result = seed;
          if (!hasSeed) {
            const first = iterator.next();
            if (first.done) throw new TypeError("Sequence contains no elements.");
            result = first.value;
          }
          for (let item = iterator.next(); !item.done; item = iterator.next())
            result = accumulator(result, item.value);
          return resultSelector == null ? result : resultSelector(result);
        }

        """;

}
