namespace Workers.Compiler.Tests;

public sealed class InheritanceTests
{
    [Theory]
    [InlineData("base.Value = 4")]
    [InlineData("base.Value += 4")]
    [InlineData("base.Value++")]
    [InlineData("++base.Value")]
    public void NonvirtualBaseComputedPropertiesSupportAssignments(string update) => DifferentialRunner.Equal($$"""
        public class Base
        {
            private int stored = 2;
            public int Value { get => stored; set => stored = value; }
        }
        public sealed class Child : Base
        {
            public object Change()
            {
                var result = {{update}};
                return new { result, stored = base.Value };
            }
        }
        public static class Test { public static object Run() => new Child().Change(); }
        """);

    [Fact]
    public void BaseAutoPropertiesSupportCoalesceAndDeconstructionAssignments() => DifferentialRunner.Equal("""
        public class Base { public string Value { get; set; } = "kept"; }
        public sealed class Child : Base
        {
            public object Change()
            {
                var present = base.Value ??= "unused";
                (base.Value, var other) = ("assigned", 3);
                return new { present, stored = base.Value, other };
            }
        }
        public static class Test { public static object Run() => new Child().Change(); }
        """);

    [Fact]
    public void BaseEventSubscriptionsBypassOverriddenAccessors() => DifferentialRunner.Equal("""
        public class Base
        {
            public int BaseAdds;
            public int BaseRemoves;
            public virtual event System.Action Changed
            {
                add { BaseAdds++; }
                remove { BaseRemoves++; }
            }
        }
        public sealed class Child : Base
        {
            public int ChildAdds;
            public int ChildRemoves;
            public override event System.Action Changed
            {
                add { ChildAdds++; }
                remove { ChildRemoves++; }
            }
            public void Subscribe(System.Action handler) { base.Changed += handler; base.Changed -= handler; }
        }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child();
                child.Subscribe(() => { });
                child.Changed += () => { };
                return new { baseAdds = child.BaseAdds, baseRemoves = child.BaseRemoves,
                    childAdds = child.ChildAdds, childRemoves = child.ChildRemoves };
            }
        }
        """);

    [Fact]
    public void BaseCoalesceAssignmentReadsTheBaseGetterAndSkipsUnusedFallbacks() => DifferentialRunner.Equal("""
        public class Base { public virtual string Value { get; set; } = "kept"; }
        public sealed class Child : Base
        {
            public override string Value { get; set; } = "child";
            private int calls;
            private string Fallback() { calls++; return "fallback"; }
            public object Change()
            {
                var present = base.Value ??= Fallback();
                base.Value = null;
                var absent = base.Value ??= Fallback();
                return new { present, absent, stored = base.Value, derived = Value, calls };
            }
        }
        public static class Test { public static object Run() => new Child().Change(); }
        """);

    [Fact]
    public void PreservesPrivateBaseSetterUsedByBaseMethods() => DifferentialRunner.Equal("""
        public class Base
        {
            public virtual int Value { get; private set; }
            public void Update(int value) { Value = value; }
        }
        public sealed class Child : Base { public override int Value => base.Value * 10; }
        public static class Test
        {
            public static object Run() { var child = new Child(); child.Update(4); return new { value = child.Value }; }
        }
        """);

    [Fact]
    public void InheritsSetterWhenOverridingOnlyComputedGetter() => DifferentialRunner.Equal("""
        public class Base { public virtual int Value { get; set; } }
        public sealed class Child : Base { public override int Value => base.Value * 10; }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child();
                child.Value = 4;
                var direct = child.Value;
                Base parent = child;
                parent.Value = 7;
                return new { direct, throughBase = child.Value };
            }
        }
        """);

    [Fact]
    public void UsesInheritedSetterInBaseAssignmentsAndCompoundUpdates() => DifferentialRunner.Equal("""
        public class Base { public virtual int Value { get; set; } }
        public class Middle : Base { public override int Value => base.Value * 10; }
        public sealed class Child : Middle
        {
            public int Change()
            {
                base.Value = 4;
                var before = base.Value++;
                base.Value += 2;
                return before;
            }
        }
        public static class Test
        {
            public static object Run() { var child = new Child(); return new { before = child.Change(), after = child.Value }; }
        }
        """);

