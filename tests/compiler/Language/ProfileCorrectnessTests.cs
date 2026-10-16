namespace Workers.Compiler.Tests;

public sealed class ProfileCorrectnessTests
{
    [Fact]
    public void GeneratedCastCapacityAndNullableChecksUseTypedExceptions() => DifferentialRunner.Equal("""
        public sealed class Box { }
        public static class Test
        {
            public static object Run()
            {
                object other = "text"; var cast = false; var capacity = false; var nullable = false;
                try { var box = (Box)other; } catch (InvalidCastException) { cast = true; }
                var size = -1;
                try { var list = new List<int>(size); } catch (ArgumentOutOfRangeException) { capacity = true; }
                try { foreach (int value in new int?[] { null }) { } } catch (InvalidOperationException) { nullable = true; }
                return new { cast, capacity, nullable };
            }
        }
        """);
    [Fact]
    public void FrameworkArgumentFailuresKeepTheirDistinctExceptionTypes() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string Catch(Action action)
            {
                try { action(); return "none"; }
                catch (ArgumentNullException) { return "null"; }
                catch (ArgumentOutOfRangeException) { return "range"; }
                catch (ArgumentException) { return "argument"; }
            }
            public static object Run()
            {
                var list = new List<int> { 1, 2 };
                return new { tasks = Catch(() => { Task.WhenAll(new Task[] { null! }); }),
                    nullTasks = Catch(() => { Task.WhenAll((Task[])null!); }),
                    negative = Catch(() => { list.GetRange(-1, 1); }), beyond = Catch(() => { list.GetRange(1, 3); }) };
            }
        }
        """);
    [Fact]
    public void NumericParsingMatchesTheSupportedDecimalGrammar() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string ParseDouble(string text)
            {
                try { var value = double.Parse(text); return value != value ? "nan" : value.ToString(); }
                catch (FormatException) { return "format"; }
            }
            private static string ParseInt(string text)
            {
                try { return int.Parse(text).ToString(); } catch (FormatException) { return "format"; }
            }
            public static object Run() => new { doubles = new[] { "NaN", "nan", "0x10", "1,234", "1.25e2", "1_0", "\u00a01" }.Select(ParseDouble).ToArray(),
                integers = new[] { "\u00a01", "\u00851", "\uFEFF1", " 1 ", "1\0", "\0" }.Select(ParseInt).ToArray() };
        }
        """);
    [Fact]
    public void QuerySyntaxCanCallSourceDefinedQueryOperators() => DifferentialRunner.Equal("""
        public sealed class Query(int value)
        {
            public Query Where(Func<int, bool> predicate) => new(predicate(value) ? value : 0);
            public T Select<T>(Func<int, T> selector) => selector(value);
        }
        public static class Test
        {
            public static object Run()
            {
                var query = from value in new Query(3) where value > 1 select value * 2;
                return query;
            }
        }
        """);

    [Fact]
    public void PlainRecordCopiesKeepRuntimeTypeWithoutLeakingMetadata() => DifferentialRunner.Equal("""
        public sealed record Row(int Value);
        public static class Test
        {
            public static object Run()
            {
                var original = new Row(1); object copy = original with { Value = 2 };
                var value = copy switch { Row row => row.Value, _ => -1 };
                return new { value, type = copy is Row, text = $"{copy}" };
            }
        }
        """);
    [Fact]
    public void CheckedNarrowBitwiseAssignmentsThrowBeforeWriting() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                byte value = 255; var mask = 255; var caught = false;
                try { checked { value <<= mask; } } catch (OverflowException) { caught = true; }
                var shifted = false; byte number = 255;
                try { checked { number <<= 2; } } catch (OverflowException) { shifted = true; }
                return new { value, caught, shifted, number };
            }
        }
        """);

    [Fact]
    public void NullableValuesAndNullThrowsUseTheirSpecifiedExceptionTypes() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var nullable = false; var nullThrow = false;
                int? value = null;
                try { var read = value.Value; } catch (InvalidOperationException) { nullable = true; }
                try { throw null!; } catch (NullReferenceException) { nullThrow = true; }
                return new { nullable, nullThrow };
            }
        }
        """);
    [Fact]
    public void QueryTransparentIdentifiersPreservePropertyNamesAndDeferredCaptures() => DifferentialRunner.Equal("""
        public sealed record Item(int Value);
        public static class Test
        {
            public static object Run()
            {
                var minimum = 1;
                var query = from item in new[] { new Item(1), new Item(3) }
                    let Pascal = item.Value * 2 where Pascal > minimum select Pascal;
                minimum = 4;
                var explicitName = new { Pascal = 7 };
                return new { result = query.ToArray(), value = explicitName.Pascal };
            }
        }
        """);

    [Fact]
    public void NestedIndexerInitializersCaptureArgumentsAndRepeatGetterCalls() => DifferentialRunner.Equal("""
        public sealed class Config
        {
            public static string Trace = "";
            private List<int> values = new();
            public List<int> this[int index] { get { Trace += "g"; return values; } }
        }
        public static class Test
        {
            private static int Index() { Config.Trace += "i"; return 0; }
            public static object Run()
            {
                var config = new Config { [Index()] = { 1, 2 } };
                var trace = Config.Trace;
                return new { trace, items = config[0].ToArray() };
            }
        }
        """);
    [Fact]
    public void BitwiseAssignmentsSupportNarrowEnumsAndLiftedBooleanRules() => DifferentialRunner.Equal("""
        [Flags] public enum Bits : byte { A = 1, B = 2 }
        public static class Test
        {
            public static object Run()
            {
                byte value = 255; value <<= 2; value ^= 3;
                char character = 'a'; character &= (char)127;
                var flags = Bits.A; flags |= Bits.B;
                int? number = 3; number <<= 2; number &= null;
                bool? first = false; first &= null;
                bool? second = null; second |= true;
                bool? third = true; third ^= null;
                return new { value, character, flags = (int)flags, number, first, second, third };
            }
        }
        """);
    [Fact]
    public void ExceptionConstructorsCaptureNamedArgumentsAndKeepInnerExceptions() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string trace = "";
            private static string Text(string value) { trace += value; return value; }
            private static Exception Inner() { trace += "i"; return new FormatException("inner"); }
            public static object Run()
            {
                var argument = new ArgumentException(paramName: Text("p"), message: Text("m"), innerException: Inner());
                var caught = false; var filtered = false;
                try { throw argument; }
                catch (SystemException exception) when (exception is ArgumentException) { caught = true; }
                try { int.Parse("bad"); }
                catch (FormatException) when (false) { }
                catch (SystemException) { filtered = true; }
                return new { trace, caught, filtered, parameter = argument.ParamName, message = argument.Message,
                    inner = argument.InnerException is FormatException, innerMessage = argument.InnerException!.Message };
            }
        }
        """);

    [Fact]
    public void QueriesSupportJoinsGroupsAndContinuations() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { 1, 2, 3, 4 };
                var joined = from a in values join b in new[] { 2, 4 } on a equals b select a * b;
                var grouped = from a in values group a by a % 2 into g orderby g.Key select g.Sum();
                var continued = from a in values select a * 2 into doubled where doubled > 4 select doubled;
                return new { joined = joined.ToArray(), grouped = grouped.ToArray(), continued = continued.ToArray() };
            }
        }
        """);

    [Fact]
    public void TypeOfOpenGenericAndArrayTypesKeepDistinctIdentities() => DifferentialRunner.Equal("""
        public sealed class FalseType { }
        public static class Test
        {
            public static object Run() => new { name = typeof(Dictionary<,>).Name,
                array = typeof(int[]) != typeof(int[,]), jagged = typeof(int[][]).Name, literal = typeof(FalseType).Name,
                generic = typeof(Dictionary<,>).IsGenericType };
        }
        """);
    [Fact]
    public void BuiltInExceptionCatchesUseHelperTypesAndInheritance() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string Read(Action action)
            {
                try { action(); return "none"; }
                catch (ArgumentNullException) { return "null"; }
                catch (ArgumentOutOfRangeException) { return "range"; }
                catch (ArgumentException) { return "argument"; }
                catch (FormatException) { return "format"; }
                catch (OverflowException) { return "overflow"; }
                catch (InvalidOperationException) { return "operation"; }
                catch (KeyNotFoundException) { return "key"; }
                catch (IndexOutOfRangeException) { return "index"; }
                catch (DivideByZeroException) { return "zero"; }
                catch (Exception) { return "other"; }
            }
            public static object Run()
            {
                var dictionary = new Dictionary<string, int> { ["x"] = 1 };
                var divisor = 0;
                return new { empty = Read(() => { new int[0].First(); }), many = Read(() => { new[] { 1, 2 }.Single(); }),
                    format = Read(() => { int.Parse("bad"); }), overflow = Read(() => { int.Parse("999999999999999"); }),
                    nil = Read(() => { int.Parse(null!); }), duplicate = Read(() => { dictionary.Add("x", 2); }),
                    key = Read(() => { var value = dictionary["y"]; }), list = Read(() => { var value = new List<int>()[0]; }),
                    array = Read(() => { var value = (new int[0])[0]; }), zero = Read(() => { var value = 1 / divisor; }),
                    argument = Read(() => { throw new ArgumentException("bad"); }), derived = Read(() => { throw new ArgumentNullException("x"); }) };
            }
        }
        """);
    [Fact]
    public void TypeOfUsesSpecializedAndInternedMetadata() => DifferentialRunner.Equal("""
        public sealed class Item<T> { }
        public static class Test
        {
            private static Type Type<T>() => typeof(T);
            public static object Run()
            {
                var type = Type<Item<int>>();
                return new { name = type.Name, ns = type.Namespace, generic = type.IsGenericType, same = type == typeof(Item<int>),
                    different = type != typeof(Item<string>), integer = Type<int>().Name, array = typeof(int[,]).Name,
                    text = typeof(int).ToString(), iface = typeof(IDisposable).IsInterface, value = typeof(int).IsValueType };
            }
        }
        """);
    [Fact]
    public void NestedCollectionInitializersUseExistingStorageAndRepeatGetters() => DifferentialRunner.Equal("""
        public sealed class Config
        {
            public static int Reads;
            private List<string> tags = new();
            public List<string> Tags { get { Reads++; return tags; } }
            public Dictionary<string, int> Values { get; } = new();
            public HashSet<int> Numbers { get; } = new();
            public Child Child { get; } = new();
        }
        public sealed class Child { public List<int> Items { get; } = new(); public int Value { get; set; } }
        public static class Test
        {
            public static object Run()
            {
                var config = new Config { Tags = { "a", "b" }, Values = { { "x", 2 } }, Numbers = { 1, 1, 2 }, Child = { Items = { 3, 4 }, Value = 7 } };
                var reads = Config.Reads;
                return new { reads, tags = config.Tags.ToArray(), value = config.Values["x"], numbers = config.Numbers.Count, child = config.Child.Value, items = config.Child.Items.ToArray() };
            }
        }
        """);
    [Fact]
    public void QuerySyntaxUsesRoslynBoundCallsAndTransparentIdentifiers() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { 3, 1, 2 };
                var simple = from n in values where n > 1 orderby n descending select n * 2;
                var nested = from a in values from b in values let sum = a + b where sum > 4 select new { a, b, sum };
                return new { simple = simple.ToArray(), nested = nested.ToArray() };
            }
        }
        """);

    [Fact]
    public void CoalescingAssignmentsAreLazyAndCaptureStorageBeforeAwait() => DifferentialRunner.Equal("""
        public sealed class Box { public string? Value { get; set; } }
        public static class Test
        {
            private static string trace = "";
            private static Box box = new();
            private static Box Receiver() { trace += "r"; return box; }
            private static string Make() { trace += "m"; return "ok"; }
            public static async Task<object> Run()
            {
                string? local = null; var first = local ??= Make(); var second = local ??= Make();
                var a = Receiver().Value ??= await Task.FromResult(Make());
                var b = Receiver().Value ??= await Task.FromResult(Make());
                var array = new string?[2]; var i = 0; array[i++] ??= Make();
                return new { first, second, a, b, trace, i, item = array[0] };
            }
        }
        """);

    [Fact]
    public void BitwiseCompoundAssignmentsRespectFlagsUnsignedAndBooleanTypes() => DifferentialRunner.Equal("""
        [Flags] public enum Bits { A = 1, B = 2, C = 4 }
        public static class Test
        {
            public static object Run()
            {
                var flags = Bits.A | Bits.B; flags &= ~Bits.A; flags |= Bits.C; flags ^= Bits.B;
                var signed = -8; signed >>= 1; signed <<= 2; signed >>>= 1;
                uint unsigned = 0x80000000; unsigned >>= 1; unsigned |= 1; unsigned ^= 2;
                var calls = 0; bool Next() { calls++; return true; }
                var boolean = false; boolean &= Next(); boolean |= Next(); boolean ^= Next();
                return new { flags = (int)flags, signed, unsigned, boolean, calls };
            }
        }
        """);
    [Fact]
    public void ForClosuresShareLoopVariablesButKeepBodyLocalsSeparate() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var shared = new List<Func<int>>(); var separate = new List<Func<int>>();
                for (var i = 0; i < 3; i++) { var snapshot = i; shared.Add(() => i); separate.Add(() => snapshot); }
                var nested = new List<Func<int>>();
                for (var i = 0; i < 2; i++) for (var j = 0; j < 2; j++) { nested.Add(() => i * 10 + j); }
                return new { shared = shared.Select(f => f()).ToArray(), separate = separate.Select(f => f()).ToArray(), nested = nested.Select(f => f()).ToArray() };
            }
        }
        """);

    [Fact]
    public void ExtendedPropertyPatternsCheckNullAndReadGettersOnce() => DifferentialRunner.Equal("""
        public sealed class Node(string? name, Node? parent)
        {
            public static int Reads;
            public string? Name { get { Reads++; return name; } }
            public Node? Parent { get { Reads++; return parent; } }
        }
        public static class Test
        {
            public static object Run()
            {
                var root = new Node("root", null); var child = new Node("", root);
                var first = child is { Parent.Name.Length: > 0 };
                var missing = root is { Parent.Name: var absent };
                var binding = child is { Parent.Name: var name } ? name : "missing";
                return new { first, missing, binding, reads = Node.Reads };
            }
        }
        """);

    [Fact]
    public void PlainRecordsKeepTypeIdentityAcrossObjectPatterns() => DifferentialRunner.Equal("""
        public sealed record P(int Value);
        public sealed record Q(int Value);
        public static class Test
        {
            private static int Read(object? value) => value switch { P => 1, Q q => q.Value, null => 0, _ => -1 };
            public static object Run()
            {
                object p = new P(3); object q = new Q(7);
                return new { p = Read(p), q = Read(q), nil = Read(null), other = Read(new object()), match = p is P, mismatch = q is P };
            }
        }
        """);
}
