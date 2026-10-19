namespace Workers.Compiler.Tests;

// C# 14 `field`: accessors read and write the property's compiler-generated backing field.
public sealed class FieldKeywordTests
{
    [Fact]
    public void AccessorsShareTheBackingField() => DifferentialRunner.Equal("""
        public sealed class Settings
        {
            public static int Loads;
            public string Name { get => field ?? "unnamed"; set => field = value.Trim(); }
            public int Retries { get => field + 1; set; } = 4;
            public int Limit { get; set => field = value < 0 ? 0 : value; } = 10;
            public string Tag { get; set { field = value; Changes++; } } = "start";
            public int Changes { get; private set; }
            public List<string> Items => field ??= Load();
            private static List<string> Load() { Loads++; return new List<string> { "a" }; }
            public int Unset { get => field; set => field = value; }
        }
        public static class Test
        {
            public static object Run()
            {
                var settings = new Settings();
                var initialName = settings.Name;
                settings.Name = "  primary  ";
                var retries = settings.Retries;
                settings.Retries = 1;
                var limit = settings.Limit;
                settings.Limit = -5;
                settings.Tag = "next";
                settings.Items.Add("b");
                return new
                {
                    InitialName = initialName, Name = settings.Name, Retries = retries, RetriesAfter = settings.Retries,
                    Limit = limit, LimitAfter = settings.Limit, Tag = settings.Tag, Changes = settings.Changes,
                    Items = settings.Items.Count, Loads = Settings.Loads, Unset = settings.Unset
                };
            }
        }
        """);

    [Fact]
    public void CompoundAssignmentAndIncrementUseTheAccessors() => DifferentialRunner.Equal("""
        public sealed class Counter
        {
            public int Value { get => field * 10; set => field = value + 1; }
        }
        public static class Test
        {
            public static object Run()
            {
                var counter = new Counter();
                counter.Value += 2;
                var afterCompound = counter.Value;
                counter.Value++;
                return new { AfterCompound = afterCompound, Final = counter.Value };
            }
        }
        """);

    [Fact]
    public void StaticPropertiesKeepTheirBackingFieldInTheStaticHolder() => DifferentialRunner.Equal("""
        public static class Registry
        {
            public static List<string> Log = new();
            public static int Version { get => field; set { Log.Add("set " + value); field = value; } } = Initial();
            public static string Name { get => field ?? "none"; set => field = value.ToUpperInvariant(); }
            private static int Initial() { Log.Add("initial"); return 3; }
            static Registry() { Log.Add("static constructor " + Version); }
        }
        public static class Test
        {
            public static object Run()
            {
                var first = Registry.Version;
                Registry.Version = 5;
                var before = Registry.Name;
                Registry.Name = "main";
                return new { First = first, Version = Registry.Version, Before = before, Name = Registry.Name, Log = Registry.Log };
            }
        }
        """);

    [Fact]
    public void InheritedAndVirtualPropertiesKeepSeparateBackingFields() => DifferentialRunner.Equal("""
        public class Animal
        {
            public string Name { get => field ?? "?"; set => field = value + "!"; }
            public virtual int Legs { get => field; set => field = value; } = 4;
        }
        public sealed class Bird : Animal
        {
            public override int Legs { get => field + base.Legs; set { field = value; base.Legs = 0; } } = 2;
        }
        public static class Test
        {
            public static object Run()
            {
                Animal bird = new Bird();
                var initial = bird.Legs;
                bird.Legs = 3;
                bird.Name = "tweety";
                var plain = new Animal();
                return new { Initial = initial, Legs = bird.Legs, Name = bird.Name, Plain = plain.Legs, PlainName = plain.Name };
            }
        }
        """);

    [Fact]
    public void RecordsCopyAndCompareBackingFields() => DifferentialRunner.Equal("""
        public sealed record Account(string Id)
        {
            public string Owner { get => field ?? "nobody"; init => field = value.Trim(); }
            public int Reads { get; set; }
        }
        public static class Test
        {
            public static object Run()
            {
                var first = new Account("a") { Owner = " ann " };
                var copy = first with { };
                var renamed = first with { Owner = " bob " };
                var unnamed = new Account("a");
                return new
                {
                    Owner = copy.Owner, Renamed = renamed.Owner, Equal = first == copy, Different = first == renamed,
                    UnnamedEqual = unnamed == new Account("a"), Unnamed = unnamed.Owner, Text = first.ToString()
                };
            }
        }
        """);
}
