namespace Workers.Compiler.Tests;

public sealed class CompositeLanguageFeaturesTests
{
    [Fact]
    public void StaticInterfaceDefaultBodiesPreservePerConstructionState() => DifferentialRunner.Equal("""
        public interface ICounter<T> where T : ICounter<T>
        {
            private static int count;
            static virtual int Next() { count++; return count; }
            static virtual int Value { get => count; set => count = value; }
        }
        public sealed class A : ICounter<A> { }
        public sealed class B : ICounter<B> { }
        public static class Test
        {
            private static int Next<T>() where T : ICounter<T> => T.Next();
            private static int Set<T>(int value) where T : ICounter<T> { T.Value = value; return T.Value; }
            public static object Run() => new { first = Next<A>(), second = Next<A>(), other = Next<B>(), changed = Set<A>(7), last = Next<A>() };
        }
        """);
    [Fact]
    public void IndexerObjectInitializersSupportInitAndParamsAccessors() => DifferentialRunner.Equal("""
        public sealed class Row
        {
            private int value;
            public int this[int index] { get => value + index; init { this.value = value - index; } }
            public int this[string name, params int[] values] => value + values.Sum();
        }
        public static class Test
        {
            public static object Run()
            {
                var row = new Row { [2] = 7 };
                return new { value = row[2], expanded = row["x", 1, 2], normal = row["x", new[] { 3, 4 }], empty = row["x"] };
            }
        }
        """);

