namespace Workers.Compiler.Tests;

// Mutable record structs are values: assignment, arguments, returns and collection reads copy them.
public sealed class RecordStructValueSemanticsTests
{
    private const string Point = """
        public record struct Point(int X, int Y);
        """;

    [Fact]
    public void AssignmentArgumentsAndReturnsCopyTheValue() => DifferentialRunner.Equal(Point + """
        public static class Extensions { public static Point Shift(this Point p, int by) { p.X += by; return p; } }
        public sealed class Holder { public Point Field = new(5, 5); }
        public static class Test
        {
            private static Point Move(Point p) { p.X += 10; return p; }
            private static Point Stored(Holder holder) => holder.Field;
            public static object Run()
            {
                var a = new Point(1, 2);
                var b = a;
                b.X = 99;
                var moved = Move(a);
                var shifted = a.Shift(5);
                var holder = new Holder();
                var fromMethod = Stored(holder);
                fromMethod.X = 0;
                Func<Point> capture = () => a;
                var captured = capture();
                captured.Y = 50;
                return new { A = a.X, AY = a.Y, B = b.X, Moved = moved.X, Shifted = shifted.X, Stored = holder.Field.X, CapturedY = captured.Y };
            }
        }
        """);

    [Fact]
    public void CollectionReadsCopyButArrayElementsAreVariables() => DifferentialRunner.Equal(Point + """
        public static class Test
        {
            public static object Run()
            {
                var list = new List<Point> { new(1, 1) };
                var fromList = list[0];
                fromList.X = 7;
                var array = new[] { new Point(1, 1) };
                array[0].X = 8;
                array[0].Y += 1;
                var fromArray = array[0];
                fromArray.X = 100;
                var map = new Dictionary<string, Point> { ["a"] = new(1, 1) };
                var fromMap = map["a"];
                fromMap.X = 9;
                var sum = 0;
                foreach (var item in list) { var local = item; local.X = 1000; sum += item.X; }
                var source = new Point(3, 3);
                list.Add(source);
                source.X = 4;
                return new { List = list[0].X, Added = list[1].X, Array = array[0].X, Map = map["a"].X, Sum = sum };
            }
        }
        """);

    [Fact]
    public void MutationsChangeVariablesAndFieldsButNotPropertyCopies() => DifferentialRunner.Equal(Point + """
        public sealed class Holder
        {
            public Point Field = new(1, 1);
            public Point Property { get; set; } = new(1, 1);
        }
        public static class Test
        {
            public static object Run()
            {
                var local = new Point(1, 1);
                local.X++;
                var holder = new Holder();
                holder.Field.X++;
                holder.Field.X += 10;
                var property = holder.Property;
                property.X++;
                var before = holder.Property.X;
                holder.Property = property;
                property.X = 500;
                var list = new List<Point> { new(1, 1) };
                var item = list[0];
                item.X++;
                return new { Local = local.X, Field = holder.Field.X, Before = before, Property = holder.Property.X, List = list[0].X };
            }
        }
        """);

    [Fact]
    public void NestedValueMembersAreCopiedWithTheirContainer() => DifferentialRunner.Equal(Point + """
        public record struct Segment(Point Start, Point End);
        public sealed class Holder { public Segment Value = new(new Point(1, 1), new Point(2, 2)); }
        public static class Test
        {
            public static object Run()
            {
                var holder = new Holder();
                var first = holder.Value;
                var second = first;
                var start = second.Start;
                start.X = 10;
                second.Start = start;
                second.End = new Point(20, 20);
                holder.Value.Start = new Point(3, 3);
                var changed = first with { End = new Point(5, 5) };
                Segment? maybe = first;
                var unwrapped = maybe.Value;
                unwrapped.End = new Point(77, 77);
                return new
                {
                    FirstStart = first.Start.X, FirstEnd = first.End.X, SecondStart = second.Start.X, SecondEnd = second.End.X,
                    HolderStart = holder.Value.Start.X, ChangedEnd = changed.End.X, ChangedStart = changed.Start.X,
                    Maybe = maybe.Value.End.X, Equal = first == new Segment(new Point(1, 1), new Point(2, 2))
                };
            }
        }
        """);
}
