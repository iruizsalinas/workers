internal static partial class HelperSource
{
    private static string LinqNumericAggregate(Func<string, string> name) => $$"""
        function {{name("linqNumericAggregate")}}(source, operation, selector, kind, nullable) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          let total = 0, count = 0;
          for (const item of {{name("linqValues")}}(source)) {
            const value = selector == null ? item : selector(item);
            if (value == null && nullable) continue;
            total += value;
            count++;
            if (kind === 0 && (total < -2147483648 || total > 2147483647))
              throw new RangeError("Arithmetic operation resulted in an overflow.");
          }
          if (operation === 1 && count === 0) {
            if (nullable) return null;
            throw new TypeError("Sequence contains no elements.");
          }
          const result = operation === 0 ? total : total / count;
          return kind === 0 ? result | 0 : kind === 1 ? Math.fround(result) : result;
        }

        """;

    private static string LinqExtremum(Func<string, string> name) => $$"""
        function {{name("linqExtremumCompare")}}(left, right, kind) {
          if (left == null || right == null) return left == null ? (right == null ? 0 : -1) : 1;
          if (kind === 2) { left = new Date(left).getTime(); right = new Date(right).getTime(); }
          else if (kind === 1) { left = left.charCodeAt(0); right = right.charCodeAt(0); }
          if (left === right) return 0;
          if (typeof left === "number" && Number.isNaN(left)) return Number.isNaN(right) ? 0 : -1;
          if (typeof right === "number" && Number.isNaN(right)) return 1;
          return left < right ? -1 : 1;
        }
        function {{name("linqExtremum")}}(source, selector, maximum, kind, canBeNull, by, returnKey) {
          if (source == null) throw new TypeError("LINQ source cannot be null.");
          let found = false, result = null, resultKey = null;
          for (const value of {{name("linqValues")}}(source)) {
            const key = selector == null ? value : selector(value);
            if (!by && key == null) continue;
            if (!found || (maximum ? 1 : -1) * {{name("linqExtremumCompare")}}(key, resultKey, kind) > 0) {
              found = true; result = value; resultKey = key;
            }
          }
          if (found) return returnKey ? resultKey : result;
          if (canBeNull) return null;
          throw new TypeError("Sequence contains no elements.");
        }

        """;

    private static string LinqSet(Func<string, string> name) => $$"""
        function {{name("linqSet")}}(first, second, keySelector, operation, secondContainsKeys) {
          if (first == null || second == null) throw new TypeError("LINQ source cannot be null.");
          const key = keySelector == null ? value => value : keySelector;
          return { *[Symbol.iterator]() {
            if (operation === 0) {
              const seen = new Set();
              for (const source of [first, second]) for (const value of {{name("linqValues")}}(source))
                if (!seen.has(key(value))) { seen.add(key(value)); yield value; }
              return;
            }
            const other = new Set(Array.from({{name("linqValues")}}(second),
              secondContainsKeys ? value => value : key));
            const yielded = new Set();
            for (const value of {{name("linqValues")}}(first)) {
              const itemKey = key(value);
              if ((operation === 1 ? other.has(itemKey) : !other.has(itemKey)) && !yielded.has(itemKey)) {
                yielded.add(itemKey); yield value;
              }
            }
          }
          };
        }

        """;

}
