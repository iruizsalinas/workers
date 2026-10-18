namespace Workers.Compiler.Tests;

public sealed class TupleAssignmentRegressionTests
{
    [Theory]
    [InlineData("values[2].Item1 = Input();")]
    [InlineData("(values[2].Item1, var other) = (Input(), 3);")]
    public void TupleFieldAssignmentsValidateArrayStorageBeforeEvaluatingTheValue(string assignment) => DifferentialRunner.Equal($$"""
        public static class Test
        {
            private static string trace = "";
            private static int Input() { trace += "value;"; return 7; }
            public static object Run()
            {
                var values = new[] { (1, 2) };
                var caught = false;
                try { {{assignment}} }
                catch (System.IndexOutOfRangeException) { caught = true; }
                return new { caught, trace, first = values[0].Item1 };
            }
        }
        """);
}
