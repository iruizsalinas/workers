namespace Workers.Compiler.Tests;

public sealed class CollectionInterfaceRegressionTests
{
    [Fact]
    public void CollectionCountPatternsFollowNativeContainerRepresentations()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    IReadOnlyCollection<int> set = new HashSet<int> { 1, 2 };
                    IReadOnlyCollection<KeyValuePair<string, int>> dictionary = new Dictionary<string, int> { ["length"] = 17 };
                    return new { set = set is { Count: 2 }, dictionary = dictionary is { Count: 1 } };
                }
            }
            """);
    }

    [Fact]
    public void InterfaceEnumerationUsesDictionaryEntriesAndUtf16Characters()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    IEnumerable<KeyValuePair<string, int>> pairs = new Dictionary<string, int> { ["a"] = 2, ["b"] = 3 };
                    IEnumerable<char> text = "😀";
                    var values = new List<int>();
                    foreach (var pair in pairs) values.Add(pair.Value);
                    foreach (var character in text) values.Add((int)character);
                    return values.ToArray();
                }
            }
            """);
    }

    [Fact]
    public void CollectionCountsFollowTheirNativeRepresentations()
    {
        DifferentialRunner.Equal("""
            public static class Test
            {
                public static object Run() {
                    ICollection<int> array = new[] { 1, 2 };
                    ICollection<int> list = new List<int> { 1, 2, 3 };
                    ICollection<int> set = new HashSet<int> { 1, 2 };
                    IReadOnlyCollection<int> queue = new Queue<int>(new[] { 1, 2 });
                    IReadOnlyCollection<int> stack = new Stack<int>(new[] { 1, 2 });
                    IReadOnlyCollection<KeyValuePair<string, int>> text = new Dictionary<string, int> { ["length"] = 17, ["size"] = 29 };
                    IReadOnlyCollection<KeyValuePair<int, int>> numbers = new Dictionary<int, int> { [1] = 2 };
                    return new { array = array.Count, list = list.Count, set = set.Count,
                        queue = queue.Count, stack = stack.Count, text = text.Count, numbers = numbers.Count };
                }
            }
            """);
    }
}
