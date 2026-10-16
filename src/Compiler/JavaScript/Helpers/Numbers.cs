internal static partial class HelperSource
{
    private static string IntegerDivide(Func<string, string> name) => $$"""
        function {{name("integerDivide")}}(left, right, unsigned) {
          if (right === 0) throw new ({{name("exceptionType")}}("System.DivideByZeroException"))("Integer division by zero.");
          if (!unsigned && left === -2147483648 && right === -1) {
            throw new ({{name("exceptionType")}}("System.OverflowException"))("Integer division overflow.");
          }
          const value = Math.trunc(left / right);
          return unsigned ? value >>> 0 : value | 0;
        }

        """;

    private static string IntegerRemainder(Func<string, string> name) => $$"""
        function {{name("integerRemainder")}}(left, right, unsigned) {
          if (right === 0) throw new ({{name("exceptionType")}}("System.DivideByZeroException"))("Integer division by zero.");
          const value = left % right;
          return unsigned ? value >>> 0 : value | 0;
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
