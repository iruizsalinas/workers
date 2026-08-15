using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed partial class JavaScriptEmitter
{
    private string Member(MemberAccessExpressionSyntax member)
    {
        var symbol = _model.GetSymbolInfo(member).Symbol;
        var property = symbol as IPropertySymbol;
        if (property is { IsStatic: true, Name: "UtcNow" }
            && property.ContainingType.ToDisplayString() == "System.DateTimeOffset") return "new Date()";
        if (property?.ContainingType.ToDisplayString() == "System.Random" && property.Name == "Shared") return "Math";
        if (property?.ContainingType.ToDisplayString() == "Workers.CacheStorage" && property.Name == "Default")
            return "caches.default";
        if (symbol is IFieldSymbol { ContainingType: { } enumType } field
            && enumType.ToDisplayString() == "Workers.DigestAlgorithm")
            return field.Name switch
            {
                "Sha1" => "\"SHA-1\"",
                "Sha256" => "\"SHA-256\"",
                "Sha384" => "\"SHA-384\"",
                "Sha512" => "\"SHA-512\"",
                _ => throw Unsupported("WRK108", member)
            };
        if (property is { Name: "CompletedTask", ContainingType: { } taskType }
            && taskType.ToDisplayString() is "System.Threading.Tasks.Task" or "System.Threading.Tasks.ValueTask")
            return "undefined";
        if (property?.ContainingType.ToDisplayString() == "Workers.WebSocketPair") return $"{Expression(member.Expression)}[{(property.Name == "Client" ? 0 : 1)}]";
        if (property?.ContainingType.ToDisplayString() == "Workers.Body" && property.Name == "Empty") return "null";
        if (property?.ContainingType.ToDisplayString() == "Workers.HtmlContentOptions") return property.Name == "Html" ? "{ html: true }" : "{ html: false }";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "Path") return $"new URL({Expression(member.Expression)}.url).pathname";
        if (property?.ContainingType.ToDisplayString() == "Workers.Request" && property.Name == "QueryParameters") return $"new URL({Expression(member.Expression)}.url).searchParams";
        return $"{Expression(member.Expression)}.{LowerFirst(member.Name.Identifier.Text)}";
    }

    private string ObjectCreation(BaseObjectCreationExpressionSyntax value)
    {
        var constructor = _model.GetSymbolInfo(value).Symbol as IMethodSymbol;
        var type = constructor?.ContainingType;
        var arguments = value.ArgumentList?.Arguments.ToArray() ?? [];
        if (type?.ToDisplayString() is "Workers.Request" or "Workers.Response")
            return $"new {type.Name}({string.Join(", ", arguments.Select(argument => Expression(argument.Expression)))})";
        if (type?.ToDisplayString() == "Workers.AbortController")
            return "new AbortController()";
        if (type?.ToDisplayString() == "Workers.HtmlRewriter")
            return "new HTMLRewriter()";
        if (type is not null && type.BaseType?.ToDisplayString() is "Workers.HtmlElementHandler" or "Workers.HtmlDocumentHandler")
        {
            if (constructor?.Parameters.Length != 0)
                throw UnsupportedSymbol(constructor, value);
            if (type.DeclaringSyntaxReferences.Length != 1
                || type.DeclaringSyntaxReferences[0].GetSyntax() is not ClassDeclarationSyntax handlerDeclaration
                || handlerDeclaration.Members.Any(member => member is FieldDeclarationSyntax or ConstructorDeclarationSyntax))
                throw UnsupportedSymbol(constructor, value);
            return $"new {UserIdentifier(type, type.Name)}()";
        }

        if (type is not null && BindingIntrinsicRegistry.IsStructuralType(type))
        {
            var properties = new List<string>();
            for (var index = 0; index < arguments.Length; index++)
            {
                var parameterName = arguments[index].NameColon?.Name.Identifier.Text
                    ?? (index < (constructor?.Parameters.Length ?? 0) ? constructor!.Parameters[index].Name : $"value{index}");
                properties.Add($"{LowerFirst(parameterName)}: {Expression(arguments[index].Expression)}");
            }
            if (value.Initializer is not null)
                properties.AddRange(value.Initializer.Expressions.Select(InitializerProperty));
            return "{ " + string.Join(", ", properties) + " }";
        }

        throw UnsupportedSymbol(constructor, value);
    }

    private string InitializerProperty(ExpressionSyntax expression) => expression switch
    {
        AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax name =>
            $"{LowerFirst(name.Identifier.Text)}: {Expression(assignment.Right)}",
        _ => throw Unsupported("WRK106", expression)
    };

    private static string Response(string[] arguments, string _) => $"new Response({arguments[0]}{ResponseInit(arguments, 1)})";
    private string Identifier(IdentifierNameSyntax value) => _model.GetSymbolInfo(value).Symbol switch
    {
        IFieldSymbol { IsStatic: false } field => $"this.{UserIdentifier(field, field.Name)}",
        IFieldSymbol { IsStatic: true } => throw Unsupported("WRK110", value),
        ISymbol symbol => UserIdentifier(symbol, value.Identifier),
        _ => value.Identifier.ValueText
    };
    private static string ResponseInit(string[] arguments, int statusIndex) => arguments.Length > statusIndex ? $", {{ status: {arguments[statusIndex]} }}" : "";
    private string AnonymousMember(AnonymousObjectMemberDeclaratorSyntax value)
    {
        var name = value.NameEquals?.Name.Identifier.Text ?? value.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            MemberAccessExpressionSyntax member => LowerFirst(member.Name.Identifier.Text),
            _ => throw Unsupported("WRK107", value)
        };
        return $"{name}: {Expression(value.Expression)}";
    }
    private string Literal(LiteralExpressionSyntax literal)
    {
        if (literal.IsKind(SyntaxKind.NullLiteralExpression)) return "null";
        if (literal.IsKind(SyntaxKind.TrueLiteralExpression)) return "true";
        if (literal.IsKind(SyntaxKind.FalseLiteralExpression)) return "false";

        var type = _model.GetTypeInfo(literal).Type?.SpecialType ?? SpecialType.None;
        if (type is SpecialType.System_String or SpecialType.System_Char)
            return JsonSerializer.Serialize(literal.Token.ValueText);
        if (type is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
            throw Unsupported("WRK108", literal);
        if (literal.Token.Value is IFormattable value)
            return value.ToString(null, CultureInfo.InvariantCulture) switch
            {
                "NaN" => "Number.NaN",
                "Infinity" => "Number.POSITIVE_INFINITY",
                "-Infinity" => "Number.NEGATIVE_INFINITY",
                var text => text
            };
        throw Unsupported("WRK101", literal);
    }

    private string InterpolatedPart(InterpolatedStringContentSyntax value) => value switch
    {
        InterpolatedStringTextSyntax text => EscapeTemplateText(text.TextToken.ValueText),
        InterpolationSyntax { AlignmentClause: not null } item => throw Unsupported("WRK108", item),
        InterpolationSyntax { FormatClause.FormatStringToken.ValueText: "O" or "o" } item => "${new Date(" + Expression(item.Expression) + ").toISOString()}",
        InterpolationSyntax { FormatClause: not null } item => throw Unsupported("WRK108", item),
        InterpolationSyntax item => "${" + Expression(item.Expression) + " ?? \"\"}",
        _ => throw Unsupported("WRK108", value)
    };
    private static string EscapeTemplateText(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("`", "\\`", StringComparison.Ordinal).Replace("${", "\\${", StringComparison.Ordinal);
    private string Element(CollectionElementSyntax value) => value is ExpressionElementSyntax expression
        ? Expression(expression.Expression)
        : throw Unsupported("WRK102", value);
}
