internal static partial class HelperSource
{
    private static string StructuralKeys(Func<string, string> name) => $$"""
        const {{name("keyIdentities")}} = new WeakMap();
        const {{name("keyEqualsSlot")}} = Symbol("key equality");
        const {{name("keyHashSlot")}} = Symbol("key hash");
        function {{name("keyObjectEquals")}}(left, right) {
          if (left == null || right == null) return left == right;
          return typeof left[{{name("keyEqualsSlot")}}] === "function" ? left[{{name("keyEqualsSlot")}}](right) : left === right;
        }
        function {{name("keyObjectHash")}}(value) {
          if (value == null) return 0;
          return typeof value[{{name("keyHashSlot")}}] === "function" ? value[{{name("keyHashSlot")}}]() : {{name("keyHash")}}(value);
        }
        let {{name("nextKeyIdentity")}} = 1;
        function {{name("keyHash")}}(value) {
          if (value == null) return 0;
          if (typeof value === "object" || typeof value === "function") {
            if (!{{name("keyIdentities")}}.has(value)) {{name("keyIdentities")}}.set(value, {{name("nextKeyIdentity")}}++);
            return {{name("keyIdentities")}}.get(value);
          }
          if (typeof value === "boolean") return value ? 1 : 0;
          if (typeof value === "number" && (value === 0 || value !== value)) return 0;
          const text = String(value);
          let hash = 2166136261;
          for (let i = 0; i < text.length; i++) hash = Math.imul(hash ^ text.charCodeAt(i), 16777619);
          return hash | 0;
        }
        function {{name("keyHashCombine")}}(values) {
          let hash = 17;
          for (const value of values) hash = (Math.imul(hash, 31) + value) | 0;
          return hash;
        }
        function {{name("keyCollection")}}(equals, hash, copyKey, copyValue, set, items = [], keyText = null) {
          if (items == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Collection cannot be null.");
          const buckets = new Map();
          let entries = [], free = [], size = 0, version = 0;
          function check(key) { if (!set && key == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("Dictionary key cannot be null."); }
          function find(key) {
            check(key);
            const code = hash(key), bucket = buckets.get(code);
            return { code, bucket, entry: bucket?.find(entry => entry.alive && equals(entry.key, key)) };
          }
          function store(key, value, add, replace = true) {
            const found = find(key);
            if (found.entry) {
              if (add && !set) throw new ({{name("exceptionType")}}("System.ArgumentException"))("An item with the same key has already been added.");
              if (!set && replace) found.entry.value = copyValue(value);
              return false;
            }
            const index = free.length === 0 ? entries.length : free.pop();
            const entry = { key: copyKey(key), value: set ? undefined : copyValue(value), alive: true, index };
            if (found.bucket) found.bucket.unshift(entry); else buckets.set(found.code, [entry]);
            entries[index] = entry; size++; version++;
            return true;
          }
          function removeEntry(found) {
            if (!found.entry) return false;
            found.entry.alive = false; found.bucket.splice(found.bucket.indexOf(found.entry), 1);
            free.push(found.entry.index); found.entry.key = undefined; found.entry.value = undefined;
            if (found.bucket.length === 0) buckets.delete(found.code);
            size--; return true;
          }
          // Capture version when enumeration starts, and copy value-type keys and
          // values when exposing them. Removing/clearing does not invalidate .NET enumeration.
          function iterate(kind) {
            return { get length() { return size; }, [Symbol.iterator]: function* () {
              const expected = version, snapshot = entries;
              for (const entry of snapshot) {
                if (version !== expected) throw new ({{name("exceptionType")}}("System.InvalidOperationException"))("Collection was modified; enumeration operation may not execute.");
                if (!entry.alive) continue;
                yield kind === 0 ? copyKey(entry.key) : kind === 1 ? copyValue(entry.value) : [copyKey(entry.key), copyValue(entry.value)];
              }
              if (version !== expected) throw new ({{name("exceptionType")}}("System.InvalidOperationException"))("Collection was modified; enumeration operation may not execute.");
            } };
          }
          const result = {
            get size() { return size; },
            get length() { return size; },
            toJSON() {
              if (set) return Array.from(this);
              if (keyText == null) throw new ({{name("exceptionType")}}("System.NotSupportedException"))("Composite dictionary keys cannot be represented as JSON property names.");
              const result = Object.create(null);
              for (const [key, value] of this) result[keyText(key)] = value;
              return result;
            },
            has(key) { return find(key).entry !== undefined; },
            take(key, fallback, remove) {
              const found = find(key); if (!found.entry) return [false, fallback];
              const value = set ? copyKey(found.entry.key) : copyValue(found.entry.value);
              if (remove) removeEntry(found);
              return [true, value];
            },
            getOrDefault(key, fallback) { const entry = find(key).entry; return entry ? copyValue(entry.value) : fallback; },
            get(key) { const entry = find(key).entry; if (!entry) throw new ({{name("exceptionType")}}("System.Collections.Generic.KeyNotFoundException"))("The key was not present in the dictionary."); return copyValue(entry.value); },
            set(key, value) { store(key, value, false); return this; },
            add(key, value) { return store(key, value, true); },
            tryAdd(key, value) { return store(key, value, false, false); },
            delete(key) { return removeEntry(find(key)); },
            clear() { for (const entry of entries) { entry.alive = false; entry.key = undefined; entry.value = undefined; } buckets.clear(); entries = []; free = []; size = 0; },
            keys() { return iterate(0); }, values() { return iterate(set ? 0 : 1); }, entries() { return iterate(2); },
            [Symbol.iterator]() { return iterate(set ? 0 : 2)[Symbol.iterator](); }
          };
          for (const item of {{name("linqValues")}}(items)) {
            if (set) result.add(item); else result.add(item[0], item[1]);
          }
          {{name("keyCollectionKinds")}}.set(result, set);
          return result;
        }
        function {{name("keySetOperation")}}(source, other, operation) {
          if (operation === "UnionWith") { for (const item of other) source.add(item); return; }
          if (operation === "IntersectWith") { for (const item of source) if (!other.has(item)) source.delete(item); return; }
          if (operation === "ExceptWith") { for (const item of other) source.delete(item); return; }
          if (operation === "SymmetricExceptWith") { for (const item of other) { if (!source.delete(item)) source.add(item); } return; }
          if (operation === "Overlaps") { for (const item of other) if (source.has(item)) return true; return false; }
          if (operation === "SetEquals" && source.size !== other.size) return false;
          const subset = operation === "IsSubsetOf" || operation === "IsProperSubsetOf" || operation === "SetEquals";
          if (operation === "IsProperSubsetOf" && source.size >= other.size || operation === "IsProperSupersetOf" && source.size <= other.size) return false;
          for (const item of subset ? source : other) if (!(subset ? other : source).has(item)) return false;
          return true;
        }
        function {{name("keyToDictionary")}}(source, keySelector, valueSelector, create, hasValueSelector = false) {
          if (source == null || keySelector == null || hasValueSelector && valueSelector == null) throw new ({{name("exceptionType")}}("System.ArgumentNullException"))("LINQ argument cannot be null.");
          const result = create();
          for (const item of {{name("linqValues")}}(source)) {
            const key = keySelector(item);
            result.add(key, valueSelector == null ? item : valueSelector(item));
          }
          return result;
        }

        """;
}
