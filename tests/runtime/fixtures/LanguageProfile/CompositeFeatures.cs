using Workers;

namespace LanguageProfile;

public sealed record UserKey(string Tenant, int Id);

public interface IFactory<T> where T : IFactory<T>
{
    static abstract T Create(int value);
    static abstract int Code { get; }
}

public sealed class FactoryValue(int value) : IFactory<FactoryValue>
{
    public int Value { get; } = value;
    public static FactoryValue Create(int value) => new(value + 1);
    public static int Code => 7;
}

public sealed class UserTable
{
    private Dictionary<UserKey, int> values = new();
    public event Action<int>? Changed;
    public int this[string tenant, int id]
    {
        get => values[new UserKey(tenant, id)];
        set { values[new UserKey(tenant, id)] = value; Changed?.Invoke(value); }
    }
}

public static class CompositeFeatures
{
    private static T Create<T>(int value) where T : IFactory<T> => T.Create(value + T.Code);

    public static Response Run()
    {
        var table = new UserTable();
        var total = 0;
        Action<int> handler = value => total += value;
        table.Changed += handler;
        table.Changed += handler;
        table.Changed -= handler;
        table["tenant", 3] = 4;
        table["tenant", 3] += 2;
        table.Changed -= handler;
        table["tenant", 3]++;
        var keys = new HashSet<(string, int)> { ("tenant", 3), ("tenant", 3) };
        var factory = Create<FactoryValue>(2);
        return Response.Json(new { value = table["tenant", 3], total, count = keys.Count,
            contains = keys.Contains(("tenant", 3)), factory = factory.Value });
    }
}
