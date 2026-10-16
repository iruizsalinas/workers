using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Text;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private readonly Dictionary<string, INamedTypeSymbol> _builtInExceptions = new(StringComparer.Ordinal);
    private static readonly string[] HelperExceptionTypes =
    [
        "System.InvalidOperationException", "System.ArgumentException", "System.ArgumentNullException",
        "System.ArgumentOutOfRangeException", "System.FormatException", "System.OverflowException",
        "System.DivideByZeroException", "System.IndexOutOfRangeException", "System.NullReferenceException",
        "System.InvalidCastException", "System.NotSupportedException", "System.Collections.Generic.KeyNotFoundException"
    ];

    private string BuiltInExceptionType(INamedTypeSymbol type)
    {
        if (!IsException(type) || type.DeclaringSyntaxReferences.Length != 0) throw UnsupportedSymbol(type, _diagnosticNode!);
        _helpers.Require(JavaScriptHelper.Exceptions);
        for (var current = type; current is not null && IsException(current); current = current.BaseType)
            _builtInExceptions.TryAdd(current.ToDisplayString(), current);
        return $"{_helpers.Name("exceptionType")}({JsonSerializer.Serialize(type.ToDisplayString())})";
    }

    private string NewBuiltInException(string typeName, string message) =>
        $"new ({BuiltInExceptionType(_compilation.GetTypeByMetadataName(typeName)!)})({JsonSerializer.Serialize(message)})";

    private string NullReferenceException() => NewBuiltInException("System.NullReferenceException", "Object reference not set to an instance of an object.");

    private bool NeedsNullCheck(IOperation? instance) => instance?.Type is { } type && ResolveType(type).IsReferenceType
        // JsonValue is an erased JavaScript value: null/undefined are valid payloads,
        // inspected through IsNull/IsUndefined and the typed JSON adapters.
        && type.ToDisplayString() != "Workers.JsonValue"
        && instance is not IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ContainingTypeInstance }
        && instance is not IObjectCreationOperation and not IArrayCreationOperation and not IAnonymousObjectCreationOperation;

    private string NotNull(string value)
    {
        _helpers.Require(JavaScriptHelper.Exceptions);
        return $"{_helpers.Name("notNull")}({value})";
    }

    private string ThrowValue(Microsoft.CodeAnalysis.IOperation operation) =>
        $"({Expression(operation)} ?? {NewBuiltInException("System.NullReferenceException", "Object reference not set to an instance of an object.")})";

    private string EmitBuiltInExceptionTypes()
    {
        if (!_helpers.IsRequired(JavaScriptHelper.Exceptions)) return "";
        foreach (var name in HelperExceptionTypes)
            if (_compilation.GetTypeByMetadataName(name) is { } type) BuiltInExceptionType(type);
        var output = new StringBuilder();
        var registry = _helpers.Name("exceptionTypes");
        output.Append("const ").Append(registry).AppendLine(" = Object.create(null);");
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in _builtInExceptions.Values.ToArray()) Emit(type);
        output.Append("function ").Append(_helpers.Name("exceptionType")).Append("(name) { return ").Append(registry).AppendLine("[name]; }");
        output.Append("function ").Append(_helpers.Name("notNull")).AppendLine("(value) {")
            .Append("  if (value == null) throw new (").Append(registry).AppendLine("[\"System.NullReferenceException\"])(\"Object reference not set to an instance of an object.\");")
            .AppendLine("  return value;").AppendLine("}");
        return output.ToString();

        void Emit(INamedTypeSymbol type)
        {
            var name = type.ToDisplayString();
            if (!emitted.Add(name)) return;
            var parent = type.BaseType;
            if (IsException(parent)) Emit(parent!);
            output.Append(registry).Append('[').Append(JsonSerializer.Serialize(name)).Append("] = class extends ")
                .Append(IsException(parent) ? registry + "[" + JsonSerializer.Serialize(parent!.ToDisplayString()) + "]" : "Error")
                .AppendLine(" {")
                .AppendLine("  constructor(message, options, paramName) {")
                .Append("    super(message ?? ").Append(JsonSerializer.Serialize(DefaultExceptionMessage(type))).AppendLine(", options);")
                .Append("    this.name = ").Append(JsonSerializer.Serialize(type.Name)).AppendLine(";")
                .AppendLine("    if (paramName !== undefined) this.paramName = paramName;")
                .AppendLine("  }").AppendLine("};");
        }
    }
}
