internal static partial class HelperSource
{
    // JsonNode trees are plain JSON values: JsonObject is a null-prototype object, JsonArray an array,
    // JsonValue a string, boolean or finite number, and JSON null is null. Numbers a double cannot hold
    // exactly keep their source text through JSON.rawJSON. Objects are only written through
    // defineProperty and read through Object.hasOwn, so keys such as "__proto__" stay ordinary data.
    // Each object and array may have one parent, tracked in a WeakMap, so adding a node that already has
    // a parent or that would create a cycle throws like System.Text.Json.Nodes.
    private static string JsonNode(Func<string, string> name) => $$"""
        const {{name("jsonNodeParents")}} = new WeakMap();
        function {{name("jsonNodeIsRaw")}}(value) {
          return typeof JSON.isRawJSON === "function" && JSON.isRawJSON(value);
        }
        function {{name("jsonNodeIsContainer")}}(value) {
          return value !== null && typeof value === "object" && !{{name("jsonNodeIsRaw")}}(value);
        }
        function {{name("jsonNodeIsObject")}}(value) {
          return {{name("jsonNodeIsContainer")}}(value) && !Array.isArray(value);
        }
        function {{name("jsonNodeIsValue")}}(value) {
          return typeof value === "string" || typeof value === "number" || typeof value === "boolean"
            || {{name("jsonNodeIsRaw")}}(value);
        }
        function {{name("jsonNodeRequired")}}(value) {
          if (value == null) throw new TypeError("Object reference not set to an instance of an object.");
          return value;
        }
        function {{name("jsonNodeKind")}}(value) {
          {{name("jsonNodeRequired")}}(value);
          if (Array.isArray(value)) return 2;
          if (typeof value === "string") return 3;
          if (typeof value === "number" || {{name("jsonNodeIsRaw")}}(value)) return 4;
          if (typeof value === "boolean") return value ? 5 : 6;
          return 1;
        }
        function {{name("jsonNodeAs")}}(value, kind) {
          {{name("jsonNodeRequired")}}(value);
          if (kind === 1 ? !{{name("jsonNodeIsObject")}}(value) : kind === 2 ? !Array.isArray(value) : !{{name("jsonNodeIsValue")}}(value))
            throw new Error(`The node must be of type '${["", "JsonObject", "JsonArray", "JsonValue"][kind]}'.`);
          return value;
        }
        function {{name("jsonNodeIs")}}(value, kind) {
          return kind === 1 ? {{name("jsonNodeIsObject")}}(value) : kind === 2 ? Array.isArray(value) : {{name("jsonNodeIsValue")}}(value);
        }
        function {{name("jsonNodeAttach")}}(parent, child) {
          if (!{{name("jsonNodeIsContainer")}}(child)) return child;
          const parents = {{name("jsonNodeParents")}};
          if (parents.has(child)) throw new Error("The node already has a parent.");
          for (let ancestor = parent; ancestor !== undefined; ancestor = parents.get(ancestor))
            if (ancestor === child) throw new Error("A node cycle was detected.");
          parents.set(child, parent);
          return child;
        }
        function {{name("jsonNodeDetach")}}(child) {
          if ({{name("jsonNodeIsContainer")}}(child)) {{name("jsonNodeParents")}}.delete(child);
        }
        function {{name("jsonNodeDefine")}}(target, key, value) {
          Object.defineProperty(target, key, { value, writable: true, enumerable: true, configurable: true });
        }
        function {{name("jsonNodeKey")}}(key) {
          if (key == null) throw new TypeError("Value cannot be null. (Parameter 'propertyName')");
          return key;
        }
        function {{name("jsonNodeNumberKey")}}(text) {
          const match = /^(-?)([0-9]+)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$/.exec(text);
          if (!match) return text;
          let digits = (match[2] + (match[3] ?? "")).replace(/^0+/, "");
          if (digits === "") return "0";
          let exponent = Number(match[4] ?? 0) - (match[3] ?? "").length;
          const trimmed = digits.replace(/0+$/, "");
          exponent += digits.length - trimmed.length;
          return match[1] + trimmed + "e" + exponent;
        }
        function {{name("jsonNodeNumberText")}}(value) {
          return {{name("jsonNodeIsRaw")}}(value) ? value.rawJSON : String(value);
        }
        function {{name("jsonNodeReviver")}}(key, value, context) {
          if (typeof value !== "number" || typeof context?.source !== "string" || typeof JSON.rawJSON !== "function")
            return value;
          return Number.isFinite(value) && {{name("jsonNodeNumberKey")}}(context.source) === {{name("jsonNodeNumberKey")}}(String(value))
            ? value
            : JSON.rawJSON(context.source);
        }
        function {{name("jsonNodeCopy")}}(value, depth) {
          if (value === null || typeof value === "string" || typeof value === "boolean" || {{name("jsonNodeIsRaw")}}(value))
            return value;
          if (typeof value === "number") {
            if (!Number.isFinite(value)) throw new TypeError("JSON numbers cannot be NaN or infinite.");
            return value;
          }
          if (Array.isArray(value) || typeof value === "object"
              && [Object.prototype, null].includes(Object.getPrototypeOf(value))) {
            if (depth >= 64) throw new RangeError("The maximum configured depth of 64 has been exceeded.");
            if (Array.isArray(value)) {
              const result = [];
              for (const item of value) result.push({{name("jsonNodeAttach")}}(result, {{name("jsonNodeCopy")}}(item, depth + 1)));
              return result;
            }
            const result = Object.create(null);
            for (const key of Object.keys(value))
              {{name("jsonNodeDefine")}}(result, key, {{name("jsonNodeAttach")}}(result, {{name("jsonNodeCopy")}}(value[key], depth + 1)));
            return result;
          }
          throw new TypeError("The value cannot be represented as a JSON node.");
        }
        // Kinds: 0 JsonNode, 1 JsonObject, 2 JsonArray, 3 JsonValue.
        function {{name("jsonNodeImport")}}(value, kind = 0) {
          if (value === undefined) throw new TypeError("The value cannot be represented as a JSON node.");
          if (value !== null && kind !== 0 && !{{name("jsonNodeIs")}}(value, kind)) {
            if (kind === 3) throw new Error("The element cannot be an object or array.");
            throw new TypeError(`The JSON value could not be converted to System.Text.Json.Nodes.${kind === 1 ? "JsonObject" : "JsonArray"}.`);
          }
          return {{name("jsonNodeCopy")}}(value, 0);
        }
        function {{name("jsonNodeParse")}}(text, kind = 0) {
          if (text == null) throw new TypeError("Value cannot be null. (Parameter 'json')");
          return {{name("jsonNodeImport")}}(JSON.parse(text, {{name("jsonNodeReviver")}}), kind);
        }
        function {{name("jsonNodeNumber")}}(value) {
          if (value != null && !Number.isFinite(value))
            throw new Error(".NET number values such as positive and negative infinity cannot be written as valid JSON.");
          return value;
        }
        function {{name("jsonNodeSingle")}}(value) {
          return value == null ? null : Number({{name("numberText")}}({{name("jsonNodeNumber")}}(value), true));
        }
        function {{name("jsonNodeDate")}}(value, offset) {
          if (value == null) return null;
          const text = new Date(value).toISOString(), fraction = text.slice(20, 23).replace(/0+$/, "");
          return text.slice(0, 19) + (fraction ? "." + fraction : "") + (offset ? "+00:00" : "Z");
        }
        // Kinds: 0 string, 1 bool, 2 integer in [minimum, maximum], 3 float, 4 double, 5 char,
        // 7 DateTime/DateTimeOffset, 8 Guid, 11 JsonElement.
        function {{name("jsonNodeValue")}}(node, kind, type, minimum = 0, maximum = 0) {
          {{name("jsonNodeAs")}}(node, 3);
          const raw = {{name("jsonNodeIsRaw")}}(node);
          const number = raw ? Number(node.rawJSON) : node;
          switch (kind) {
            case 0: if (typeof node === "string") return node; break;
            case 1: if (typeof node === "boolean") return node; break;
            case 2:
              if (typeof number === "number" && (!raw || /^-?(?:0|[1-9][0-9]*)$/.test(node.rawJSON))
                && Number.isInteger(number) && number >= minimum && number <= maximum) return number;
              break;
            case 3: if (typeof number === "number") return Math.fround(number); break;
            case 4: if (typeof number === "number") return number; break;
            case 5: if (typeof node === "string" && node.length === 1) return node; break;
            case 7: case 8:
              if (typeof node === "string") {
                try { return {{name("jsonDeserializeValue")}}(node, 0, kind); } catch { }
              }
              break;
            case 11: return number;
          }
          const kinds = ["", "Object", "Array", "String", "Number", "True", "False"];
          throw new Error(`An element of type '${kinds[{{name("jsonNodeKind")}}(node)]}' cannot be converted to a '${type}'.`);
        }
        function {{name("jsonNodeTryValue")}}(node, fallback, kind, type, minimum, maximum) {
          try {
            return [true, {{name("jsonNodeValue")}}(node, kind, type, minimum, maximum)];
          } catch {
            return [false, fallback];
          }
        }
        function {{name("jsonObjectGet")}}(target, key) {
          {{name("jsonNodeKey")}}(key);
          return Object.hasOwn(target, key) ? target[key] : null;
        }
        function {{name("jsonObjectSet")}}(target, key, value) {
          {{name("jsonNodeKey")}}(key);
          const exists = Object.hasOwn(target, key), previous = exists ? target[key] : undefined;
          if (exists && previous === value) return value;
          {{name("jsonNodeAttach")}}(target, value);
          if (exists) {{name("jsonNodeDetach")}}(previous);
          {{name("jsonNodeDefine")}}(target, key, value);
          return value;
        }
        function {{name("jsonObjectAdd")}}(target, key, value) {
          if (Object.hasOwn(target, {{name("jsonNodeKey")}}(key)))
            throw new Error(`An item with the same key has already been added. Key: ${key}`);
          {{name("jsonNodeDefine")}}(target, key, {{name("jsonNodeAttach")}}(target, value));
        }
        function {{name("jsonObjectTryAdd")}}(target, key, value) {
          if (Object.hasOwn(target, {{name("jsonNodeKey")}}(key))) return false;
          {{name("jsonNodeDefine")}}(target, key, {{name("jsonNodeAttach")}}(target, value));
          return true;
        }
        function {{name("jsonObjectRemove")}}(target, key) {
          if (!Object.hasOwn(target, {{name("jsonNodeKey")}}(key))) return false;
          {{name("jsonNodeDetach")}}(target[key]);
          delete target[key];
          return true;
        }
        function {{name("jsonObjectContainsKey")}}(target, key) {
          return Object.hasOwn(target, {{name("jsonNodeKey")}}(key));
        }
        function {{name("jsonObjectTake")}}(target, key) {
          return Object.hasOwn(target, {{name("jsonNodeKey")}}(key)) ? [true, target[key]] : [false, null];
        }
        function {{name("jsonObjectClear")}}(target) {
          for (const key of Object.keys(target)) {
            {{name("jsonNodeDetach")}}(target[key]);
            delete target[key];
          }
        }
        function {{name("jsonObjectFrom")}}(entries) {
          const result = Object.create(null);
          for (const [key, value] of entries) {{name("jsonObjectAdd")}}(result, key, value);
          return result;
        }
        function {{name("jsonArrayIndex")}}(target, index, inclusive) {
          if (!Number.isInteger(index) || index < 0 || index > target.length || !inclusive && index === target.length)
            throw new RangeError("Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')");
        }
        function {{name("jsonArrayGet")}}(target, index) {
          {{name("jsonArrayIndex")}}(target, index, false);
          return target[index];
        }
        function {{name("jsonArraySet")}}(target, index, value) {
          {{name("jsonArrayIndex")}}(target, index, false);
          const previous = target[index];
          if (previous === value) return value;
          {{name("jsonNodeAttach")}}(target, value);
          {{name("jsonNodeDetach")}}(previous);
          target[index] = value;
          return value;
        }
        function {{name("jsonArrayAdd")}}(target, value) {
          target.push({{name("jsonNodeAttach")}}(target, value));
        }
        function {{name("jsonArrayInsert")}}(target, index, value) {
          {{name("jsonArrayIndex")}}(target, index, true);
          target.splice(index, 0, {{name("jsonNodeAttach")}}(target, value));
        }
        function {{name("jsonArrayRemoveAt")}}(target, index) {
          {{name("jsonArrayIndex")}}(target, index, false);
          {{name("jsonNodeDetach")}}(target.splice(index, 1)[0]);
        }
        function {{name("jsonArrayRemoveRange")}}(target, index, count) {
          if (!Number.isInteger(index) || index < 0 || !Number.isInteger(count) || count < 0)
            throw new RangeError("Index and count must be non-negative values.");
          if (index + count > target.length)
            throw new RangeError("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
          for (const item of target.splice(index, count)) {{name("jsonNodeDetach")}}(item);
        }
        function {{name("jsonArrayRemoveAll")}}(target, predicate) {
          if (predicate == null) throw new TypeError("Value cannot be null. (Parameter 'match')");
          const keep = target.map(item => !predicate(item));
          let kept = 0;
          for (let index = 0; index < target.length; index++) {
            if (keep[index]) target[kept++] = target[index];
            else {{name("jsonNodeDetach")}}(target[index]);
          }
          const removed = target.length - kept;
          target.length = kept;
          return removed;
        }
        function {{name("jsonArrayClear")}}(target) {
          for (const item of target) {{name("jsonNodeDetach")}}(item);
          target.length = 0;
        }
        function {{name("jsonArrayFrom")}}(items) {
          const result = [];
          for (const item of items) result.push({{name("jsonNodeAttach")}}(result, item));
          return result;
        }
        function {{name("jsonNodeGet")}}(node, key) {
          return {{name("jsonObjectGet")}}({{name("jsonNodeAs")}}(node, 1), key);
        }
        function {{name("jsonNodeSet")}}(node, key, value) {
          return {{name("jsonObjectSet")}}({{name("jsonNodeAs")}}(node, 1), key, value);
        }
        function {{name("jsonNodePropertyKey")}}(node, index) {
          const keys = Object.keys(node);
          if (!Number.isInteger(index) || index < 0 || index >= keys.length)
            throw new RangeError("Specified argument was out of the range of valid values. (Parameter 'index')");
          return keys[index];
        }
        function {{name("jsonNodeAt")}}(node, index) {
          {{name("jsonNodeRequired")}}(node);
          if (Array.isArray(node)) return {{name("jsonArrayGet")}}(node, index);
          if ({{name("jsonNodeIsObject")}}(node)) return node[{{name("jsonNodePropertyKey")}}(node, index)];
          throw new Error("The node must be of type 'JsonArray, JsonObject'.");
        }
        function {{name("jsonNodeSetAt")}}(node, index, value) {
          {{name("jsonNodeRequired")}}(node);
          if (Array.isArray(node)) return {{name("jsonArraySet")}}(node, index, value);
          if ({{name("jsonNodeIsObject")}}(node))
            return {{name("jsonObjectSet")}}(node, {{name("jsonNodePropertyKey")}}(node, index), value);
          throw new Error("The node must be of type 'JsonArray, JsonObject'.");
        }
        function {{name("jsonNodeDeepClone")}}(node) {
          if (!{{name("jsonNodeIsContainer")}}({{name("jsonNodeRequired")}}(node))) return node;
          if (Array.isArray(node)) {
            const result = [];
            for (const item of node) result.push({{name("jsonNodeAttach")}}(result, item == null ? null : {{name("jsonNodeDeepClone")}}(item)));
            return result;
          }
          const result = Object.create(null);
          for (const key of Object.keys(node))
            {{name("jsonNodeDefine")}}(result, key, {{name("jsonNodeAttach")}}(result, node[key] == null ? null : {{name("jsonNodeDeepClone")}}(node[key])));
          return result;
        }
        function {{name("jsonNodeDeepEquals")}}(left, right) {
          if (left === right) return true;
          if (left == null || right == null) return false;
          const isNumber = value => typeof value === "number" || {{name("jsonNodeIsRaw")}}(value);
          if (isNumber(left) || isNumber(right))
            return isNumber(left) && isNumber(right)
              && {{name("jsonNodeNumberKey")}}({{name("jsonNodeNumberText")}}(left)) === {{name("jsonNodeNumberKey")}}({{name("jsonNodeNumberText")}}(right));
          if (Array.isArray(left))
            return Array.isArray(right) && left.length === right.length
              && left.every((item, index) => {{name("jsonNodeDeepEquals")}}(item, right[index]));
          if (!{{name("jsonNodeIsObject")}}(left) || !{{name("jsonNodeIsObject")}}(right)) return false;
          const keys = Object.keys(left);
          return keys.length === Object.keys(right).length
            && keys.every(key => Object.hasOwn(right, key) && {{name("jsonNodeDeepEquals")}}(left[key], right[key]));
        }
        // Projects a tree for jsonSerializeClr: numbers take the .NET double format and keep raw text.
        function {{name("jsonNodeProject")}}(value) {
          if (typeof value === "number") return {{name("jsonClrNumber")}}(value, false);
          if (Array.isArray(value)) return value.map({{name("jsonNodeProject")}});
          if (!{{name("jsonNodeIsObject")}}(value)) return value;
          const result = Object.create(null);
          for (const key of Object.keys(value)) {{name("jsonNodeDefine")}}(result, key, {{name("jsonNodeProject")}}(value[key]));
          return result;
        }
        function {{name("jsonNodeToJsonString")}}(node, indented = false) {
          return {{name("jsonSerializeClr")}}({{name("jsonNodeProject")}}(node), indented);
        }
        function {{name("jsonNodeToString")}}(node) {
          return typeof {{name("jsonNodeRequired")}}(node) === "string" ? node : {{name("jsonNodeToJsonString")}}(node, true);
        }
        // Converts a tree to plain JSON values for typed deserialization.
        function {{name("jsonNodePlain")}}(value) {
          if ({{name("jsonNodeIsRaw")}}(value)) return Number(value.rawJSON);
          if (Array.isArray(value)) return value.map({{name("jsonNodePlain")}});
          if (!{{name("jsonNodeIsObject")}}(value)) return value;
          const result = Object.create(null);
          for (const key of Object.keys(value)) {{name("jsonNodeDefine")}}(result, key, {{name("jsonNodePlain")}}(value[key]));
          return result;
        }

        """;
}
