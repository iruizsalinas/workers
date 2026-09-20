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

    private static string LinqOrder(Func<string, string> name) => $$"""
        const {{name("linqOrderState")}} = Symbol();
        function {{name("linqOrderCompare")}}(left, right, kind) {
          if (left == null || right == null)
            return left == null ? (right == null ? 0 : -1) : 1;
          if (kind === 2) {
            left = new Date(left).getTime();
            right = new Date(right).getTime();
          } else if (kind === 1) {
            left = left.charCodeAt(0);
            right = right.charCodeAt(0);
          }
          if (left === right) return 0;
          if (typeof left === "number" && Number.isNaN(left))
            return typeof right === "number" && Number.isNaN(right) ? 0 : -1;
          if (typeof right === "number" && Number.isNaN(right)) return 1;
          return left < right ? -1 : 1;
        }
        function {{name("linqOrdered")}}(source, criteria) {
          return {
            [{{name("linqOrderState")}}]: { source, criteria },
            *[Symbol.iterator]() {
              const entries = Array.from({{name("linqValues")}}(source), (value, index) => ({
                value, index, keys: criteria.map(criterion => criterion.selector(value))
              }));
              entries.sort((left, right) => {
                for (let index = 0; index < criteria.length; index++) {
                  const criterion = criteria[index];
                  const comparison = {{name("linqOrderCompare")}}(
                    left.keys[index], right.keys[index], criterion.kind);
                  if (comparison !== 0) return criterion.descending ? -comparison : comparison;
                }
                return left.index - right.index;
              });
              for (const entry of entries) yield entry.value;
            }
          };
        }
        function {{name("linqOrder")}}(source, selector, descending, kind, append) {
          if (source == null || selector == null) throw new TypeError("LINQ argument cannot be null.");
          const state = append ? source[{{name("linqOrderState")}}] : null;
          if (append && state == null) throw new TypeError("ThenBy requires an ordered sequence.");
          return {{name("linqOrdered")}}(
            state == null ? source : state.source,
            [...(state == null ? [] : state.criteria), { selector, descending, kind }]);
        }

        """;

    private static string LinqGroupBy(Func<string, string> name) => $$"""
        function {{name("linqGroupBy")}}(source, keySelector, elementSelector) {
          if (source == null || keySelector == null) throw new TypeError("LINQ argument cannot be null.");
          return { *[Symbol.iterator]() {
            const groups = new Map();
            for (const value of {{name("linqValues")}}(source)) {
              const key = keySelector(value);
              let group = groups.get(key);
              if (group === undefined) {
                group = [];
                group.key = key;
                groups.set(key, group);
              }
              group.push(elementSelector == null ? value : elementSelector(value));
            }
            yield* groups.values();
          }
          };
        }

        """;

    private static string LinqToDictionary(Func<string, string> name) => $$"""
        function {{name("linqToDictionary")}}(source, keySelector, elementSelector) {
          if (source == null || keySelector == null) throw new TypeError("LINQ argument cannot be null.");
          const result = Object.create(null);
          for (const value of {{name("linqValues")}}(source)) {
            const key = keySelector(value);
            if (key == null) throw new TypeError("Dictionary key cannot be null.");
            if (Object.hasOwn(result, key)) throw new TypeError("An item with the same key has already been added.");
            result[key] = elementSelector == null ? value : elementSelector(value);
          }
          return result;
        }

        """;

    private static string LinqToLookup(Func<string, string> name) => $$"""
        function {{name("linqToLookup")}}(source, keySelector, elementSelector) {
          if (source == null || keySelector == null) throw new TypeError("LINQ argument cannot be null.");
          const groups = new Map();
          for (const value of {{name("linqValues")}}(source)) {
            const key = keySelector(value);
            let group = groups.get(key);
            if (group === undefined) {
              group = [];
              group.key = key;
              groups.set(key, group);
            }
            group.push(elementSelector == null ? value : elementSelector(value));
          }
          const empty = [];
          return {
            count: groups.size,
            contains: key => groups.has(key),
            get: key => groups.get(key) ?? empty,
            *[Symbol.iterator]() { yield* groups.values(); }
          };
        }

        """;

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