    [Fact]
    public void InheritsSetterAcrossMultipleGenericGetterOverrides() => DifferentialRunner.Equal("""
        public class Base<T> { public virtual T Value { get; set; } }
        public class Middle<T> : Base<T> { public override T Value => base.Value; }
        public sealed class Child : Middle<int> { public override int Value => base.Value + 10; }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child { Value = 4 }; child.Value += 1;
                return new { value = child.Value };
            }
        }
        """);

    [Fact]
    public void DoesNotTreatJsonPropertyNamesAsInterfaceIdentity() => DifferentialRunner.Equal("""
        public interface IValue { }
        public sealed class Item : IValue { }
        public static class Test
        {
            public static object Run()
            {
                object fake = new System.Collections.Generic.Dictionary<string, bool>
                    { ["$workers$implements$IValue"] = true };
                object real = new Item();
                return new { fake = fake is IValue, real = real is IValue };
            }
        }
        """);
    [Fact]
    public void EvaluatesInterfaceTypeTestSubjectsOnce() => DifferentialRunner.Equal("""
        public interface IValue { }
        public sealed class Item : IValue { }
        public static class Test
        {
            private static int calls;
            private static object Next() { calls++; return new Item(); }
            public static object Run() => new { matched = Next() is IValue, calls };
        }
        """);
    [Fact]
    public void IncludesCustomBaseRecordEqualityInDerivedEquality() => DifferentialRunner.Equal("""
        public record Base(int X)
        {
            public virtual bool Equals(Base other) => other is not null && X % 2 == other.X % 2;
            public override int GetHashCode() => X % 2;
        }
        public sealed record Child(int X, int Y) : Base(X);
        public static class Test
        {
            public static object Run()
            {
                Base a = new Child(1, 7); Base b = new Child(3, 7); Base c = new Child(1, 8);
                return new { a = a == b, b = a == c, c = a.Equals(b), d = a.Equals(c),
                    e = ((Child)a).Equals((object)c), f = ((Child)a).Equals(new object()) };
            }
        }
        """);
    [Fact]
    public void UpdatesBaseAutoPropertyStorageWithoutVirtualDispatch() => DifferentialRunner.Equal("""
        public class Base { public virtual int Value { get; set; } = 3; }
        public sealed class Child : Base
        {
            public override int Value { get; set; } = 7;
            public int Change()
            {
                base.Value = 10;
                var before = base.Value++;
                base.Value += 2;
                return before + base.Value;
            }
        }
        public static class Test
        {
            public static object Run() { var child = new Child(); return new { a = child.Change(), b = child.Value }; }
        }
        """);
    [Fact]
    public void CapturesBaseAndInterfaceDelegatesAndOutDispatch() => DifferentialRunner.Equal("""
        public interface IValue { int Read(); bool TryRead(out int value); }
        public class Base : IValue
        {
            public virtual int Read() => 3;
            public virtual bool TryRead(out int value) { value = Read(); return true; }
        }
        public sealed class Child : Base
        {
            public override int Read() => 7;
            public int ReadBase() { System.Func<int> read = base.Read; return read(); }
            public override bool TryRead(out int value) { value = 9; return true; }
        }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child(); IValue contract = child;
                System.Func<int> read = contract.Read;
                contract.TryRead(out var value);
                return new { a = read(), b = child.ReadBase(), value };
            }
        }
        """);
    [Fact]
    public void SupportsDefaultInterfaceMembers() => DifferentialRunner.Equal("""
        public interface IValue
        {
            int Value { get; }
            int Double => Value * 2;
            int Read() => Double + 1;
        }
        public sealed class Item : IValue { public int Value => 7; }
        public static class Test
        {
            public static object Run()
            {
                IValue value = new Item();
                return new { a = value.Double, b = value.Read() };
            }
        }
        """);
    [Fact]
    public void InvokesCustomRecordCopyConstructorsThroughBaseReferences() => DifferentialRunner.Equal("""
        public record Base
        {
            public int A { get; init; } = 3;
            public Base() { }
            protected Base(Base original) { A = original.A + 1; }
        }
        public sealed record Child : Base
        {
            public int B { get; init; } = 7;
            public Child() { }
            private Child(Child original) : base(original) { B = original.B + 2; }
        }
        public static class Test
        {
            public static object Run()
            {
                Base original = new Child();
                var copy = original with { };
                return new { a = copy.A, b = ((Child)copy).B, original = original.A };
            }
        }
        """);
    [Fact]
    public void PreservesVirtualPropertyStorageAndBaseGetters() => DifferentialRunner.Equal("""
        public class Base
        {
            public virtual int Value { get; set; } = 3;
            public int Read() => Value;
        }
        public sealed class Child : Base
        {
            public override int Value { get; set; } = 7;
            public int ReadBase() => base.Value;
        }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child(); Base parent = child;
                var before = child.ReadBase(); parent.Value = 9;
                return new { a = parent.Read(), b = child.Value, before, after = child.ReadBase() };
            }
        }
        """);

