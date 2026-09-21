internal static partial class HelperSource
{
    private static string StringTrim(Func<string, string> name) => $$"""
        function {{name("stringTrim")}}(source) {
          return source.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/gu, "");
        }

        """;

    private static string StringContains(Func<string, string> name) => $$"""
        function {{name("stringContains")}}(source, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          return source.includes(value);
        }

        """;

    private static string StringStartsWith(Func<string, string> name) => $$"""
        function {{name("stringStartsWith")}}(source, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          return source.startsWith(value);
        }

        """;

    private static string StringEndsWith(Func<string, string> name) => $$"""
        function {{name("stringEndsWith")}}(source, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          return source.endsWith(value);
        }

        """;

    private static string StringSubstring(Func<string, string> name) => $$"""
        function {{name("stringSubstring")}}(source, start, length) {
          if (start < 0 || start > source.length || length != null && (length < 0 || start + length > source.length))
            throw new RangeError("Substring range is outside the string.");
          return length == null ? source.slice(start) : source.slice(start, start + length);
        }

        """;

    private static string StringReplace(Func<string, string> name) => $$"""
        function {{name("stringReplace")}}(source, oldValue, newValue) {
          if (oldValue == null) throw new TypeError("Value cannot be null.");
          if (oldValue.length === 0) throw new RangeError("Value cannot be empty.");
          return source.replaceAll(oldValue, newValue ?? "");
        }

        """;

    private static string StringIsNullOrEmpty(Func<string, string> name) => $$"""
        function {{name("stringIsNullOrEmpty")}}(value) {
          return value == null || value.length === 0;
        }

        """;

    private static string StringIsNullOrWhiteSpace(Func<string, string> name) => $$"""
        function {{name("stringIsNullOrWhiteSpace")}}(value) {
          return value == null || /^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]*$/u.test(value);
        }

        """;

    private static string StringJoin(Func<string, string> name) => $$"""
        function {{name("stringJoin")}}(separator, values) {
          if (values == null) throw new TypeError("Values cannot be null.");
          return Array.from(values, value => value ?? "").join(separator ?? "");
        }

        """;

    private static string StringOrdinal(Func<string, string> name) => $$"""
        function {{name("stringOrdinalFold")}}(value) {
          let result = "";
          for (const character of value) {
            const upper = character.toUpperCase();
            result += upper.length === character.length ? upper : character;
          }
          return result;
        }
        function {{name("stringOrdinal")}}(source, value, ignoreCase, operation, start, count) {
          if (operation === 6) {
            if (source == null) throw new TypeError("String receiver cannot be null.");
            if (value == null) return false;
          }
          if (operation === 0) {
            if (source == null || value == null) return source == null && value == null;
          } else if (source == null || value == null) {
            throw new TypeError("Value cannot be null.");
          }
          if (ignoreCase) {
            source = {{name("stringOrdinalFold")}}(source);
            value = {{name("stringOrdinalFold")}}(value);
          }
          if (operation === 0 || operation === 6) return source === value;
          if (operation === 1) return source.includes(value);
          if (operation === 2) return source.startsWith(value);
          if (operation === 3) return source.endsWith(value);
          if (operation === 5) return source.lastIndexOf(value);
          const offset = start ?? 0;
          const length = count ?? source.length - offset;
          if (offset < 0 || offset > source.length || length < 0 || offset + length > source.length)
            throw new RangeError("Search range is outside the string.");
          const found = source.slice(offset, offset + length).indexOf(value);
          return found < 0 ? -1 : offset + found;
        }

        """;

    private static string StringRemove(Func<string, string> name) => $$"""
        function {{name("stringRemove")}}(source, start, count) {
          if (start < 0 || start > source.length || count != null && (count < 0 || start + count > source.length))
            throw new RangeError("Remove range is outside the string.");
          return count == null ? source.slice(0, start) : source.slice(0, start) + source.slice(start + count);
        }

        """;

    private static string StringInsert(Func<string, string> name) => $$"""
        function {{name("stringInsert")}}(source, index, value) {
          if (value == null) throw new TypeError("Value cannot be null.");
          if (index < 0 || index > source.length) throw new RangeError("Insert index is outside the string.");
          return source.slice(0, index) + value + source.slice(index);
        }

        """;

    private static string StringPad(Func<string, string> name) => $$"""
        function {{name("stringPad")}}(source, width, character, left) {
          if (width < 0) throw new RangeError("Padding width cannot be negative.");
          return left ? source.padStart(width, character) : source.padEnd(width, character);
        }

        """;

    private static string StringToCharArray(Func<string, string> name) => $$"""
        function {{name("stringToCharArray")}}(source, start, length) {
          const offset = start ?? 0;
          const count = length ?? source.length;
          if (offset < 0 || offset > source.length || count < 0 || offset + count > source.length)
            throw new RangeError("Character range is outside the string.");
          return source.slice(offset, offset + count).split("");
        }

        """;

    private static string StringSplit(Func<string, string> name) => $$"""
        function {{name("stringSplit")}}(source, separator, options) {
          if (separator == null || separator.length !== 1)
            throw new TypeError("Only a single character separator is supported.");
          if ((options & ~3) !== 0) throw new RangeError("Split options are out of range.");
          let values = source.split(separator);
          if ((options & 2) !== 0) values = values.map(value =>
            value.replace(/^[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+|[\u0009-\u000d\u0020\u0085\u00a0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]+$/gu, ""));
          if ((options & 1) !== 0) values = values.filter(value => value.length !== 0);
          return values;
        }

        """;
}
