namespace Workers.Compiler.Tests;

public sealed class ExtensionBlockTests
{
    [Fact]
    public void ExtensionBlockMethodsReceiveTheirReceiver() => DifferentialRunner.Equal("""
        public sealed class Box { public int V; }
        public static class Text
        {
            extension(string text)
            {
                public string Shout(int times = 1) => text.ToUpperInvariant() + (times > 1 ? "!!" : "!");
                public string Twice() => text.Shout() + text.Shout(2);
                public static string Greeting() => "hi";
                public static string Join(string left, string right) => left + "-" + right;
            }
            extension(Box box)
            {
                public void Add(int amount) { box.V += amount; }
                public int Doubled() => box.V * 2;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var box = new Box { V = 2 };
                box.Add(3);
                return new List<string>
                {
                    "abc".Shout(), "abc".Shout(times: 3), "x".Twice(),
                    string.Greeting(), string.Join("a", "b"), Text.Shout("raw", 2),
                    box.Doubled().ToString()
                };
            }
        }
        """);

    [Fact]
    public void ExtensionBlockMembersDoNotNullCheckTheirReceiver() => DifferentialRunner.Equal("""
        public static class Text
        {
            extension(string? text)
            {
                public bool IsMissing => text is null;
                public string OrEmpty() => text ?? "";
            }
        }
        public static class Test
        {
            public static object Run()
            {
                string? value = null;
                string? present = "p";
                return new List<string>
                {
                    value.IsMissing.ToString(), value.OrEmpty(), present.OrEmpty(),
                    (value?.OrEmpty() ?? "skipped"), (present?.OrEmpty() ?? "skipped")
                };
            }
        }
        """);

    [Fact]
    public void ExtensionBlockPropertiesSupportReadsWritesAndCompoundAssignment() => DifferentialRunner.Equal("""
        public sealed class Box { public int V; }
        public static class Boxes
        {
            public static int Reads;
            extension(Box box)
            {
                public int Value
                {
                    get { Reads++; return box.V; }
                    set => box.V = value * 10;
                }
                public bool IsEmpty => box.V == 0;
                public static int Created => 7;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var box = new Box();
                var empty = box.IsEmpty;
                box.Value = 1;
                var afterSet = box.V;
                box.Value += 2;
                var afterCompound = box.V;
                box.Value++;
                var assigned = (box.Value = 4);
                return new { Empty = empty, AfterSet = afterSet, AfterCompound = afterCompound, AfterIncrement = assigned,
                    Final = box.V, Reads = Boxes.Reads, Created = Box.Created, Pattern = box is { IsEmpty: false, Value: 40 } };
            }
        }
        """);

    [Fact]
    public void GenericExtensionBlocksSpecializeTheirTypeParameters() => DifferentialRunner.Equal("""
        public static class Lists
        {
            extension<T>(List<T> list)
            {
                public T Second => list[1];
                public void AddTwice(T item) { list.Add(item); list.Add(item); }
                public T FirstOr(T fallback) => list.Count == 0 ? fallback : list[0];
                public TOut Fold<TOut>(TOut seed, Func<TOut, T, TOut> step)
                {
                    var result = seed;
                    foreach (var item in list) result = step(result, item);
                    return result;
                }
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var numbers = new List<int> { 1, 2 };
                numbers.AddTwice(3);
                var words = new List<string>();
                return new
                {
                    Second = numbers.Second, Count = numbers.Count, First = words.FirstOr("none"),
                    Sum = numbers.Fold(0, (total, item) => total + item),
                    Joined = numbers.Fold("", (text, item) => text + item)
                };
            }
        }
        """);

    [Fact]
    public void ExtensionBlockReceiversAreEvaluatedOnceBeforeArguments() => DifferentialRunner.Equal("""
        public sealed class Box { public int V; }
        public static class Boxes
        {
            extension(Box box)
            {
                public int Value { get => box.V; set => box.V = value; }
                public string Tag(string suffix) => box.V + suffix;
            }
        }
        public static class Test
        {
            public static List<string> Log = new();
            private static readonly Box Shared = new() { V = 1 };
            private static Box Get() { Log.Add("receiver"); return Shared; }
            private static string Arg() { Log.Add("argument"); return "!"; }
            public static object Run()
            {
                var tag = Get().Tag(Arg());
                Get().Value += 5;
                Get().Value++;
                return new { Tag = tag, Value = Shared.V, Log };
            }
        }
        """);

    [Fact]
    public void ExtensionBlockMembersOnStructsCopyTheReceiver() => DifferentialRunner.Equal("""
        public record struct Point(int X, int Y);
        public static class Points
        {
            extension(Point point)
            {
                public int Sum => point.X + point.Y;
                public Point Shifted(int by) { point.X += by; return point; }
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var point = new Point(1, 2);
                var shifted = point.Shifted(10);
                return new { Original = point.X, Shifted = shifted.X, Sum = point.Sum };
            }
        }
        """);

    [Fact]
    public void ExtensionBlockMethodGroupsBindTheirReceiver() => DifferentialRunner.Equal("""
        public static class Text
        {
            extension(string text)
            {
                public string Wrap(string edge) => edge + text + edge;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                Func<string, string> wrap = "core".Wrap;
                return wrap("*") + new[] { "a" }.Select("b".Wrap).First();
            }
        }
        """);
}
