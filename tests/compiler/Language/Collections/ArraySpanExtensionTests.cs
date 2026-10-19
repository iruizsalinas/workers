namespace Workers.Compiler.Tests;

// C# 14 first-class spans bind array.Contains(x) and friends to System.MemoryExtensions.
// A null array converts to an empty span, so these calls never throw for a null receiver.
public sealed class ArraySpanExtensionTests
{
    [Fact]
    public void ContainsAndIndexOfUseElementEquality() => DifferentialRunner.Equal("""
        public readonly record struct Point(int X, int Y);
        public sealed record Tag(string Name);
        public static class Test
        {
            public static object Run()
            {
                string[] allowed = ["https://a.test", "https://b.test"];
                var numbers = new[] { 3, 1, 3, 2 };
                var points = new[] { new Point(1, 2), new Point(3, 4) };
                var tags = new[] { new Tag("x"), new Tag("y") };
                var letters = "hello".ToCharArray();
                var doubles = new[] { double.NaN, 1.5 };
                return new
                {
                    Allowed = allowed.Contains("https://b.test"), Denied = allowed.Contains("https://c.test"),
                    Index = numbers.IndexOf(3), Last = numbers.LastIndexOf(3), Missing = numbers.IndexOf(9),
                    Point = points.Contains(new Point(3, 4)), PointIndex = points.IndexOf(new Point(3, 4)),
                    Tag = tags.Contains(new Tag("y")), Letter = letters.Contains('l'), LetterIndex = letters.LastIndexOf('l'),
                    NaN = doubles.Contains(double.NaN), Inline = new[] { "GET", "HEAD" }.Contains("HEAD")
                };
            }
        }
        """);

    [Fact]
    public void SequenceComparisonsCompareElements() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var digest = new byte[] { 1, 2, 3, 4 };
                var expected = new byte[] { 1, 2, 3, 4 };
                var other = new byte[] { 1, 2, 9, 4 };
                var words = new[] { "a", "b", "c" };
                return new
                {
                    Bytes = digest.SequenceEqual(expected), Different = digest.SequenceEqual(other),
                    Shorter = digest.SequenceEqual(new byte[] { 1, 2 }),
                    Starts = words.StartsWith(new[] { "a", "b" }), NotStarts = words.StartsWith(new[] { "b" }),
                    Ends = words.EndsWith(new[] { "b", "c" }), TooLong = words.EndsWith(new[] { "z", "a", "b", "c" }),
                    Words = words.SequenceEqual(new[] { "a", "b", "c" })
                };
            }
        }
        """);

    [Fact]
    public void NullArraysBehaveAsEmptySpans() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                int[]? missing = null;
                string[]? none = null;
                return new
                {
                    Contains = missing.Contains(1), Index = missing.IndexOf(1), Last = none.LastIndexOf("a"),
                    Equal = missing.SequenceEqual(new int[0]), Starts = none.StartsWith(new string[0])
                };
            }
        }
        """);

    [Fact]
    public void ReceiversAndArgumentsAreEvaluatedOnceInOrder() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static readonly List<string> Log = new();
            private static int[] Items() { Log.Add("items"); return new[] { 1, 2, 3 }; }
            private static int Value() { Log.Add("value"); return 2; }
            public static object Run()
            {
                var found = Items().Contains(Value());
                var index = Items().IndexOf(Value());
                return new { Found = found, Index = index, Log };
            }
        }
        """);
}
