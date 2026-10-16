internal static partial class HelperSource
{
    private static string IndexRange(Func<string, string> name) => $$"""
        function {{name("indexCreate")}}(value, fromEnd = false) {
          if (!Number.isInteger(value) || value < 0 || value > 2147483647)
            throw new ({{name("exceptionType")}}("System.ArgumentOutOfRangeException"))("Index must be non-negative.");
          return [value, fromEnd];
        }
        function {{name("sequenceSlice")}}(source, range, array = false) {
          if (source == null) throw new ({{name("exceptionType")}}(array ? "System.ArgumentNullException" : "System.NullReferenceException"))("Source cannot be null.");
          const length = source.length;
          const start = range[0][1] ? length - range[0][0] : range[0][0];
          const end = range[1][1] ? length - range[1][0] : range[1][0];
          if (start < 0 || end < start || end > length)
            throw new ({{name("exceptionType")}}("System.ArgumentOutOfRangeException"))("Range was outside the bounds of the sequence.");
          return source.slice(start, end);
        }

        """;
}
