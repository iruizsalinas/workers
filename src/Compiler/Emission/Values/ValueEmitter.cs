using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string FormEntryMember(MemberAccessExpressionSyntax member, IPropertySymbol property)
    {
        var receiver = Expression(member.Expression);
        var value = _names.Get(
            $"form-entry:{member.SyntaxTree.FilePath}:{member.SpanStart}",
            "formEntry");
        return property.Name switch
        {
            "File" => $"(({value}) => {value} instanceof File ? {value} : null)({receiver})",
            "Text" => $"(({value}) => typeof {value} === \"string\" ? {value} : null)({receiver})",
            _ => throw UnsupportedSymbol(property, member)
        };
    }

    private static bool IsDictionary(ITypeSymbol? type) => type is INamedTypeSymbol named
        && (named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.Dictionary<TKey, TValue>" or
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.IDictionary<TKey, TValue>" or
                "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"));

    private static bool IsSet(ITypeSymbol? type) => type is INamedTypeSymbol named
        && (named.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.HashSet<T>" or
                "System.Collections.Generic.ISet<T>" or
                "System.Collections.Generic.IReadOnlySet<T>"
            || named.AllInterfaces.Any(item => item.OriginalDefinition.ToDisplayString() is
                "System.Collections.Generic.ISet<T>" or
                "System.Collections.Generic.IReadOnlySet<T>"));

    private static void ThrowIfUnsupportedFoundationMember(ISymbol? symbol, SyntaxNode source)
    {
        if (symbol?.ContainingNamespace.ToDisplayString() is { } @namespace
            && (@namespace == "System" || @namespace.StartsWith("System.", StringComparison.Ordinal)))
            throw UnsupportedSymbol(symbol, source);
    }

    private static string Response(string[] arguments) => $"new Response({arguments[0]}{ResponseInit(arguments, 1, 2)})";
    private string Identifier(IdentifierNameSyntax value) => _model.GetSymbolInfo(value).Symbol switch
    {
        IMethodSymbol method when IsMethodGroup(value) => MethodGroup(value, method, null),
        IPropertySymbol { ContainingType: { } type, Name: var name }
            when type.ToDisplayString() == "Workers.WorkerEntrypoint" => name == "Environment" ? "this.env" : "this.ctx",
        IPropertySymbol { IsStatic: false } property when IsUserInstanceType(property.ContainingType) =>
            UserMemberAccess("this", property),
        IFieldSymbol { IsStatic: false } field when IsUserInstanceType(field.ContainingType) =>
            UserMemberAccess("this", field),
        IFieldSymbol { IsStatic: false } field => $"this.{UserIdentifier(field, field.Name)}",
        IFieldSymbol { IsStatic: true, HasConstantValue: true } field => LiteralConstant(field.ConstantValue, value),
        ISymbol staticState when IsUserStaticState(staticState) => StaticMemberAccess(staticState, value),
        IFieldSymbol { IsStatic: true } => throw Unsupported("WRK110", value),
        ISymbol symbol => UserIdentifier(symbol, value.Identifier),
        _ => value.Identifier.ValueText
    };

    private static bool IsMethodGroup(ExpressionSyntax syntax)
    {
        var target = syntax.Parent is MemberAccessExpressionSyntax access && access.Name == syntax ? access : syntax;
        return !(target.Parent is InvocationExpressionSyntax invocation && invocation.Expression == target);
    }

    // A method group converted to a delegate, such as items.Select(Format).
    private string MethodGroup(ExpressionSyntax syntax, IMethodSymbol method, ExpressionSyntax? receiver)
    {
        method = method.ReducedFrom ?? method;
        if (method.IsStatic && !method.IsExtensionMethod && method.MethodKind == MethodKind.Ordinary
            && method.DeclaringSyntaxReferences is [{ } reference]
            && reference.GetSyntax() is MethodDeclarationSyntax)
            return QueueUserMethod(method, syntax);
        if (!method.IsStatic && method.MethodKind == MethodKind.Ordinary
            && method.ContainingType is { } type && IsUserInstanceType(type) && RequiresUserClass(type))
        {
            QueueUserType(type, syntax);
            var key = $"method-group:{syntax.SyntaxTree.FilePath}:{syntax.SpanStart}";
            var target = _names.Get($"{key}:target", "target");
            var parameters = string.Join(", ", method.Parameters.Select((_, index) => _names.Get($"{key}:{index}", $"arg{index + 1}")));
            return $"(({target}) => ({parameters}) => {target}.{UserInstanceMethodName(method)}({parameters}))" +
                   $"({(receiver is null ? "this" : Expression(receiver))})";
        }
        throw new NotSupportedException(
            $"WRK105: Converting the method group '{method.ToDisplayString()}' to a delegate is not supported; use a lambda instead.");
    }

    private string LiteralConstant(object? value, SyntaxNode source) => value switch
    {
        null => "null",
        string text => JsonSerializer.Serialize(text),
        char character => JsonSerializer.Serialize(character.ToString()),
        bool boolean => boolean ? "true" : "false",
        // A float constant is the nearest single-precision value; JavaScript numbers are doubles.
        float single => ((double)single).ToString("R", CultureInfo.InvariantCulture),
        byte or sbyte or short or ushort or int or uint or double =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        long signed when Math.Abs((double)signed) <= 9_007_199_254_740_991d =>
            signed.ToString(CultureInfo.InvariantCulture),
        ulong unsigned when unsigned <= 9_007_199_254_740_991UL =>
            unsigned.ToString(CultureInfo.InvariantCulture),
        _ => throw Unsupported("WRK108", source)
    };

    private static string ResponseInit(
        string[] arguments,
        int statusIndex,
        int? statusTextIndex = null,
        string? headers = null)
    {
        if (arguments.Length <= statusIndex && headers is null) return "";
        var properties = new List<string>();
        if (arguments.Length > statusIndex) properties.Add($"status: {arguments[statusIndex]}");
        if (statusTextIndex is { } textIndex && arguments.Length > textIndex)
            properties.Add($"statusText: {arguments[textIndex]} ?? undefined");
        if (headers is not null) properties.Add($"headers: {headers}");
        return $", {{ {string.Join(", ", properties)} }}";
    }

    private string AnonymousMember(AnonymousObjectMemberDeclaratorSyntax value)
    {
        var name = value.NameEquals?.Name.Identifier.ValueText ?? value.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => LowerFirst(member.Name.Identifier.ValueText),
            _ => throw Unsupported("WRK107", value)
        };
        return $"{JavaScriptObjectKey(name)}: {Expression(value.Expression)}";
    }
}
