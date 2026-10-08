using Microsoft.CodeAnalysis;
using System.Text.Json;

// Enum.ToString(): the member name for defined values, the number otherwise, and for [Flags] enums
// the names of the set flags in ascending value order separated by ", ".
internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<INamedTypeSymbol, string> _enumTextFunctions = new(SymbolEqualityComparer.Default);
    private readonly Queue<INamedTypeSymbol> _pendingEnumTextFunctions = new();

    private static bool IsTextEnum(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration
        && enumeration.DeclaringSyntaxReferences.Length != 0
        && enumeration.EnumUnderlyingType?.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Byte or SpecialType.System_SByte;

    private string EnumText(ITypeSymbol type, string value)
    {
        var enumeration = (INamedTypeSymbol)type.OriginalDefinition;
        if (!_enumTextFunctions.TryGetValue(enumeration, out var name))
        {
            name = _names.Get($"enum-text:{enumeration.ToDisplayString()}", $"enumText${enumeration.Name}");
            _enumTextFunctions.Add(enumeration, name);
            _pendingEnumTextFunctions.Enqueue(enumeration);
        }
        return $"{name}({value})";
    }

    private bool EmitPendingEnumTextFunctions()
    {
        var emitted = false;
        while (_pendingEnumTextFunctions.TryDequeue(out var enumeration))
        {
            emitted = true;
            var members = enumeration.GetMembers().OfType<IFieldSymbol>()
                .Where(field => field.HasConstantValue)
                .Select(field => (field.Name, Value: Convert.ToInt64(field.ConstantValue)))
                .ToList();
            var names = members.GroupBy(member => member.Value).Select(group => group.First()).ToList();
            var flags = enumeration.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.FlagsAttribute");
            _output.Append("function ").Append(_enumTextFunctions[enumeration]).AppendLine("(value) {");
            _output.Append("  const names = new Map([")
                .Append(string.Join(", ", names.Select(member => $"[{member.Value}, {JsonSerializer.Serialize(member.Name)}]")))
                .AppendLine("]);");
            _output.AppendLine("  if (names.has(value)) return names.get(value);");
            if (flags)
            {
                _output.AppendLine("  if (value === 0) return \"0\";");
                _output.AppendLine("  let remaining = value;");
                _output.AppendLine("  const parts = [];");
                _output.AppendLine("  for (const [flag, name] of Array.from(names).filter(([flag]) => flag !== 0).sort((left, right) => right[0] - left[0])) {");
                _output.AppendLine("    if ((remaining & flag) === flag) { parts.unshift(name); remaining &= ~flag; }");
                _output.AppendLine("  }");
                _output.AppendLine("  if (remaining === 0) return parts.join(\", \");");
            }
            _output.AppendLine("  return String(value);");
            _output.AppendLine("}").AppendLine();
        }
        return emitted;
    }
}
