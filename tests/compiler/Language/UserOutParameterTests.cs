namespace Workers.Compiler.Tests;

public sealed class UserOutParameterTests
{
    [Fact]
    public void FromEndOutReferencesValidateBoundsAndCaptureTheirLocationBeforeLaterArguments() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            private static System.Index position = ^1;
            private static int Input() { calls++; position = 0; return 7; }
            private static void Write(out int value, int input) { value = input; }
            public static object Run()
            {
                var values = new[] { 1, 2, 3 };
                var caught = false;
                try { Write(out values[^0], Input()); }
                catch (System.IndexOutOfRangeException) { caught = true; }
                Write(out values[position], Input());
                return new { values, caught, calls };
            }
        }
        """);

    [Fact]
    public void FromEndOutArgumentsWriteTheSelectedArrayElement() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static void Write(out int value) { value = 7; }
            public static object Run()
            {
                var values = new[] { 1, 2, 3 };
                Write(out values[^1]);
                System.Index first = 0;
                System.Index middle = ^2;
                Write(out values[first]);
                Write(out values[middle]);
                return values;
            }
        }
        """);

    [Fact]
    public void NestedTupleOutReferencesPreserveAliasingAndWritesBeforeExceptions() => DifferentialRunner.Equal("""
        public sealed class Box { public ((int, int), int) Value = ((1, 2), 3); }
        public static class Test
        {
            private static void Change(out int first, out int second)
            {
                first = 4;
                second = 5;
                first += second;
                throw new System.Exception("stop");
            }
            public static object Run()
            {
                var box = new Box();
                var values = new[] { ((1, 2), 3) };
                try { Change(out box.Value.Item1.Item2, out box.Value.Item1.Item2); }
                catch (System.Exception) { }
                try { Change(out values[0].Item1.Item2, out values[0].Item1.Item2); }
                catch (System.Exception) { }
                return new { field = box.Value.Item1.Item2, array = values[0].Item1.Item2 };
            }
        }
        """);

    [Fact]
    public void TupleFieldOutArgumentsValidateTheirStorageBeforeLaterArguments() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            private static int Input() { trace += "argument;"; return 7; }
            private static void Change(out int value, int input) { trace += "method;"; value = input; }
            public static object Run()
            {
                var values = new[] { (1, 2) };
                var caught = false;
                try { Change(out values[2].Item1, Input()); }
                catch (System.IndexOutOfRangeException) { caught = true; }
                return new { caught, trace, first = values[0].Item1 };
            }
        }
        """);

    [Fact]
    public void DeclaresOutLocalsInExpressionBodiedGettersAndConstructors() => DifferentialRunner.Equal("""
        public sealed class Item
        {
            private int initial;
            public Item() => initial = Read(out var value) ? value : 0;
            public bool HasValue => Read(out var value) && value == initial;
            public static bool HasStaticValue => Read(out var value) && value == 7;
            private static bool Read(out int value) { value = 7; return true; }
        }
        public static class Test
        {
            public static object Run() => new { a = new Item().HasValue, b = Item.HasStaticValue };
        }
        """);
    [Fact]
    public void SupportsDeclarationsExistingLocalsDiscardsAndForwarding() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static bool TryValue(int input, out int value) { value = input + 1; return input > 0; }
            private static bool Forward(int input, out int value) => TryValue(input, out value);
            public static object Run()
            {
                var a = Forward(4, out var first);
                int second = 7;
                var b = TryValue(-1, out second);
                var c = TryValue(2, out _);
                return new { a, first, b, second, c };
            }
        }
        """);

    [Fact]
    public void PreservesAliasingAndWritesBeforeExceptions() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int shared;
            private static void Change(out int first, out int second)
            {
                first = 1; second = 2; first += second; throw new System.Exception("stop");
            }
            public static object Run()
            {
                int value = 0;
                try { Change(out value, out value); } catch (System.Exception) { }
                try { Change(out shared, out shared); } catch (System.Exception) { }
                return new { value, shared };
            }
        }
        """);

    [Fact]
    public void CapturesFieldsAndArrayLocationsInSourceOrder() => DifferentialRunner.Equal("""
        public sealed class Box { public int Value; }
        public static class Test
        {
            private static string trace = "";
            private static Box box = new Box();
            private static Box Receiver() { trace += "r"; return box; }
            private static int Index() { trace += "i"; return 0; }
            private static int Input() { trace += "a"; return 7; }
            private static bool TryValue(out int value, int input) { trace += "m"; value = input; return true; }
            public static object Run()
            {
                var values = new int[1];
                TryValue(input: Input(), value: out Receiver().Value);
                TryValue(out values[Index()], Input());
                return new { field = box.Value, array = values[0], trace };
            }
        }
        """);
}
