namespace Workers.Compiler.Tests;
// Extension block members in initializers, conditional access, deconstruction, async and iterator
// bodies, constrained generic receivers and operators.
public sealed class ExtensionBlockMemberFormTests
{
    private const string Types = """
        public sealed class Box { public int V; public string? Label; }
        public interface IShape { int Area(); }
        public sealed class Square(int side) : IShape { public int Area() => side * side; }
        public static class Ext
        {
            extension(Box box)
            {
                public int Value { get => box.V; set => box.V = value; }
                public string? Name { get => box.Label; set => box.Label = value; }
                public async Task<int> LaterAsync() { await Task.CompletedTask; return box.V; }
                public IEnumerable<int> Upto() { for (var i = 0; i < box.V; i++) yield return i; }
            }
            extension<T>(T shape) where T : IShape
            {
                public int Doubled => shape.Area() * 2;
            }
        }
        """;
    [Fact]
    public void ExtensionPropertiesCanBeSetInObjectInitializers() => DifferentialRunner.Equal(Types + "public static class Test { public static object Run() { var b = new Box { Value = 4 }; return b.V; } }");
    [Fact]
    public void ExtensionPropertiesSupportConditionalAccess() => DifferentialRunner.Equal(Types + "public static class Test { public static object Run() { Box? n = null; Box? b = new Box { V = 2 }; return new { A = n?.Value ?? -1, B = b?.Value ?? -1 }; } }");
    [Fact]
    public void ExtensionPropertiesCanBeDeconstructionAndCoalescingTargets() => DifferentialRunner.Equal(Types + "public static class Test { public static object Run() { var b = new Box(); int x; (b.Value, x) = (5, 6); b.Name ??= \"n\"; b.Name ??= \"m\"; return b.V + x + b.Label; } }");
    [Fact]
    public void ExtensionMethodsCanBeAsyncOrIterators() => DifferentialRunner.Equal(Types + "public static class Test { public static async Task<object> Run() { var b = new Box { V = 3 }; return await b.LaterAsync() + b.Upto().Sum(); } }");
    [Fact]
    public void ConstrainedGenericReceiversAreSupported() => DifferentialRunner.Equal(Types + "public static class Test { public static object Run() => new Square(3).Doubled; }");
    [Fact]
    public void ExtensionOperatorsAreSupported() => DifferentialRunner.Equal("""
        public sealed record Money(int Cents);
        public static class MoneyOps { extension(Money) { public static Money operator +(Money a, Money b) => new(a.Cents + b.Cents); } }
        public static class Test { public static object Run() => (new Money(1) + new Money(2)).Cents; }
        """);
}
