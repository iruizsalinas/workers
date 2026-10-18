namespace Workers.Compiler.Tests;

public sealed class NextLanguageFeaturesTests
{
    [Fact]
    public void ExtensionDeconstructorsCanHandleNullReceivers() => DifferentialRunner.Equal("""
        public sealed class Pair { }
        public static class Extensions
        {
            public static void Deconstruct(this Pair pair, out int first, out int second)
            {
                first = pair is null ? 3 : 4;
                second = 5;
            }
        }
        public static class Test
        {
            public static object Run()
            {
                Pair pair = null;
                var (first, second) = pair;
                return new { first, second };
            }
        }
        """);

    [Fact]
    public void DeconstructingNullInstancesThrowsCatchableNullReferenceExceptions() => DifferentialRunner.Equal("""
        public sealed class Pair
        {
            public void Deconstruct(out int first, out int second) { first = 1; second = 2; }
        }
        public sealed record Row(int First, int Second);
        public static class Test
        {
            public static object Run()
            {
                Pair pair = null;
                Row row = null;
                var caught = 0;
                try { var (first, second) = pair; }
                catch (System.NullReferenceException) { caught++; }
                try { var (first, second) = row; }
                catch (System.NullReferenceException) { caught++; }
                return caught;
            }
        }
        """);

    [Fact]
    public void NestedDeconstructionSnapshotsTupleValuesBeforeCallingUserCode() => DifferentialRunner.Equal("""
        public sealed class Pair
        {
            public void Deconstruct(out int a, out int b) { a = 1; b = 2; Test.Change(); }
        }
        public static class Test
        {
            private static (Pair, int) value = (new Pair(), 5);
            public static void Change() { value.Item2 = 9; }
            public static object Run()
            {
                var ((a, b), c) = value;
                return new { a, b, c, current = value.Item2 };
            }
        }
        """);

    [Fact]
    public void TupleInitializersRecordCopiesAndEqualityPreserveValues() => DifferentialRunner.Equal("""
        public sealed class Snapshot((int, int) value)
        {
            public (int, int) Saved { get; } = value;
            public void Change() { value.Item1 = 9; }
            public int Current() => value.Item1;
        }
        public sealed record Row
        {
            public (int, int) Value;
            public Row((int, int) value) { Value = value; }
        }
        public static class Test
        {
            public static object Run()
            {
                var snapshot = new Snapshot((1, 2)); snapshot.Change();
                var original = new Row((3, 4)); var equal = original == new Row((3, 4));
                var copy = original with { }; copy.Value.Item1 = 7;
                return new { saved = snapshot.Saved.Item1, current = snapshot.Current(),
                    first = original.Value.Item1, second = copy.Value.Item1, equal };
            }
        }
        """);

    [Fact]
    public void DeconstructsDictionaryEntriesAndUserPositionalPatterns() => DifferentialRunner.Equal("""
        public sealed class Pair
        {
            public static int Calls;
            public void Deconstruct(out int a, out int b) { Calls++; a = 3; b = 7; }
        }
        public static class Test
        {
            public static object Run()
            {
                var total = 0; foreach (var (key, value) in new Dictionary<string, int> { ["x"] = 2, ["y"] = 4 }) total += value;
                var pair = new Pair(); var match = pair is (3, var second) ? second : -1;
                var nested = (1, 2) is (1, var tail) ? tail : -1;
                return new { total, match, nested, calls = Pair.Calls };
            }
        }
        """);

    [Fact]
    public void LocalFunctionsCanCaptureLocalsAcrossUsingDeclarations() => DifferentialRunner.Equal("""
        public sealed class Lease : System.IDisposable { public void Dispose() { } }
        public static class Test
        {
            public static object Run()
            {
                var value = 1;
                int Read() => value;
                using var lease = new Lease();
                value = 3;
                return new { value = Read() };
            }
        }
        """);

    [Fact]
    public void ParamsWorkInPrimaryConstructorsAndInferredLambdaDelegates() => DifferentialRunner.Equal("""
        public sealed class Values(params int[] values) { public int Total() => values.Sum(); }
        public static class Test
        {
            public static object Run()
            {
                var sum = (params int[] values) => values.Sum();
                return new { a = new Values(1, 2).Total(), b = new Values().Total(), c = sum(3, 4), d = sum() };
            }
        }
        """);

