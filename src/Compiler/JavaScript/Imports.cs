using System.Text;

internal sealed class ImportRegistry(GeneratedNameAllocator names)
{
    private readonly Dictionary<(string Module, string Export), string> _imports = new();

    public string Require(string module, string export, string preferredName)
    {
        var key = (module, export);
        if (_imports.TryGetValue(key, out var existing))
            return existing;
        var alias = names.Get($"import:{module}:{export}", preferredName);
        _imports.Add(key, alias);
        return alias;
    }

    public string Emit()
    {
        var output = new StringBuilder();
        foreach (var item in _imports.OrderBy(item => item.Key.Module, StringComparer.Ordinal).ThenBy(item => item.Key.Export, StringComparer.Ordinal))
            output.Append("import { ").Append(item.Key.Export).Append(" as ").Append(item.Value)
                .Append(" } from \"").Append(item.Key.Module).AppendLine("\";");
        return output.ToString();
    }
}
