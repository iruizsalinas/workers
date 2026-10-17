namespace Workers.Compiler.Tests;

public sealed class LinqSelectorRegressionTests
{
    [Theory]
    [InlineData("GroupBy(value => value, (Func<int, int>)null)")]
    [InlineData("ToLookup(value => value, (Func<int, int>)null)")]
    [InlineData("ToDictionary(value => value.ToString(), (Func<int, int>)null)")]
    [InlineData("ToDictionary(value => (value, value), (Func<int, int>)null)")]
    public void NullSelectorsThrowBeforeEnumeration(string operation)
    {
        DifferentialRunner.Equal($$"""
            public static class Test
            {
                public static bool Run() {
                    try { var result = new int[] { }.{{operation}}; }
                    catch (ArgumentNullException) { return true; }
                    return false;
                }
            }
            """);
    }

    [Fact]
    public void GroupingAndDictionaryOverloadsKeepTheirSelectedValues()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    var values = new int[] { 1, 2 };
                    var groups = values.GroupBy(value => value % 2).Select(group => group.ToArray()).ToArray();
                    var projectedGroups = values.GroupBy(value => value % 2, value => value * 10)
                        .Select(group => group.ToArray()).ToArray();
                    var lookup = values.ToLookup(value => value % 2);
                    var projectedLookup = values.ToLookup(value => value % 2, value => value * 10);
                    var dictionary = values.ToDictionary(value => value.ToString());
                    var projectedDictionary = values.ToDictionary(value => value.ToString(), value => value * 10);
                    var composite = values.ToDictionary(value => (value, value));
                    var projectedComposite = values.ToDictionary(value => (value, value), value => value * 10);
                    return new { groups, projectedGroups, found = lookup[1].ToArray(),
                        projectedFound = projectedLookup[1].ToArray(), dictionary, projectedDictionary,
                        composite = composite[(1, 1)], projectedComposite = projectedComposite[(1, 1)] };
                }
            }
            """);
    }
}
