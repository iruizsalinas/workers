namespace Workers.Compiler.Tests;

public sealed class ConstructionRegressionTests
{
    [Fact]
    public void ExpressionBodiedRecordCopyConstructorsCanDeclareOutVariables() => DifferentialRunner.Equal("""
        public sealed record Item
        {
            public int Value { get; init; }
            public Item(int value) { Value = value; }
            private Item(Item original) => Value = Read(original.Value, out var value) ? value : 0;
            private static bool Read(int input, out int value) { value = input + 1; return true; }
        }
        public static class Test
        {
            public static object Run()
            {
                var original = new Item(3);
                var copy = original with { };
                return new { original = original.Value, copied = copy.Value };
            }
        }
        """);

    [Fact]
    public void PositionalRecordPropertiesCopyTupleParametersBeforeOtherInitializers() => DifferentialRunner.Equal("""
        public sealed record Item((int, int) Value)
        {
            public int Changed { get; } = ++Value.Item1;
        }
        public static class Test
        {
            public static object Run()
            {
                var original = (1, 2);
                var item = new Item(original);
                return new { stored = item.Value.Item1, changed = item.Changed, original = original.Item1 };
            }
        }
        """);

    [Fact]
    public void AwaitsObjectInitializersAfterConstructingTheInstance() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            public static void Mark() { trace += "constructed;"; }
            public static async Task<object> Run()
            {
                var value = new Box { Value = await Next() };
                return new { value = value.Value, trace };
            }
            private static async Task<int> Next() { trace += "awaited;"; return await Task.FromResult(7); }
        }
        public sealed class Box
        {
            public int Value { get; set; }
            public Box() { Test.Mark(); }
        }
        """);

    [Fact]
    public void WithExpressionsCloneBeforeEvaluatingInitializers() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Parcel original = new Parcel { First = 1, Second = 2 };
            public static object Run()
            {
                var copy = original with { First = ChangeOriginal() };
                return new { first = copy.First, second = copy.Second, original = original.Second };
            }
            private static int ChangeOriginal() { original.Second = 20; return 3; }
        }
        public sealed record Parcel
        {
            public int First { get; set; }
            public int Second { get; set; }
        }
        """);

    [Fact]
    public void WithExpressionsSupportAwaitedInitializers() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var original = new Parcel(1, 2);
                var copy = original with { First = await Task.FromResult(3) };
                return new { first = copy.First, second = copy.Second, original = original.First };
            }
        }
        public sealed record Parcel(int First, int Second);
        """);

    [Fact]
    public void FieldAndPropertyInitializersRunInDeclarationOrder() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            public static int Mark(int value) { trace += value; return value; }
            public static object Run()
            {
                var box = new Box();
                return new { trace, total = box.First + box.Second + box.Third };
            }
        }
        public sealed class Box
        {
            public int First = Test.Mark(1);
            public int Second { get; } = Test.Mark(2);
            public int Third = Test.Mark(3);
        }
        """);

    [Fact]
    public void RecordMembersWithJavaScriptSpecialNamesKeepValueSemantics() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var original = new Names("first", "ctor");
                var different = new Names("second", "other");
                var copy = original with { __proto__ = "updated", Constructor = "copied" };
                return new { equal = original == different, originalText = original.ToString(),
                    copiedName = copy.__proto__, copiedConstructor = copy.Constructor,
                    originalName = original.__proto__ };
            }
        }
        public sealed record Names(string __proto__, string Constructor);
        """);
}
