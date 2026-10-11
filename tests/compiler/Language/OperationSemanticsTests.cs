namespace Workers.Compiler.Tests;

public sealed class OperationSemanticsTests
{
    [Fact]
    public void CharacterArithmeticAvoidsUnnecessaryWrappers()
    {
        var module = TestCompiler.Compile("""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    char letter = 'c';
                    return Response.Json(letter - 'a');
                }
            }
            """);
        Assert.Contains("letter.charCodeAt(0)", module);
        Assert.DoesNotContain("charCodeAt(0))", module);
        Assert.DoesNotContain("(\"a\")", module);
    }

    [Fact]
    public void OutFieldsCaptureTheirReceiverBeforeTheCall() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Box current = new Box();
            private static int calls;
            private static string trace = "";
            private static Box Other() { calls++; trace += "out;"; return current; }
            private static string Input() { trace += "input;"; return "12"; }
            public static object Run()
            {
                var box = current;
                bool parsed = int.TryParse(Input(), out Other().Value);
                current = null;
                bool caught = false;
                try { int.TryParse(Input(), out Other().Value); }
                catch (System.Exception) { caught = true; }
                return new { parsed, value = box.Value, calls, trace, caught };
            }
        }
        public sealed class Box { public int Value; }
        """);

    [Fact]
    public void PreservesNameofAndConstantNumericConversions() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                char letter = 'c';
                float rounded = 16777217;
                return new { name = nameof(letter), member = nameof(string.Length), offset = letter - 'a',
                    code = (int)'\uD800', number = (double)'a', rounded, constant = 1 + 2 * 3 };
            }
        }
        """);

    [Fact]
    public void PreservesAwaitedMutationReceiversAndOutArgumentExceptions() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Counter current = new Counter { Value = 3 };
            public static Counter Current => current;
            public static async Task<object> Run()
            {
                var original = current;
                Current.Value += await Next();
                int parsed = 42;
                bool caught = false;
                try { int.TryParse(await Input(), out parsed); }
                catch (System.Exception) { caught = true; }
                return new { original = original.Value, current = current.Value, parsed, caught };
            }
            private static async Task<int> Next()
            {
                await Task.FromResult(0);
                current = new Counter { Value = 20 };
                return 5;
            }
            private static async Task<string> Input()
            {
                await Task.FromResult(0);
                throw new System.Exception("argument");
            }
        }
        public sealed class Counter { public int Value; }
        """);

    [Fact]
    public void PreservesPatternsSwitchConversionsAndThrowingExceptionFilters() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var item = new Parcel(3, "ready");
                var matched = item is Parcel(> 2, "ready") { Count: var count } && count == 3;
                var numbers = new[] { 1, 2, 3 };
                var list = numbers is [1, .. var rest] && rest.Length == 2;
                int large = 16777217;
                float selected = numbers.Length switch { 3 => large, _ => 0 };
                float[] spread = [.. new[] { large }];
                char[] text = [.. "a😀"];
                int[] units = [.. text];
                int caught = 0;
                try { throw new System.Exception("original"); }
                catch (System.Exception) when (Filter()) { caught = 1; }
                catch (System.Exception) { caught = 2; }
                return new { matched, list, selected, spread, units, caught, formatted = $"{{{matched,6}}}" };
            }
            private static bool Filter() => throw new System.Exception("filter");
        }
        public sealed record Parcel(int Count, string State);
        """);

    [Fact]
    public void PreservesPrecedenceAndNamedExtensionArguments() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                double a = 2, b = 3, c = 4;
                bool choose = true;
                bool? configured = null;
                var selected = new[] { 1, 2, 3 }.Select(selector: value => value * 2).ToArray();
                Func<int, int> increment = value => value + 1;
                Func<int, int, int> pair = (left, right) => left * 10 + right;
                return new { grouped = (a + b) * c, subtraction = a - (b - c),
                    conditional = (choose ? a : b) + c, negative = -(a + b),
                    accepted = configured ?? a == 2 || b == 4, selected, delegated = increment(4),
                    namedDelegate = pair(arg2: 2, arg1: 1) };
            }
        }
        """);

    [Fact]
    public void MutationEvaluatesAPropertyReceiverOnce() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static Counter current = new Counter();
            private static int calls;
            public static Counter Current { get { calls++; return current; } }
            public static object Run()
            {
                var previous = Current.Value++;
                Current.Value += 2;
                return new { previous, value = current.Value, calls };
            }
        }
        public sealed class Counter { public int Value; }
        """);

    [Fact]
    public void PreservesImplicitConversionsInAssignmentsArgumentsReturnsAndArrays() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                int large = 16777217;
                float assigned = large;
                assigned = large + 2;
                float[] values = [large, large + 2];
                float? optional = large;
                return new { assigned, argument = Accept(large), returned = Convert(large), values, optional,
                    code = Code('A'), literalAdd = 'A' + 1, variableDifference = Code('C') - Code('A') };
            }
            private static float Accept(float value) => value;
            private static float Convert(int value) => value;
            private static int Code(char value) => value;
        }
        """);

    [Fact]
    public void PreservesNamedArgumentsOptionalDefaultsAndReceiverOrder() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            public static object Run()
            {
                var value = Receiver().Pair(b: Mark("b", 2), a: Mark("a", 1));
                var optional = Pair(b: Mark("optional", 5));
                var constructed = new PairValue(b: Mark("ctor-b", 4), a: Mark("ctor-a", 3));
                return new { value, optional, a = constructed.A, b = constructed.B, trace };
            }
            private static Calculator Receiver() { trace += "receiver;"; return new Calculator(); }
            private static int Mark(string name, int value) { trace += name + ";"; return value; }
            private static string Pair(int a = 10, int b = 20) => $"{a}:{b}";
        }
        public sealed class Calculator { public string Pair(int a, int b) => $"{a}:{b}"; }
        public sealed class PairValue
        {
            public int A { get; }
            public int B { get; }
            public PairValue(int a, int b) { A = a; B = b; }
        }
        """);

    [Fact]
    public void ConditionalAccessUsesTheBoundReceiverAndSkipsArgumentsForNull() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static int calls;
            public static object Run()
            {
                Calculator? missing = null;
                var skipped = missing?.Add(Mark());
                var present = Receiver()?.Add(Mark());
                string? text = " AbC ";
                var chain = text?.Trim().ToLowerInvariant();
                int[]? values = [4, 5];
                var element = values?[1];
                return new { skipped, present, chain, element, calls };
            }
            private static int Mark() { calls++; return calls; }
            private static Calculator? Receiver() { calls++; return new Calculator(); }
        }
        public sealed class Calculator { public int Add(int value) => value + 10; }
        """);

    [Fact]
    public void PreservesNullableOperatorsLoopsAndExceptionRegions() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                int? missing = null;
                int? value = 3;
                bool? unknown = null;
                int total = 0;
                for (int index = 0; index < 5; index++)
                {
                    if (index == 2) continue;
                    total += index;
                }
                try { total += Divide(1, 0); }
                catch (System.Exception) { total += 10; }
                finally { total++; }
                return new { total, sum = missing + value, product = value * value,
                    less = missing < value, and = false & unknown, or = true | unknown };
            }
            private static int Divide(int left, int right) => left / right;
        }
        """);

    [Theory]
    [InlineData("checked(value + 1)")]
    [InlineData("checked(++value)")]
    [InlineData("checked(value += 1)")]
    public void RejectsUnsupportedCheckedSemanticsWithSourceLocations(string expression)
    {
        var source = $$"""
            using Workers;
            public static class Worker
            {
                [Fetch]
                public static Response Fetch(Request request, Env env, Context context)
                {
                    int value = 1;
                    return Response.Json({{expression}});
                }
            }
            """;
        var exception = Assert.Throws<NotSupportedException>(() => Compile(source));
        Assert.Contains("WRK108", exception.Message);
        Assert.Contains("(8,", global::WorkerDiagnostics.Format(exception));
    }
}
