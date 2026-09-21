internal static partial class HelperSource
{
    private static string IntegerDivide(Func<string, string> name) => $$"""
        function {{name("integerDivide")}}(left, right, unsigned) {
          if (right === 0) throw new RangeError("Integer division by zero.");
          if (!unsigned && left === -2147483648 && right === -1) {
            throw new RangeError("Integer division overflow.");
          }
          const value = Math.trunc(left / right);
          return unsigned ? value >>> 0 : value | 0;
        }

        """;

    private static string IntegerRemainder(Func<string, string> name) => $$"""
        function {{name("integerRemainder")}}(left, right, unsigned) {
          if (right === 0) throw new RangeError("Integer division by zero.");
          const value = left % right;
          return unsigned ? value >>> 0 : value | 0;
        }

        """;

    private static string RandomNext(Func<string, string> name) => $$"""
        function {{name("randomNext")}}(minimum, maximum) {
          if (minimum > maximum) throw new RangeError("Minimum cannot exceed maximum.");
          if (minimum === maximum) return minimum;
          return Math.floor(Math.random() * (maximum - minimum)) + minimum;
        }

        """;

    private static string SetAdd(Func<string, string> name) => $$"""
        function {{name("setAdd")}}(set, value) {
          if (set.has(value)) return false;
          set.add(value);
          return true;
        }

        """;

}