    [Fact]
    public void LiftedRangesPropagateMissingEndpoints() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                System.Index? start = null; System.Index? end = ^1;
                System.Range? missing = start..end;
                start = 1; System.Range? present = start..end;
                return new { missing = missing == null, text = "abcd"[present.Value] };
            }
        }
        """);

    [Fact]
    public void LocalFunctionsPreserveLexicalScopesAndNestedGenericCaptures() => DifferentialRunner.Equal("""
        public sealed class Box<T>(T item)
        {
            public string Read<U>(U other)
            {
                string Local<V>(V value) => $"{item}:{other}:{value}";
                return Local(3) + Local("x");
            }
        }
        public static class Test
        {
            public static object Run()
            {
                var functions = new List<System.Func<int>>();
                for (var i = 0; i < 3; i++) { var copy = i; int Read() => copy; functions.Add(Read); }
                bool Even(int n) => n == 0 || Odd(n - 1);
                bool Odd(int n) => n != 0 && Even(n - 1);
                return new { values = functions.Select(f => f()).ToArray(), even = Even(8), text = new Box<string>("a").Read(2) };
            }
        }
        """);

    [Fact]
    public void ParamsWorkWithExtensionsGenericMethodsAndDelegates() => DifferentialRunner.Equal("""
        public static class Extensions { public static int Total(this int first, params int[] rest) => first + rest.Sum(); }
        public static class Test
        {
            private static T[] Values<T>(params T[] values) => values;
            public static object Run()
            {
                int Local(params int[] values) => values.Sum();
                System.Func<int[], int> sum = Local;
                return new { a = 1.Total(2, 3), b = 1.Total(), c = Values("a", "b"), d = sum(new[] { 4, 5 }), e = Local() };
            }
        }
        """);

    [Fact]
    public void ObjectTextHandlesBoxedEnumsAnonymousValuesAndNullOverrides() => DifferentialRunner.Equal("""
        public enum Color { Green, Blue }
        public sealed record Empty(int Value) { public override string ToString() => null; }
        public sealed class None { public override string ToString() => null; }
        public static class Test
        {
            private static string Format(object value) => $"{value}";
            public static object Run() => new {
                a = Format(Color.Blue), b = $"{Color.Green}", c = $"<{new Empty(1)}>|<{new None()}>",
                d = Format(new { name = "x", flag = true }), e = $"{new { number = 3 }}",
                f = Format(new List<string> { "a" }), g = Format((2, "x")),
                h = Format((Color?)Color.Blue), i = Format(((int, string)?)(2, "x")), j = Format((Color?)null)
            };
        }
        """);

    [Fact]
    public void TupleStorageSurvivesReassignmentDuringMutationAndCopiesWhenBoxed() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var tuple = (1, 2); var reference = 3;
                int Replace() { tuple = (10, 20); return reference; }
                tuple.Item1 += Replace(); tuple.Item2++;
                object boxed = tuple; tuple.Item1 = 8;
                var value = 0; (value, tuple.Item2) = (7, 9);
                return new { first = tuple.Item1, second = tuple.Item2, value, text = $"{boxed}", serialized = tuple };
            }
        }
        """);

    [Fact]
    public void DeconstructionEvaluatesAllAddressesBeforeSourceAndDelaysWritesUntilSuccess() => DifferentialRunner.Equal("""
        public sealed class Broken
        {
            public void Deconstruct(out int a, out int b) { a = 7; b = 8; throw new System.Exception(); }
        }
        public static class Test
        {
            public static object Run()
            {
                var value = 1; var other = 2;
                try { (value, other) = new Broken(); } catch (System.Exception) { }
                var list = new List<int> { 0 }; var dictionary = new Dictionary<string, int>();
                (list[0], dictionary["x"]) = (3, 4);
                return new { value, other, first = list[0], second = dictionary["x"] };
            }
        }
        """);

    [Fact]
    public void RangeBoundsRejectNegativeAndExcessiveFromEndIndices() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static bool Invalid(int left, int right)
            {
                try { var value = "abc"[left..^right]; return false; } catch (System.Exception) { return true; }
            }
            public static object Run() => new { a = Invalid(-1, 0), b = Invalid(0, 4), c = Invalid(3, 1), d = Invalid(0, -1),
                full = "abc"[System.Range.All], last = "abc"[System.Index.End.Value..] };
        }
        """);

    [Fact]
    public void DeconstructionPreservesTargetEvaluationAndConversionOrder() => DifferentialRunner.Equal("""
        public sealed class Holder { public int Value { get; set; } }
        public static class Test
        {
            private static string log = "";
            private static int Index() { log += "i"; return 0; }
            private static Holder Target(Holder h) { log += "t"; return h; }
            private static (int, int) Values() { log += "v"; return (7, 8); }
            public static object Run()
            {
                var a = new[] { 0 }; var h = new Holder();
                (a[Index()], Target(h).Value) = Values();
                var (x, y) = (1, 2); (x, x) = (3, 4);
                (double left, double right) = (5, 6);
                return new { a, value = h.Value, x, y, left, right, log };
            }
        }
        """);

    [Fact]
    public void DeconstructsRecordsExtensionsAndForeachElements() => DifferentialRunner.Equal("""
        public record Pair<T>(T First, int Second);
        public sealed class Value { public int Number { get; set; } = 7; }
        public static class Extensions
        {
            public static void Deconstruct(this Value value, out int a, out int b) { a = value.Number; b = 9; }
        }
        public static class Test
        {
            public static object Run()
            {
                var (text, number) = new Pair<string>("x", 3);
                new Pair<int>(4, 5).Deconstruct(out var directFirst, out var directSecond);
                var (a, b) = new Value(); var total = 0;
                foreach (var (left, right) in new[] { (1, 2), (3, 4) }) total += left + right;
                return new { text, number, a, b, total, directFirst, directSecond };
            }
        }
        """);

    [Fact]
    public void TuplesHaveValueSemanticsAcrossLocalsArgumentsAndReturns() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static (int, (int, int)) Copy((int, (int, int)) value) { value.Item2.Item1 = 9; return value; }
            public static object Run()
            {
                var original = (1, (2, 3)); var second = original; second.Item1 = 7;
                var third = Copy(original);
                (double, (double, double)) converted = original;
                return new { a = original.Item1, b = original.Item2.Item1, c = second.Item1,
                    d = third.Item2.Item1, e = converted.Item2.Item2 };
            }
        }
        """);

    [Fact]
    public void RangesEvaluateOnceAndSupportStoredIndicesAndByteCopies() => DifferentialRunner.Equal("""
        public static class Test
        {
            private static string log = "";
            private static byte[] Source(byte[] source) { log += "s"; return source; }
            private static int Endpoint(int n) { log += n; return n; }
            public static object Run()
            {
                var data = new byte[] { 1, 2, 3, 4 };
                System.Index end = ^1; System.Range range = 1..end;
                var copy = Source(data)[Endpoint(1)..^Endpoint(1)]; copy[0] = 9;
                data[end] = 8;
                return new { first = data[0], last = data[end], copyFirst = copy[0], copyLength = copy.Length,
                    stored = "abcd"[range], empty = "abcd"[default(System.Range)], log };
            }
        }
        """);

    [Fact]
    public void FormatsTypeErasedUserObjectsRecordsAndArrays() => DifferentialRunner.Equal("""
        public interface IItem { }
        public sealed class Plain { }
        public sealed class Named { public override string ToString() => "named"; }
        public sealed record Item(int Value) : IItem;
        public static class Test
        {
            private static string Format(object value) => $"{value}";
            public static object Run() => new {
                a = Format(new Plain()), b = Format(new Named()), c = Format(new Item(3)),
                d = Format(new int[] { 1, 2 }), e = Format(new object()), f = Format(null), g = Format(true),
                h = Format((IItem)new Item(7))
            };
        }
        """);

    [Fact]
    public void PrimaryCaptureIsInitializedBeforeBaseVirtualCallsAndRespectsInitializerMutations() => DifferentialRunner.Equal("""
        public class Base { public int Observed { get; } public Base() { Observed = Read(); } public virtual int Read() => 0; }
        public sealed class Child(int value) : Base
        {
            public int Initial { get; } = value++;
            public override int Read() => value;
        }
        public sealed class Box<T>(T value) { public T Read() => value; }
        public static class Test
        {
            public static object Run()
            {
                var c = new Child(3);
                return new { observed = c.Observed, initial = c.Initial, current = c.Read(),
                    a = new Box<int>(7).Read(), b = new Box<string>("x").Read() };
            }
        }
        """);

    [Fact]
    public void LocalFunctionsCaptureMutateRecurseAndConvertToDelegates() => DifferentialRunner.Equal("""
        public sealed class Counter
        {
            public int Offset { get; set; } = 10;
            public int Run()
            {
                var count = 0;
                var first = Fib(5);
                int Fib(int n) { count++; return n < 2 ? n : Fib(n - 1) + Fib(n - 2); }
                int Add(int value = 2) => Offset + value + count++;
                System.Func<int, int> add = Add;
                return first + Add() + add(3) + count;
            }
        }
        public static class Test { public static object Run() => new { result = new Counter().Run() }; }
        """);

    [Fact]
    public void LocalFunctionsPreserveGenericAsyncAndIteratorSemantics() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                T Echo<T>(T value) => value;
                async Task<int> Later(int value) { await Task.CompletedTask; return Echo(value); }
                IEnumerable<int> Values(int n) { yield return n++; yield return n++; }
                var values = Values(2);
                return new { a = Echo("x"), b = await Later(7), first = values.ToArray(), second = values.ToArray() };
            }
        }
        """);

    [Fact]
    public void ParamsPreserveExpandedNormalNullAndNamedCalls() => DifferentialRunner.Equal("""
        public sealed class Values
        {
            public int[] Items { get; }
            public Values(params int[] values) { Items = values; }
        }
        public static class Test
        {
            private static string log = "";
            private static int Next(int n) { log += n; return n; }
            private static int[] Read(string label = "x", params int[] values) { log += label; return values; }
            public static object Run()
            {
                var input = new[] { 3, 4 };
                var normal = Read(values: input, label: "a"); normal[0] = 9;
                var expanded = Read("b", Next(1), Next(2));
                var constructor = new Values(input); constructor.Items[1] = 8;
                return new { input, normal, expanded, empty = Read().Length, missing = Read(values: null) == null,
                    constructorEmpty = new Values().Items.Length, log };
            }
        }
        """);

    [Fact]
    public void InterpolationUsesRecordEnumAndObjectTextOnce() => DifferentialRunner.Equal("""
        public enum Color { Green, Blue }
        public record Item(int Value);
        public class Base { public override string ToString() => "base"; }
        public sealed class Child : Base { public override string ToString() => "child"; }
        public static class Test
        {
            private static int calls;
            private static Item Next() { calls++; return new Item(calls); }
            public static object Run()
            {
                Item missing = null; Base value = new Child();
                return new { text = $"{Next(),25}|{Color.Green}|{missing}|{value}", calls };
            }
        }
        """);

    [Fact]
    public void PrimaryConstructorsCaptureParametersAndChainThroughBase() => DifferentialRunner.Equal("""
        public class Base(int value)
        {
            public int Original { get; } = value;
            public virtual int Read() => value;
        }
        public sealed class Child(int value, string name) : Base(value + 1)
        {
            public Child() : this(3, "start") { }
            public int Initial { get; } = value;
            public override int Read() => value++;
            public string Name() => name;
            public void Rename(string text) { name = text; }
            public int BaseValue() => base.Read();
        }
        public static class Test
        {
            public static object Run()
            {
                var item = new Child(); item.Rename("end");
                return new { initial = item.Initial, original = item.Original, a = item.Read(), b = item.Read(), c = item.BaseValue(), name = item.Name() };
            }
        }
        """);

    [Fact]
    public void TuplesDeconstructNestSwapAndCallUserDeconstruct() => DifferentialRunner.Equal("""
        public sealed class Pair
        {
            public void Deconstruct(out int a, out string b) { a = 7; b = "pair"; }
        }
        public static class Test
        {
            private static (int Lo, int Hi) Bounds() => (1, 9);
            public static object Run()
            {
                var (lo, hi) = Bounds(); (lo, hi) = (hi, lo);
                var (a, (b, _)) = (2, (3, 4));
                var (number, text) = new Pair();
                return new { lo, hi, a, b, number, text, named = Bounds().Hi };
            }
        }
        """);

    [Fact]
    public void RangesCopyArraysAndValidateBounds() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static object Run()
            {
                var values = new[] { 1, 2, 3, 4 }; var middle = values[1..^1]; middle[0] = 9;
                var invalid = false; try { var bad = values[3..1]; } catch (System.Exception) { invalid = true; }
                var outside = false; try { var bad = "abc"[..4]; } catch (System.Exception) { outside = true; }
                return new { values, middle, all = values[..], empty = values[^0..], text = "abcd"[1..^1], invalid, outside };
            }
        }
        """);
}
