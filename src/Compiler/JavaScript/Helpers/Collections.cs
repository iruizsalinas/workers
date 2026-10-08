internal static partial class HelperSource
{
    private static string QueueStack(Func<string, string> name) => $$"""
        function {{name("queueStackCreate")}}(source, stack, capacity) {
          if (capacity != null) {
            if (!Number.isInteger(capacity) || capacity < 0)
              throw new RangeError("Capacity cannot be negative.");
            return [];
          }
          if (source == null) throw new TypeError("Collection cannot be null.");
          const values = Array.from({{name("linqValues")}}(source));
          return stack ? values.reverse() : values;
        }
        function {{name("queueStackTake")}}(values, remove) {
          if (values.length === 0) throw new RangeError("Collection is empty.");
          return remove ? values.shift() : values[0];
        }

        """;

    private static string SequenceIndex(Func<string, string> name) => $$"""
        function {{name("sequenceIndex")}}(source, index) {
          if (!Number.isInteger(index) || index < 0 || index >= source.length)
            throw new RangeError("Index was outside the bounds of the sequence.");
          return source[index];
        }
        function {{name("sequenceSet")}}(source, index, value) {
          if (!Number.isInteger(index) || index < 0 || index >= source.length)
            throw new RangeError("Index was outside the bounds of the sequence.");
          source[index] = value;
          return value;
        }

        """;

    private static string DictionaryIndex(Func<string, string> name) => $$"""
        function {{name("dictionaryIndex")}}(source, key) {
          if (key == null) throw new TypeError("Dictionary key cannot be null.");
          if (!Object.hasOwn(source, key)) throw new RangeError("The key was not present in the dictionary.");
          return source[key];
        }
        function {{name("dictionarySet")}}(source, key, value) {
          if (key == null) throw new TypeError("Dictionary key cannot be null.");
          source[key] = value;
          return value;
        }

        """;

    // Members of Dictionary, List and HashSet with the argument checks and results of the CLR types.
    private static string CollectionMembers(Func<string, string> name) => $$"""
        function {{name("collectionKey")}}(key) {
          if (key == null) throw new TypeError("Value cannot be null. (Parameter 'key')");
          return key;
        }
        function {{name("dictionaryAdd")}}(source, key, value) {
          if (Object.hasOwn(source, {{name("collectionKey")}}(key)))
            throw new RangeError(`An item with the same key has already been added. Key: ${key}`);
          source[key] = value;
        }
        function {{name("dictionaryTryAdd")}}(source, key, value) {
          if (Object.hasOwn(source, {{name("collectionKey")}}(key))) return false;
          source[key] = value;
          return true;
        }
        function {{name("dictionaryContainsKey")}}(source, key) {
          return Object.hasOwn(source, {{name("collectionKey")}}(key));
        }
        function {{name("dictionaryRemove")}}(source, key) {
          if (!Object.hasOwn(source, {{name("collectionKey")}}(key))) return false;
          delete source[key];
          return true;
        }
        function {{name("dictionaryClear")}}(source) {
          for (const key of Object.keys(source)) delete source[key];
        }
        function {{name("dictionaryGetValueOrDefault")}}(source, key, fallback) {
          return Object.hasOwn(source, {{name("collectionKey")}}(key)) ? source[key] : fallback;
        }
        function {{name("listIndexOf")}}(source, item, equals) {
          for (let index = 0; index < source.length; index++) if (equals(source[index], item)) return index;
          return -1;
        }
        function {{name("listRemove")}}(source, item, equals) {
          const index = {{name("listIndexOf")}}(source, item, equals);
          if (index < 0) return false;
          source.splice(index, 1);
          return true;
        }
        function {{name("listAddRange")}}(source, items) {
          if (items == null) throw new TypeError("Collection cannot be null.");
          if (source === items) items = source.slice();
          for (const item of {{name("linqValues")}}(items)) source.push(item);
        }
        function {{name("listCheckIndex")}}(source, index, inclusive) {
          if (!Number.isInteger(index) || index < 0 || index > source.length || !inclusive && index === source.length)
            throw new RangeError("Index was out of range. Must be non-negative and less than the size of the collection.");
        }
        function {{name("listRemoveAt")}}(source, index) {
          {{name("listCheckIndex")}}(source, index, false);
          source.splice(index, 1);
        }
        function {{name("listInsert")}}(source, index, item) {
          {{name("listCheckIndex")}}(source, index, true);
          source.splice(index, 0, item);
        }
        function {{name("listRemoveAll")}}(source, predicate) {
          if (predicate == null) throw new TypeError("Predicate cannot be null.");
          let kept = 0;
          for (const item of source) if (!predicate(item)) source[kept++] = item;
          const removed = source.length - kept;
          source.length = kept;
          return removed;
        }
        function {{name("listGetRange")}}(source, index, count) {
          if (!Number.isInteger(index) || !Number.isInteger(count) || index < 0 || count < 0 || index + count > source.length)
            throw new RangeError("Offset and length were out of bounds for the array.");
          return source.slice(index, index + count);
        }
        function {{name("listSort")}}(source, comparison) {
          if (comparison === "Ordinal" || comparison === "OrdinalIgnoreCase") {
            const key = comparison === "Ordinal" ? text => text : text => Array.from(text, character => {
              const mapped = character.toUpperCase();
              return mapped.length === character.length ? mapped : character;
            }).join("");
            comparison = (left, right) => left == null || right == null
              ? (left == null ? (right == null ? 0 : -1) : 1)
              : key(left) < key(right) ? -1 : key(left) > key(right) ? 1 : 0;
          }
          source.sort(comparison ?? ((left, right) => left !== left ? (right !== right ? 0 : -1)
            : right !== right ? 1 : left < right ? -1 : left > right ? 1 : 0));
        }
        function {{name("setUnionWith")}}(set, items) {
          for (const item of {{name("linqValues")}}(items)) set.add(item);
        }
        function {{name("setIntersectWith")}}(set, items) {
          const keep = new Set({{name("linqValues")}}(items));
          for (const item of Array.from(set)) if (!keep.has(item)) set.delete(item);
        }
        function {{name("setExceptWith")}}(set, items) {
          for (const item of {{name("linqValues")}}(items)) set.delete(item);
        }

        """;
}