    [Fact]
    public void PreservesRecordSubtypeEqualityCopyingAndText() => DifferentialRunner.Equal("""
        public abstract record Result(string Label);
        public sealed record Success(string Label, int Value) : Result(Label);
        public sealed record Failure(string Label) : Result(Label);
        public static class Test
        {
            public static object Run()
            {
                Result a = new Success("ok", 7);
                Result b = new Success("ok", 8);
                Result c = new Success("ok", 7);
                Result d = new Failure("ok");
                var copy = a with { Label = "copy" };
                return new { a = a == b, b = a == c, c = a == d, text = a.ToString(),
                    copied = ((Success)copy).Value, label = copy.Label, original = a.Label };
            }
        }
        """);
    [Fact]
    public void PreservesVirtualDispatchBaseCallsAndHiddenMembers() => DifferentialRunner.Equal("""
        public class Base
        {
            public int Value = 3;
            public virtual int Get() => Value;
            public int Plain() => 4;
        }
        public sealed class Child : Base
        {
            public new int Value = 7;
            public override int Get() => base.Get() + Value;
            public new int Plain() => 8;
        }
        public static class Test
        {
            public static object Run()
            {
                var child = new Child();
                Base parent = child;
                return new { a = parent.Get(), b = parent.Plain(), c = child.Plain(),
                    d = parent.Value, e = child.Value, f = parent is Child, g = ((Child)parent).Value };
            }
        }
        """);

    [Fact]
    public void InitializesDerivedFieldsBeforeBaseArgumentsAndBaseBody() => DifferentialRunner.Equal("""
        public class Base
        {
            private int initial = Test.Mark(2);
            public Base(int value) { Test.Mark(value); Test.Mark(Get()); }
            public virtual int Get() => initial;
        }
        public sealed class Child : Base
        {
            private int initial = Test.Mark(1);
            public Child() : this(Test.Mark(3)) { Test.Mark(5); }
            public Child(int value) : base(Test.Mark(4)) { Test.Mark(6); }
            public override int Get() => initial;
        }
        public static class Test
        {
            private static string trace = "";
            public static int Mark(int value) { trace += value; return value; }
            public static object Run() { var child = new Child(); return new { trace, value = child.Get() }; }
        }
        """);

    [Fact]
    public void DispatchesImplicitAndExplicitInterfacesAndTypeTests() => DifferentialRunner.Equal("""
        public interface IValue { int Value { get; } int Get(int add); }
        public interface IOther { int Get(int add); }
        public class Base : IValue
        {
            public int Value { get; set; } = 3;
            public virtual int Get(int add) => Value + add;
        }
        public sealed class Child : Base, IOther
        {
            public override int Get(int add) => base.Get(add) * 2;
            int IOther.Get(int add) => add + 10;
        }
        public static class Test
        {
            public static object Run()
            {
                object source = new Child();
                IValue value = (IValue)source;
                IOther other = source as IOther;
                return new { a = value.Get(4), b = value.Value, c = other.Get(5),
                    d = source is IValue, e = new Base() is IOther, f = new Base() as IOther == null };
            }
        }
        """);

    [Fact]
    public void SupportsAbstractRecordResultsAndSwitchDispatch() => DifferentialRunner.Equal("""
        public abstract record Result(string Label);
        public sealed record Success(string Label, int Value) : Result(Label);
        public sealed record Failure(string Label) : Result(Label);
        public static class Test
        {
            private static int Read(Result result) => result switch { Success value => value.Value, Failure => -1, _ => 0 };
            public static object Run()
            {
                Result a = new Success("ok", 7);
                Result b = new Failure("no");
                return new { a = Read(a), b = Read(b), label = a.Label };
            }
        }
        """);
}
