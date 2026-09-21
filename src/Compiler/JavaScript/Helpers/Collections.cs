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
          const values = Array.from(source);
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

}
