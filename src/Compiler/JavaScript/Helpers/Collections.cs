internal static partial class HelperSource
{
    private static string ListState(Func<string, string> name) => $$"""
        const {{name("listVersions")}} = new WeakMap();
        function {{name("listCreate")}}(values) {
          if ({{name("listVersions")}}.has(values)) return values;
          {{name("listVersions")}}.set(values, 0);
          Object.defineProperty(values, Symbol.iterator, { configurable: true, value: function* () {
            const version = {{name("listVersions")}}.get(this);
            let index = 0;
            while (true) {
              if ({{name("listVersions")}}.get(this) !== version)
                throw new ({{name("exceptionType")}}("System.InvalidOperationException"))("Collection was modified; enumeration operation may not execute.");
              if (index >= this.length) return;
              yield this[index++];
            }
          }
          });
          return values;
        }
        function {{name("listChanged")}}(values) {
          const version = {{name("listVersions")}}.get(values);
          if (version !== undefined) {{name("listVersions")}}.set(values, version + 1);
        }
        function {{name("listAdd")}}(values, item) {
          values.push(item);
          {{name("listChanged")}}(values);
        }
        function {{name("listClear")}}(values) {
          values.length = 0;
          {{name("listChanged")}}(values);
        }
        function {{name("listReverse")}}(values) {
          values.reverse();
          {{name("listChanged")}}(values);
        }

        """;

    private static string QueueStack(Func<string, string> name) => $$"""
        function {{name("queueStackCreate")}}(source, stack, capacity) {
          if (capacity != null) {
            if (!Number.isInteger(capacity) || capacity < 0)
              throw new ({{name("exceptionType")}}("System.ArgumentOutOfRangeException"))("Capacity cannot be negative.");
            return [];
          }
          if (source == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Collection cannot be null.");
          const values = Array.from({{name("linqValues")}}(source));
          return stack ? values.reverse() : values;
        }
        function {{name("queueStackTake")}}(values, remove) {
          if (values.length === 0) throw new ({{name("exceptionType")}}("System.InvalidOperationException"))("Collection is empty.");
          return remove ? values.shift() : values[0];
        }

        """;

    private static string SequenceIndex(Func<string, string> name) => $$"""
        function {{name("sequenceIndex")}}(source, index) {
          if (source == null) throw new ({{name("exceptionType")}}("System.NullReferenceException"))("Object reference not set to an instance of an object.");
          if (Array.isArray(index)) index = index[1] ? source.length - index[0] : index[0];
          if (!Number.isInteger(index) || index < 0 || index >= source.length)
            throw new ({{name("exceptionType")}}({{name("listVersions")}}.has(source) ? "System.ArgumentOutOfRangeException" : "System.IndexOutOfRangeException"))("Index was outside the bounds of the sequence.");
          return source[index];
        }
        function {{name("sequenceSet")}}(source, index, value) {
          if (source == null) throw new ({{name("exceptionType")}}("System.NullReferenceException"))("Object reference not set to an instance of an object.");
          if (Array.isArray(index)) index = index[1] ? source.length - index[0] : index[0];
          if (!Number.isInteger(index) || index < 0 || index >= source.length)
            throw new ({{name("exceptionType")}}({{name("listVersions")}}.has(source) ? "System.ArgumentOutOfRangeException" : "System.IndexOutOfRangeException"))("Index was outside the bounds of the sequence.");
          source[index] = value;
          {{name("listChanged")}}(source);
          return value;
        }

        """;

    private static string DictionaryIndex(Func<string, string> name) => $$"""
        function {{name("dictionaryIndex")}}(source, key) {
          {{name("notNull")}}(source);
          if (key == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Dictionary key cannot be null.");
          if (!Object.hasOwn(source, key)) throw new ({{name("exceptionType")}}("System.Collections.Generic.KeyNotFoundException"))("The key was not present in the dictionary.");
          return source[key];
        }
        function {{name("dictionarySet")}}(source, key, value) {
          {{name("notNull")}}(source);
          if (key == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Dictionary key cannot be null.");
          source[key] = value;
          return value;
        }

        """;

    // Members of Dictionary, List and HashSet with the argument checks and results of the CLR types.
    private static string CollectionMembers(Func<string, string> name) => $$"""
        function {{name("listPredicate")}}(source, predicate, mode, fallback, copy = value => value) {
          if (source == null) throw new ({{name("exceptionType")}}("System.NullReferenceException"))("Object reference not set to an instance of an object.");
          if (predicate == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Predicate cannot be null.");
          const end = source.length, result = [];
          const read = index => index < source.length ? source[index] : fallback;
          for (let index = 0; index < (mode === "index" || mode === "exists" ? end : source.length); index++) {
            const matched = predicate(read(index));
            if (mode === "every") { if (!matched) return false; }
            else if (matched) {
              if (mode === "find") return read(index);
              if (mode === "index") return index;
              if (mode === "exists") return true;
              result.push(copy(read(index)));
            }
          }
          return mode === "find" ? fallback : mode === "all" ? result : mode === "index" ? -1 : mode === "every";
        }
        function {{name("collectionKey")}}(key) {
          if (key == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Value cannot be null. (Parameter 'key')");
          return key;
        }
        function {{name("dictionaryAdd")}}(source, key, value) {
          if (Object.hasOwn(source, {{name("collectionKey")}}(key)))
            throw new ({{name("exceptionType")}}("System.ArgumentException"))(`An item with the same key has already been added. Key: ${key}`);
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
          {{name("listChanged")}}(source);
          return true;
        }
        function {{name("listAddRange")}}(source, items, copy = value => value) {
          if (items == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Collection cannot be null.");
          if (source === items) items = source.slice();
          for (const item of {{name("linqValues")}}(items)) {{name("listAdd")}}(source, copy(item));
        }
        function {{name("listCheckIndex")}}(source, index, inclusive) {
          if (!Number.isInteger(index) || index < 0 || index > source.length || !inclusive && index === source.length)
            throw new ({{name("exceptionType")}}("System.ArgumentOutOfRangeException"))("Index was out of range. Must be non-negative and less than the size of the collection.");
        }
        function {{name("listRemoveAt")}}(source, index) {
          {{name("listCheckIndex")}}(source, index, false);
          source.splice(index, 1);
          {{name("listChanged")}}(source);
        }
        function {{name("listInsert")}}(source, index, item) {
          {{name("listCheckIndex")}}(source, index, true);
          source.splice(index, 0, item);
          {{name("listChanged")}}(source);
        }
        function {{name("listRemoveAll")}}(source, predicate) {
          if (predicate == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Predicate cannot be null.");
          let kept = 0;
          for (let index = 0; index < source.length; index++) {
            const item = source[index];
            if (!predicate(item)) source[kept++] = item;
          }
          const removed = source.length - kept;
          source.length = kept;
          if (removed !== 0) {{name("listChanged")}}(source);
          return removed;
        }
        function {{name("listGetRange")}}(source, index, count) {
          if (!Number.isInteger(index) || !Number.isInteger(count) || index < 0 || count < 0)
            throw new ({{name("exceptionType")}}("System.ArgumentOutOfRangeException"))("Offset and length were out of bounds for the array.");
          if (index + count > source.length)
            throw new ({{name("exceptionType")}}("System.ArgumentException"))("Offset and length were out of bounds for the array.");
          return {{name("listCreate")}}(source.slice(index, index + count));
        }
        function {{name("listSort")}}(source, comparison) {
          if (comparison === null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Comparison cannot be null.");
          if (comparison === "Ordinal" || comparison === "OrdinalIgnoreCase") {
            const key = comparison === "Ordinal" ? text => text : text => Array.from(text, character => {
              const mapped = character.toUpperCase();
              return mapped.length === character.length ? mapped : character;
            }).join("");
            comparison = (left, right) => left == null || right == null
              ? (left == null ? (right == null ? 0 : -1) : 1)
              : key(left) < key(right) ? -1 : key(left) > key(right) ? 1 : 0;
          }
          try {
            source.sort(comparison ?? ((left, right) => left !== left ? (right !== right ? 0 : -1)
              : right !== right ? 1 : left < right ? -1 : left > right ? 1 : 0));
          } catch (error) {
            throw new ({{name("exceptionType")}}("System.InvalidOperationException"))("Failed to compare two elements in the array.", { cause: error });
          }
          {{name("listChanged")}}(source);
        }
        function {{name("setUnionWith")}}(set, items) {
          if (items == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Collection cannot be null.");
          for (const item of {{name("linqValues")}}(items)) set.add(item);
        }
        function {{name("setIntersectWith")}}(set, items) {
          if (items == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Collection cannot be null.");
          const keep = new Set({{name("linqValues")}}(items));
          for (const item of Array.from(set)) if (!keep.has(item)) set.delete(item);
        }
        function {{name("setExceptWith")}}(set, items) {
          if (items == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Collection cannot be null.");
          for (const item of {{name("linqValues")}}(items)) set.delete(item);
        }

        """;
}
