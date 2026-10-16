using Workers;

namespace LanguageProfile;

public interface ITransform
{
    private int Initial => 3;
    int Read() => Initial;
    T Identity<T>(T value) => value;
}

public sealed class Transform : ITransform;
public abstract record LabelBase(string Label);
public sealed record LabelChild(string Label, int Value) : LabelBase(Label);
public sealed record OptionalResult(Result<User>? Value);

public record CopyBase(int Value);
public sealed record CopyChild : CopyBase
{
    public CopyChild() : base(3) { }
    private CopyChild(CopyChild original) : base(new CopyBase(original.Value + 10)) { }
}

public static class Semantics
{
    public static Response Run()
    {
        ITransform transform = new Transform();
        var parsed = Json.Parse<LabelChild>("""{"label":"inherited","value":7}""")!;
        CopyBase source = new CopyChild();
        return Response.Json(new
        {
            identity = transform.Identity(7),
            helper = transform.Read(),
            label = parsed.Label,
            copied = (source with { }).Value,
            bothNull = new OptionalResult(null) == new OptionalResult(null)
        });
    }
}
