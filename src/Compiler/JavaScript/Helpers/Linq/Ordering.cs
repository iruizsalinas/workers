internal static partial class HelperSource
{
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

}
