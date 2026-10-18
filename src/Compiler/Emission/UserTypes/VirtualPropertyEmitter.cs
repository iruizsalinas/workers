using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Text.Json;

internal sealed partial class JavaScriptEmitter
{
    private static bool HasPropertyDescriptor(IPropertySymbol property) =>
        property.IsVirtual || property.IsOverride || property.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is PropertyDeclarationSyntax declaration && !IsAutoProperty(declaration));

    private string UserPropertyStorage(string receiver, IPropertySymbol property) =>
        property.IsVirtual || property.IsOverride
            ? $"{receiver}.{_names.Get($"property-storage:{property.OriginalDefinition.ToDisplayString()}", $"backing${property.ContainingType.Name}${property.Name}")}"
            : UserMemberAccess(receiver, property);

    private void EmitVirtualAutoProperty(PropertyDeclarationSyntax declaration)
    {
        var property = (IPropertySymbol)_model.GetDeclaredSymbol(declaration)!;
        var name = UserMemberName(property);
        var storage = UserPropertyStorage("this", property);
        if (property.GetMethod is not null)
            _output.Append("  get ").Append(name).Append("() { return ").Append(storage).AppendLine("; }");
        if (property.SetMethod is not null)
            _output.Append("  set ").Append(name).Append("(value) { ").Append(storage).AppendLine(" = value; }");
        EmitInheritedPropertyAccessors(property, declaration);
    }

    // A JavaScript accessor descriptor hides both base accessors. C# overrides
    // replace only the declared accessor, so preserve the other accessor explicitly.
    private void EmitInheritedPropertyAccessors(IPropertySymbol property, SyntaxNode source)
    {
        if (property.GetMethod is null && PropertyAccessor(property.OverriddenProperty, getter: true) is { } getter)
            _output.Append("  get ").Append(UserMemberName(property)).Append("() { return ")
                .Append(BasePropertyGet((IPropertySymbol)getter.AssociatedSymbol!, source)).AppendLine("; }");
        if (property.SetMethod is null && PropertyAccessor(property.OverriddenProperty, getter: false) is { } setter)
            _output.Append("  set ").Append(UserMemberName(property)).Append("(value) { ")
                .Append(BasePropertyDescriptor((IPropertySymbol)setter.AssociatedSymbol!, source)).AppendLine(".set.call(this, value); }");
    }

    private static IMethodSymbol? PropertyAccessor(IPropertySymbol? property, bool getter)
    {
        for (; property is not null; property = property.OverriddenProperty)
            if ((getter ? property.GetMethod : property.SetMethod) is { } accessor) return accessor;
        return null;
    }

    private string BasePropertyDescriptor(IPropertySymbol property, SyntaxNode source)
    {
        var owner = QueueUserType(property.ContainingType, source);
        var name = JsonSerializer.Serialize(UserMemberName(property));
        return $"Object.getOwnPropertyDescriptor({owner}.prototype, {name})";
    }

    private string BasePropertyGet(IPropertySymbol property, SyntaxNode source) =>
        $"{BasePropertyDescriptor(property, source)}.get.call(this)";

    private string BasePropertySet(IPropertySymbol property, SyntaxNode source, string value)
    {
        return $"(($value) => {{ {BasePropertyDescriptor(property, source)}.set.call(this, $value); return $value; }})({value})";
    }

    private string BasePropertyReference(IPropertySymbol property, SyntaxNode source)
    {
        var receiver = _names.Get($"base-property:{source.SyntaxTree.FilePath}:{source.SpanStart}", "receiver");
        var descriptor = BasePropertyDescriptor(property, source);
        return $"(({receiver}) => ({{ get value() {{ return {descriptor}.get.call({receiver}); }}, " +
            $"set value(value) {{ {descriptor}.set.call({receiver}, value); }} }}))(this)";
    }
}
