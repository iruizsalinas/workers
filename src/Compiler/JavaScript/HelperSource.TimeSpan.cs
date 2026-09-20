internal static partial class HelperSource
{
    private static string TimeSpan(Func<string, string> name) => $$"""
        const {{name("timeSpanLimit")}} = 922337203685477.6;
        function {{name("timeSpan")}}(milliseconds) {
          if (!Number.isFinite(milliseconds) || Math.abs(milliseconds) > {{name("timeSpanLimit")}})
            throw new RangeError("TimeSpan value is out of range.");
          return milliseconds;
        }
        function {{name("timeSpanCompare")}}(left, right) {
          return left < right ? -1 : left > right ? 1 : 0;
        }
        function {{name("timeSpanNegate")}}(value) {
          if (value <= -{{name("timeSpanLimit")}})
            throw new RangeError("TimeSpan value is out of range.");
          return -value;
        }
        function {{name("timeSpanDuration")}}(value) {
          return value < 0 ? {{name("timeSpanNegate")}}(value) : value;
        }

        """;
}
