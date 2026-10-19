namespace Workers.Compiler.Tests;

public sealed class ListPatternLengthRegressionTests
{
    [Theory]
    [InlineData("List<Item>")]
    [InlineData("IReadOnlyList<Item>")]
    public void ListPatternsUseTheirInitialLengthForTrailingElements(string type) => DifferentialRunner.Equal($$"""
        public sealed class Item
        {
            public int Value;
            public Action OnRead;
            public bool Ready { get { OnRead(); return true; } }
        }
        public static class Test
        {
            public static object Run()
            {
                var list = new List<Item> { new Item { Value = 1 }, new Item { Value = 3 } };
                {{type}} values = list;
                values[0].OnRead = () => list.Add(new Item { Value = 9 });
                var matched = values is [{ Ready: true }, .., { Value: 3 }];
                {{type}} missing = null;
                var nullMatched = missing is [..];
                return new { matched, nullMatched, count = values.Count };
            }
        }
        """);
}
