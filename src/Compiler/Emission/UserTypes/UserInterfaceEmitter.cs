using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private readonly HashSet<INamedTypeSymbol> _requestedInterfaces = new(SymbolEqualityComparer.Default);
    private static bool IsSourceInterface(INamedTypeSymbol type) => type.TypeKind == TypeKind.Interface
        && type.DeclaringSyntaxReferences.Length != 0;

    private string InterfaceMarker(INamedTypeSymbol type)
    {
        type = (INamedTypeSymbol)ResolveType(type);
        _requestedInterfaces.Add(type);
        return _names.Get($"interface-marker:{type.ToDisplayString()}", $"implements${type.Name}");
    }

    private void EmitVariantInterfaceMembers()
    {
        foreach (var contract in _requestedInterfaces.ToArray())
            foreach (var type in _userTypes.Keys)
            {
                if (type.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default)
                    || !_compilation.ClassifyConversion(type, contract).IsImplicit) continue;
                var prototype = _userTypes[type] + ".prototype";
                _output.Append("Object.defineProperty(").Append(prototype).Append(", ")
                    .Append(InterfaceMarker(contract))
                    .AppendLine(", { value: true });");
                foreach (var member in contract.GetMembers())
                {
                    var implementation = FindInterfaceImplementation(type, member);
                    if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsGenericMethod: false } method
                        && implementation is IMethodSymbol target)
                    {
                        var parameters = method.Parameters.Select((_, index) => $"$arg{index}").ToArray();
                        _output.Append(prototype).Append('.').Append(UserInstanceMethodName(method)).Append(" = function(")
                            .Append(string.Join(", ", parameters)).Append(") { return this.")
                            .Append(UserInstanceMethodName(target)).Append('(').Append(string.Join(", ", parameters)).AppendLine("); };");
                    }
                    else if (member is IPropertySymbol property && implementation is IPropertySymbol targetProperty)
                    {
                        _output.Append("Object.defineProperty(").Append(prototype).Append(", ")
                            .Append(System.Text.Json.JsonSerializer.Serialize(UserMemberName(property))).Append(", { ");
                        if (property.GetMethod is not null)
                            _output.Append("get() { return ").Append(UserMemberAccess("this", targetProperty)).Append("; }, ");
                        if (property.SetMethod is not null)
                            _output.Append("set(value) { ").Append(UserMemberAccess("this", targetProperty)).Append(" = value; }, ");
                        _output.AppendLine("});");
                    }
                }
            }
    }

    private void EmitUserInterfaceMembers(INamedTypeSymbol type, TypeDeclarationSyntax declaration)
    {
        foreach (var contract in type.AllInterfaces.Where(IsSourceInterface))
        {
            _output.Append("  get [").Append(InterfaceMarker(contract)).AppendLine("]() { return true; }");
            foreach (var member in contract.GetMembers())
            {
                if (member.IsStatic || member.DeclaredAccessibility == Accessibility.Private) continue;
                if (member is IMethodSymbol { MethodKind: MethodKind.ExplicitInterfaceImplementation }
                    or IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 }) continue;
                var implementation = type.FindImplementationForInterfaceMember(member);
                switch (member)
                {
                    case IMethodSymbol { MethodKind: MethodKind.Ordinary, IsGenericMethod: false } method
                        when implementation is IMethodSymbol target:
                        var parameters = method.Parameters.Select((_, index) => $"$arg{index}").ToArray();
                        _output.Append("  ").Append(UserInstanceMethodName(method)).Append('(')
                            .Append(string.Join(", ", parameters)).Append(") { return ");
                        if (IsSourceInterface(target.ContainingType))
                            _output.Append(QueueUserMethod(target, declaration)).Append(".call(this")
                                .Append(parameters.Length == 0 ? "" : ", " + string.Join(", ", parameters));
                        else
                            _output.Append("this.").Append(UserInstanceMethodName(target)).Append('(')
                                .Append(string.Join(", ", parameters));
                        _output.AppendLine("); }");
                        break;
                    case IPropertySymbol { IsIndexer: false } property when implementation is IPropertySymbol target:
                        var name = UserMemberName(property);
                        if (property.GetMethod is not null)
                            _output.Append("  get ").Append(name).Append("() { return ")
                                .Append(IsSourceInterface(target.ContainingType)
                                    ? StaticGetter(target) + ".call(this)" : UserMemberAccess("this", target)).AppendLine("; }");
                        if (property.SetMethod is not null)
                            _output.Append("  set ").Append(name).Append("($value) { ")
                                .Append(UserMemberAccess("this", target)).AppendLine(" = $value; }");
                        break;
                    case IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet }:
                    case IMethodSymbol { IsGenericMethod: true }:
                        break;
                    default:
                        throw UnsupportedSymbol(member, declaration);
                }
            }
        }
    }

    private ISymbol? FindInterfaceImplementation(INamedTypeSymbol type, ISymbol member)
    {
        var exact = type.FindImplementationForInterfaceMember(member);
        if (exact is not null) return exact;
        foreach (var contract in type.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, member.ContainingType.OriginalDefinition)
                || !_compilation.ClassifyConversion(contract, member.ContainingType).IsImplicit) continue;
            var compatible = contract.GetMembers(member.Name).First(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, member.OriginalDefinition));
            return type.FindImplementationForInterfaceMember(compatible);
        }
        return null;
    }
}
