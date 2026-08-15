namespace Workers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class DurableObjectAttribute(string? exportName = null) : Attribute
{
    public string? ExportName { get; } = exportName;
}