    [Fact]
    public void DiagnosesRefReturningIndexers()
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile("""
            using Workers;
            public sealed class Row { private int[] values = new int[2]; public ref int this[int index] => ref values[index]; }
            public static class Worker { [Fetch] public static Response Fetch(Request request) { var row = new Row(); row[0] = 3; return Response.Json(row[0]); } }
            """));
        Assert.StartsWith("WRK119:", error.Message);
    }
    [Fact]
    public void InheritedAndExplicitEventsAndRecordCopiesPreserveHandlers() => DifferentialRunner.Equal("""
        public interface ISource { event Action Changed; }
        public abstract class Source { public abstract event Action Changed; public abstract void Raise(); }
        public sealed class Child : Source, ISource
        {
            public override event Action? Changed;
            event Action ISource.Changed { add { Changed += value; } remove { Changed -= value; } }
            public override void Raise() { Changed?.Invoke(); }
        }
        public sealed record RecordSource
        {
            public event Action? Changed;
            public void Raise() { Changed?.Invoke(); }
        }
        public static class Test
        {
            public static object Run()
            {
                var calls = 0; Action handler = () => calls++;
                Source source = new Child(); ISource contract = (ISource)source;
                source.Changed += handler; contract.Changed += handler; source.Raise();
                contract.Changed -= handler; source.Raise();
                var original = new RecordSource(); original.Changed += handler;
                var copy = original with { }; var equal = original == copy;
                copy.Changed -= handler; var different = original != copy;
                original.Raise(); copy.Raise();
                return new { calls, equal, different };
            }
        }
        """);

    [Fact]
    public void StaticVirtualDefaultsAndGenericImplementationsRemainSpecialized() => DifferentialRunner.Equal("""
        public interface IFactory<T> where T : IFactory<T>
        {
            static abstract T Create(int value);
            static virtual int Offset => 3;
            static virtual T Default() => T.Create(T.Offset);
        }
        public sealed class Item<T>(T value) : IFactory<Item<T>>
        {
            public int Count { get; } = 1;
            public T Value { get; } = value;
            public static Item<T> Create(int value) => new(default!);
        }
        public sealed class Override(int value) : IFactory<Override>
        {
            public int Value { get; } = value;
            public static int Offset => 7;
            public static Override Create(int value) => new(value);
        }
        public static class Test
        {
            private static T Default<T>() where T : IFactory<T> => T.Default();
            public static object Run() => new { value = Default<Override>().Value, count = Default<Item<string>>().Count };
        }
        """);
    [Fact]
    public void LocalFunctionDelegatesDistinguishCapturedEnvironments() => DifferentialRunner.Equal("""
        public sealed class Box(int value)
        {
            public Func<int> Read() { int Local() => value; return Local; }
        }
        public static class Test
        {
            private static Func<int> Plain() { int Read() => 1; return Read; }
            private static Func<int> Static() { static int Read() => 2; return Read; }
            private static Func<int> Captured(int value) { int Read() => value; return Read; }
            private static Func<int> Transitive(int value) { int Inner() => value; int Outer() => Inner(); return Outer; }
            public static object Run()
            {
                var box = new Box(3);
                return new { plain = Plain() == Plain(), statics = Static() == Static(), instance = box.Read() == box.Read(),
                    captured = Captured(3) == Captured(3), transitive = Transitive(3) == Transitive(3) };
            }
        }
        """);
    [Fact]
    public void CompositeKeysIncludeKeyValuePairIndexAndRangeValues() => DifferentialRunner.Equal("""
        public sealed record Key(KeyValuePair<string, int> Pair, Index Position, Range Window);
        public static class Test
        {
            public static object Run()
            {
                var set = new HashSet<Key> { new(new("a", 1), ^2, 1..^1), new(new("a", 1), ^2, 1..^1) };
                var tuples = new Dictionary<(Index, Range), int> { [(^2, 1..^1)] = 3 };
                return new { count = set.Count, found = set.Contains(new(new("a", 1), ^2, 1..^1)), value = tuples[(^2, 1..^1)] };
            }
        }
        """);
    [Fact]
    public void DelegateTypesEraseTupleNamesAndNullableAnnotations() => DifferentialRunner.Equal("""
        #nullable enable
        public static class Test
        {
            private static string Format((int, int) value) => "ok";
            public static object Run()
            {
                Func<(int A, int B), string?> first = Format;
                Func<(int X, int Y), string> second = Format;
                var normalized = (Func<(int A, int B), string?>)second;
                var combined = first + normalized;
                return new { equal = first == normalized, value = combined((1, 2)) };
            }
        }
        """);

    [Fact]
    public void NullableTupleKeysAndValuesAreCopiedAndCollisionsAreCheckedOnce() => DifferentialRunner.Equal("""
        public sealed record Key(int Value)
        {
            public static int Hashes;
            public override int GetHashCode() { Hashes++; return 1; }
        }
        public static class Test
        {
            public static object Run()
            {
                var map = new Dictionary<Key, (int, int)?>();
                map.TryAdd(new(1), (3, 4));
                var addHashes = Key.Hashes; Key.Hashes = 0;
                var found = map.TryGetValue(new(1), out var value);
                var readHashes = Key.Hashes; Key.Hashes = 0;
                map.Remove(new(1), out var removed);
                var removeHashes = Key.Hashes;
                var tuple = (1, 2); (int, int)? nullable = tuple; tuple.Item1 = 9;
                var set = new HashSet<(int, int)?> { nullable, null, (1, 2) };
                return new { addHashes, readHashes, removeHashes, found, value = value!.Value.Item1, removed = removed!.Value.Item2,
                    count = set.Count, contains = set.Contains((1, 2)), first = nullable.Value.Item1 };
            }
        }
        """);
    [Fact]
    public void CompositeMembersUseUserEquatableAndVirtualObjectEquality() => DifferentialRunner.Equal("""
        public class Identity { }
        public sealed class Equatable(int value) : IEquatable<Equatable>
        {
            public int Value { get; } = value;
            bool IEquatable<Equatable>.Equals(Equatable? other) => other is not null && Value == other.Value;
            public override int GetHashCode() => Value.GetHashCode();
        }
        public sealed class Derived(int value) : Identity
        {
            public int Value { get; } = value;
            public override bool Equals(object? other) { return other is Derived item && Value == item.Value; }
            public override int GetHashCode() => Value;
        }
        public sealed record Key(Equatable First, Identity Second);
        public static class Test
        {
            public static object Run()
            {
                var set = new HashSet<Key> { new(new(1), new Derived(2)), new(new(1), new Derived(2)) };
                var identity = new Identity();
                var tuples = new HashSet<(Identity, int)> { (identity, 3), (identity, 3), (new Identity(), 3) };
                return new { records = set.Count, tuples = tuples.Count, found = set.Contains(new(new(1), new Derived(2))) };
            }
        }
        """);

    [Fact]
    public void StaticInterfaceConversionsAndMutationOperatorsPreserveAssignedValues() => DifferentialRunner.Equal("""
        public interface IValue<T> where T : IValue<T>
        {
            static abstract T operator +(T a, T b);
            static abstract T operator ++(T a);
            static abstract explicit operator int(T a);
        }
        public sealed class Value(int number) : IValue<Value>
        {
            public int Number { get; } = number;
            public static Value operator +(Value a, Value b) => new(a.Number + b.Number);
            public static Value operator ++(Value a) => new(a.Number + 1);
            public static explicit operator int(Value a) => a.Number;
        }
        public static class Test
        {
            private static int Run<T>(T old, T added) where T : IValue<T>
            {
                old += added; var before = old++; var next = ++old;
                return (int)old * 100 + (int)before * 10 + (int)next;
            }
            public static object Run() => Run(new Value(1), new Value(2));
        }
        """);

    [Fact]
    public void CompositeInitializersDoNotShadowUserLocalsAndSupportAwait() => DifferentialRunner.Equal("""
        public static class Test
        {
            public static async Task<object> Run()
            {
                var collection = (1, 2);
                var map = new Dictionary<(int, int), int> { [collection] = await Task.FromResult(3) };
                var set = new HashSet<(int, int)> { collection, (3, 4) };
                var reference = new Action[] { () => { } }; var calls = 0;
                reference[0] += () => calls++;
                reference[0]();
                return new { value = map[(1, 2)], count = set.Count, calls };
            }
        }
        """);

    [Theory]
    [InlineData("object")]
    [InlineData("IComparable")]
    public void DiagnosesCompositeMembersWhoseEqualityWasErased(string memberType)
    {
        var error = Assert.Throws<NotSupportedException>(() => Compile($$"""
            using Workers;
            public sealed record Key({{memberType}} Value);
            public static class Worker { [Fetch] public static Response Fetch(Request request) => Response.Json(new HashSet<Key>().Count); }
            """));
        Assert.StartsWith("WRK105:", error.Message);
        Assert.Contains("concrete key member type", error.Message);
    }
    [Fact]
    public void DelegateVarianceAsyncResultsAndOutArgumentsKeepCSharpSemantics() => DifferentialRunner.Equal("""
        public delegate int Read(out int value);
        public static class Test
        {
            private static int A(out int value) { value = 3; return 1; }
            private static int B(out int value) { value = 7; return 2; }
            public static async Task<object> Run()
            {
                Read read = A; read += B; var result = read(out var value);
                Func<string> narrow = () => "a"; Func<object> wide = narrow;
                Func<object> other = () => "b";
                var rejected = false; try { wide += other; } catch (Exception) { rejected = true; }
                var calls = 0;
                Func<Task<int>> first = async () => { calls++; await Task.FromResult(0); return 1; };
                Func<Task<int>> last = () => Task.FromResult(4);
                var combined = first + last;
                var final = await combined();
                return new { result, value, rejected, calls, final };
            }
        }
        """);
    [Fact]
    public void DelegatesCompareConcreteInterfaceVirtualGenericAndExtensionTargets() => DifferentialRunner.Equal("""
        public interface IRead { int Read(); T Echo<T>(T value); }
        public class Base : IRead
        {
            public virtual int Read() => 1;
            public virtual T Echo<T>(T value) => value;
        }
        public sealed class Child : Base { public override int Read() => 2; public override T Echo<T>(T value) => value; }
        public static class Extensions { public static int Size(this string? value) => value?.Length ?? -1; }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child(); IRead contract = child; Base parent = child;
                Func<int> concrete = child.Read, viaInterface = contract.Read, viaBase = parent.Read;
                Func<int, int> generic = child.Echo<int>, genericInterface = contract.Echo<int>, genericBase = parent.Echo<int>;
                string? text = null; Func<int> extension = text.Size;
                var closures = new List<Func<int>>();
                for (var i = 0; i < 2; i++) { var capture = i; int Read() => capture; closures.Add(Read); }
                return new { interfaces = concrete == viaInterface, bases = concrete == viaBase,
                    genericInterface = generic == genericInterface, genericBase = generic == genericBase, extension = extension(),
                    closureDifferent = closures[0] != closures[1] };
            }
        }
        """);

    [Fact]
    public void VariantAndDefaultInterfaceIndexersAndStaticEventsWork() => DifferentialRunner.Equal("""
        public interface IView<out T> { T this[int index] { get; } }
        public sealed class View : IView<string> { public string this[int index] => "v" + index; }
        public interface IDefault { int this[int index] => index * 2; }
        public sealed class Default : IDefault { }
        public interface IEvents<T> where T : IEvents<T> { static abstract event Action<int> Changed; static abstract void Raise(); }
        public sealed class Events : IEvents<Events>
        {
            public static event Action<int>? Changed;
            public static void Raise() { Changed?.Invoke(3); }
        }
        public static class Test
        {
            private static int Listen<T>() where T : IEvents<T>
            {
                var total = 0; Action<int> handler = n => total += n;
                T.Changed += handler; T.Raise(); T.Changed -= handler; T.Raise(); return total;
            }
            public static object Run()
            {
                IView<object> view = new View(); IDefault defaults = new Default();
                return new { text = $"{view[2]}", value = defaults[3], total = Listen<Events>() };
            }
        }
        """);
    [Fact]
    public void HashBucketsHandleCollisionsCustomEqualityAndMutableKeys() => DifferentialRunner.Equal("""
        public sealed record Collision(int Value)
        {
            public bool Equals(Collision? other) => other is not null && Value % 10 == other.Value % 10;
            public override int GetHashCode() => 1;
        }
        public sealed record Mutable { public int Value { get; set; } }
        public static class Test
        {
            public static object Run()
            {
                var map = new Dictionary<Collision, int> { [new(1)] = 3, [new(2)] = 4 };
                var duplicate = false; try { map.Add(new(11), 0); } catch (Exception) { duplicate = true; }
                var first = map[new(21)]; map.Remove(new(12));
                var set = new HashSet<Collision> { new(1) };
                var found = set.TryGetValue(new(11), out var canonical);
                var key = new Mutable { Value = 1 }; var mutable = new HashSet<Mutable> { key };
                key.Value = 2; var afterMutation = mutable.Contains(key);
                key.Value = 1; var restored = mutable.Contains(key);
                return new { duplicate, first, count = map.Count, found, canonical = canonical!.Value, afterMutation, restored };
            }
        }
        """);

    [Fact]
    public void CompositeCollectionViewsAndEnumerationPreserveValues() => DifferentialRunner.Equal("""
        public sealed record Key(int Value);
        public static class Test
        {
            private static int calls;
            private static int Fallback() { calls++; return -1; }
            public static object Run()
            {
                var map = new Dictionary<(int, int), int> { [(1, 2)] = 5, [(3, 4)] = 6 };
                var keys = map.Keys; var values = map.Values;
                var existing = map.GetValueOrDefault((1, 2), Fallback());
                var missing = map.GetValueOrDefault((9, 9), 7);
                var seen = 0;
                foreach (var pair in map) { seen += pair.Value; map.Remove((3, 4)); }
                var invalidated = false;
                try { foreach (var pair in map) { map.Add((5, 6), 8); } } catch (Exception) { invalidated = true; }
                var liveKeys = keys.Count; var liveValues = values.Count;
                var set = new HashSet<Key> { new(1), new(1) };
                var tuples = new HashSet<(int, int)> { (1, 2), (1, 2) };
                return new { existing, missing, calls, seen, invalidated, liveKeys, liveValues, set = set.Count, tuples };
            }
        }
        """);

    [Fact]
    public void StaticInterfacePropertiesOperatorsAndGenericMethodsSpecialize() => DifferentialRunner.Equal("""
        public interface INumber<T> where T : INumber<T>
        {
            static abstract int State { get; set; }
            static abstract T operator +(T a, T b);
            static abstract T operator -(T a);
            static abstract U Identity<U>(U value);
        }
        public sealed class Number(int value) : INumber<Number>
        {
            private static int state;
            public int Value { get; } = value;
            static int INumber<Number>.State { get => state; set => state = value + 1; }
            public static Number operator +(Number a, Number b) => new(a.Value + b.Value);
            public static Number operator -(Number a) => new(-a.Value);
            public static U Identity<U>(U value) => value;
        }
        public static class Test
        {
            private static T Sum<T>(T a, T b) where T : INumber<T> => -(a + b);
            private static int State<T>() where T : INumber<T> { T.State = 3; T.State += 2; return T.State; }
            private static string Identity<T>() where T : INumber<T> { Func<string, string> method = T.Identity<string>; return method("ok"); }
            public static object Run() => new { value = Sum(new Number(2), new Number(3)).Value, state = State<Number>(), text = Identity<Number>() };
        }
        """);

    [Fact]
    public void GenericIndexersExplicitInterfacesAndDeconstructionUseBoundAccessors() => DifferentialRunner.Equal("""
        public interface IRow<T> { T this[int index] { get; set; } }
        public sealed class Row<T>(T value) : IRow<T>
        {
            T IRow<T>.this[int index] { get => value; set { this.Change(value); } }
            public T this[string name, int offset = 2] => value;
            private void Change(T next) { value = next; }
        }
        public static class Test
        {
            public static object Run()
            {
                var row = new Row<int>(1); IRow<int> contract = row;
                (contract[1], contract[2]) = (4, 7);
                var previous = contract[0]++;
                return new { previous, value = row["x"], current = contract[0] };
            }
        }
        """);

    [Fact]
    public void EventsSnapshotHandlersDuringReentrantChangesAndStopOnExceptions() => DifferentialRunner.Equal("""
        public sealed class Source
        {
            public event Func<int>? Changed;
            public int Raise() => Changed?.Invoke() ?? -1;
        }
        public static class Test
        {
            public static object Run()
            {
                var source = new Source(); var trace = "";
                Func<int> second = () => { trace += "b"; return 2; };
                Func<int> first = () => { trace += "a"; source.Changed -= second; return 1; };
                source.Changed += first; source.Changed += second;
                var initial = source.Raise(); var next = source.Raise();
                Func<int> fail = () => { trace += "x"; throw new Exception(); };
                source.Changed += fail; source.Changed += second;
                var caught = false; try { source.Raise(); } catch (Exception) { caught = true; }
                return new { trace, initial, next, caught };
            }
        }
        """);
    [Fact]
    public void CompositeDictionaryKeysUseValueEqualityAndCopyTuples() => DifferentialRunner.Equal("""
        public sealed record Key(string Tenant, int User);
        public static class Test
        {
            public static object Run()
            {
                var map = new Dictionary<(string, int), (int, int)> { { ("a", 1), (3, 4) }, { ("b", 2), (5, 6) } };
                var key = ("c", 3); map.Add(key, (7, 8)); key.Item2 = 9;
                var found = map.TryGetValue(("c", 3), out var value); value.Item1 = 99;
                var duplicate = !map.TryAdd(("a", 1), (0, 0));
                var copy = new Dictionary<(string, int), (int, int)>(map);
                var total = 0; foreach (var (k, v) in copy) total += k.Item2 + v.Item1;
                var records = new Dictionary<Key, int> { [new Key("x", 1)] = 4 };
                records[new Key("x", 1)] += 2;
                IReadOnlyDictionary<Key, int> view = records;
                var removed = records.Remove(new Key("x", 1), out var old);
                var missing = !records.TryGetValue(new Key("z", 1), out var fallback);
                return new { found, duplicate, total, stored = map[("c", 3)].Item1, removed, old, missing, fallback, count = view.Count };
            }
        }
        """);

    [Fact]
    public void CompositeSetsAndLinqDictionaryRespectRecordAndMemberIdentity() => DifferentialRunner.Equal("""
        public abstract record Key(int Value);
        public sealed record A(int Value) : Key(Value);
        public sealed record B(int Value) : Key(Value);
        public sealed record ArrayKey(int[] Values);
        public static class Test
        {
            public static object Run()
            {
                var set = new HashSet<Key> { new A(1), new A(1), new B(1), null! };
                var added = set.Add(new A(1)); var has = set.Contains(new B(1));
                set.UnionWith(new Key[] { new A(2), new B(1) });
                set.IntersectWith(new Key[] { new A(1), new A(2), new B(1) });
                var same = set.SetEquals(new Key[] { new B(1), new A(1), new A(2), new A(2) });
                var array = new[] { 1, 2 }; var arrays = new HashSet<ArrayKey> { new(array), new(array), new(new[] { 1, 2 }) };
                var map = new[] { 1, 2 }.ToDictionary(n => ("tenant", n), n => n * 3);
                var tuples = new HashSet<(int, int)> { (1, 2), (1, 2) };
                var tuple = (3, 4); tuples.Add(tuple); tuple.Item1 = 99;
                return new { count = set.Count, added, has, same, arrays = arrays.Count, value = map[("tenant", 2)], tupleCount = tuples.Count, tupleFound = tuples.Contains((3, 4)) };
            }
        }
        """);
    [Fact]
    public void MulticastDelegatesPreserveIdentitySnapshotsAndRemoval() => DifferentialRunner.Equal("""
        public sealed class Counter(int factor)
        {
            public static string Trace = "";
            public int Apply(int n) { Trace += factor; return n * factor; }
        }
        public static class Test
        {
            public static object Run()
            {
                var a = new Counter(2); var b = new Counter(3);
                Func<int, int> first = a.Apply, second = b.Apply;
                var equal = first == a.Apply;
                var different = first == b.Apply;
                var pair = first + second;
                var all = pair + pair + first;
                var snapshot = all;
                all -= pair;
                all -= a.Apply;
                var value = all(4);
                var old = snapshot(1);
                var empty = first - a.Apply;
                return new { equal, different, value, old, trace = Counter.Trace, empty = empty == null };
            }
        }
        """);

    [Fact]
    public void FieldCustomStaticAndInterfaceEventsUseAccessors() => DifferentialRunner.Equal("""
        public interface ISource { event Action<int> Changed; }
        public class Source : ISource
        {
            public event Action<int>? Changed;
            public static event Action<int>? Global;
            private Action<int>? custom;
            public event Action<int> Custom { add { custom += value; } remove { custom -= value; } }
            public void Raise(int value) { Changed?.Invoke(value); custom?.Invoke(value + 1); Global?.Invoke(value + 2); }
        }
        public static class Test
        {
            private static int total;
            private static void Add(int value) { total += value; }
            public static object Run()
            {
                var source = new Source(); ISource contract = source;
                contract.Changed += Add; contract.Changed += Add; contract.Changed -= Add;
                source.Custom += Add; Source.Global += Add;
                source.Raise(3);
                contract.Changed -= Add; source.Custom -= Add; Source.Global -= Add;
                source.Raise(10);
                return total;
            }
        }
        """);
    [Fact]
    public void UserIndexersPreserveDispatchAndEvaluationOrder() => DifferentialRunner.Equal("""
        public interface ITable { int this[int row, int col] { get; set; } }
        public class Table : ITable
        {
            private int value;
            public virtual int this[int row, int col] { get => value + row + col; set { this.value = value - row - col; } }
        }
        public sealed class Child : Table
        {
            public override int this[int row, int col] { get => base[row, col] * 2; }
            public int ReadBase() => base[1, 2];
        }
        public static class Test
        {
            private static string trace = "";
            private static Child child = new();
            private static Child Receiver() { trace += "r"; return child; }
            private static int Arg(int n) { trace += n; return n; }
            public static object Run()
            {
                Receiver()[col: Arg(2), row: Arg(1)] = Arg(9);
                ITable table = child;
                var before = table[1, 2]++;
                table[1, 2] += 4;
                return new { trace, before, current = child[1, 2], parent = child.ReadBase() };
            }
        }
        """);

    [Fact]
    public void StaticInterfaceFactoriesUseRoslynImplementations() => DifferentialRunner.Equal("""
        public interface IFactory<T> where T : IFactory<T>
        {
            static abstract T Make(int value);
            static abstract int Code { get; }
            static virtual int Default() => 7;
        }
        public sealed class Item(int value) : IFactory<Item>
        {
            public int Value { get; } = value;
            static Item IFactory<Item>.Make(int value) => new Item(value + 1);
            static int IFactory<Item>.Code => 3;
        }
        public static class Test
        {
            private static int Build<T>(int value) where T : IFactory<T> => T.Make(value).GetHashCode();
            private static T Create<T>(int value) where T : IFactory<T> => T.Make(value);
            private static int Code<T>() where T : IFactory<T> => T.Code + T.Default();
            public static object Run() => new { value = Create<Item>(5).Value, code = Code<Item>() };
        }
        """);
}
