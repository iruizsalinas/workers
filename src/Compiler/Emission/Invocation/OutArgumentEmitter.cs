using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Supports the framework Try-pattern methods that report a value through an out argument, such as
// int.TryParse and Dictionary.TryGetValue. Each call lowers to a helper returning [succeeded, value]
// and assigns the value to the out target. "out var" locals are declared before the enclosing
// statement, matching C# scoping, which places them in the enclosing block.
internal sealed partial class JavaScriptEmitter
{
    private readonly HashSet<ISymbol> _declaredOutVariables = new(SymbolEqualityComparer.Default);

    private void EmitOutVariableDeclarations(StatementSyntax statement, string indent)
    {
        var declarations = statement
            .DescendantNodes(node => node == statement
                || node is not (StatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<DeclarationExpressionSyntax>()
            .Where(declaration => declaration.Parent is ArgumentSyntax argument && argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
            .Select(declaration => declaration.Designation)
            .OfType<SingleVariableDesignationSyntax>();
        foreach (var designation in declarations)
        {
            var symbol = _model.GetDeclaredSymbol(designation);
            if (symbol is null || !_declaredOutVariables.Add(symbol)) continue;
            _output.Append(indent).Append("let ").Append(UserIdentifier(symbol, designation.Identifier)).AppendLine(";");
        }
    }

    private bool TryEmitOutInvocation(InvocationExpressionSyntax invocation, IMethodSymbol method, out string result)
    {
        result = "";
        var outArguments = invocation.ArgumentList.Arguments
            .Where(argument => argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)).ToArray();
        if (outArguments.Length == 0) return false;
        if (outArguments.Length != 1 || invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null))
            throw UnsupportedSymbol(method, invocation);

        var inputs = invocation.ArgumentList.Arguments.Where(argument => argument != outArguments[0])
            .Select(argument => Expression(argument.Expression)).ToArray();
        var receiver = invocation.Expression is MemberAccessExpressionSyntax member && !method.IsStatic
            ? Expression(member.Expression)
            : null;
        var outType = method.Parameters.Single(parameter => parameter.RefKind == RefKind.Out).Type;
        var fallback = DefaultFieldValue(outType, invocation);
        var owner = method.ContainingType.OriginalDefinition.ToDisplayString();
        var parse = method.ContainingType.SpecialType switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_UInt32 => 1,
            SpecialType.System_Single => 2,
            SpecialType.System_Double => 3,
            SpecialType.System_Boolean => 4,
            _ => -1
        };
        var invariantProvider = inputs.Length == 2
            && method.Parameters[1].Type is { Name: "IFormatProvider", ContainingNamespace.Name: "System" }
            && IsInvariantCulture(invocation.ArgumentList.Arguments[1].Expression);
        string? attempt = (owner, method.Name) switch
        {
            (_, "TryParse") when parse >= 0 && (inputs.Length == 1 || invariantProvider)
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                $"() => {NumericParse(inputs[0], parse)}",
            ("System.DateOnly" or "System.TimeOnly", "TryParseExact") =>
                $"() => {DateOrTimeOnlyParse(invocation, method, inputs[0])}",
            ("System.DateOnly" or "System.TimeOnly", "TryParse") =>
                DateOrTimeOnlyStaticInvocation(invocation, method, method.Name, inputs),
            ("System.Guid", "TryParse") when inputs.Length == 1
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                $"() => {HelperInvocation(JavaScriptHelper.GuidParse, inputs)}",
            ("System.Text.Json.JsonElement", "TryGetProperty") when inputs.Length == 1
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                $"() => {HelperInvocation(JavaScriptHelper.JsonElementGetProperty, [receiver!, inputs[0]])}",
            ("System.Collections.Generic.Queue<T>", "TryDequeue") or ("System.Collections.Generic.Stack<T>", "TryPop") =>
                $"() => {QueueStackTake(receiver!, remove: true)}",
            ("System.Collections.Generic.Queue<T>" or "System.Collections.Generic.Stack<T>", "TryPeek") =>
                $"() => {QueueStackTake(receiver!, remove: false)}",
            _ => null
        };
        string call;
        if (JsonNodeKind(method.ContainingType) == JsonNodeObject && method.Name == "TryGetPropertyValue" && inputs.Length == 1)
            call = JsonNodeHelper("jsonObjectTake", receiver!, inputs[0]);
        else if (JsonNodeKind(method.ContainingType) == JsonNodeValue && method.Name == "TryGetValue" && inputs.Length == 0)
        {
            var (kind, arguments) = JsonNodeValueKind(method.TypeArguments[0], invocation);
            call = JsonNodeHelper("jsonNodeTryValue", receiver!, fallback, kind.ToString(), arguments);
        }
        else if (owner == "Workers.Env" && method.Name == "TryGet" && inputs.Length == 1)
            call = $"((environment, key) => environment[key] !== undefined ? [true, environment[key]] : [false, {fallback}])({receiver}, {inputs[0]})";
        else if (attempt is not null)
            call = $"{_helpers.Require(JavaScriptHelper.TryCall)}({attempt}, {fallback})";
        else if (IsDictionary(method.ContainingType) && method.Name is "TryGetValue" or "Remove" && inputs.Length == 1)
            call = $"{RequireHelperName(JavaScriptHelper.TryCall, "dictionaryTake")}({receiver}, {inputs[0]}, {fallback}, {(method.Name == "Remove" ? "true" : "false")})";
        else
            throw UnsupportedSymbol(method, invocation);

        var target = outArguments[0].Expression switch
        {
            DeclarationExpressionSyntax { Designation: DiscardDesignationSyntax } => null,
            IdentifierNameSyntax { Identifier.ValueText: "_" } discard
                when _model.GetSymbolInfo(discard).Symbol is null or IDiscardSymbol => null,
            DeclarationExpressionSyntax { Designation: SingleVariableDesignationSyntax designation } =>
                _model.GetDeclaredSymbol(designation) is { } symbol && _declaredOutVariables.Contains(symbol)
                    ? UserIdentifier(symbol, designation.Identifier)
                    : throw new NotSupportedException(
                        "WRK108: An 'out var' declaration is only supported inside a statement; use a block body."),
            var existing when IsSimpleMutationTarget(existing) => Expression(existing),
            var other => throw Unsupported("WRK108", other)
        };
        var outcome = _names.Get($"out:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}", "outcome");
        result = target is null
            ? $"{call}[0]"
            : $"(({outcome}) => ({target} = {outcome}[1], {outcome}[0]))({call})";
        return true;
    }
}
