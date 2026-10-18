using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

// Supports the framework Try-pattern methods that report a value through an out argument, such as
// int.TryParse and Dictionary.TryGetValue. Each call lowers to a helper returning [succeeded, value]
// and assigns the value to the out target. "out var" locals are declared before the enclosing
// statement, matching C# scoping, which places them in the enclosing block.
internal sealed partial class JavaScriptEmitter
{
    private HashSet<ISymbol> _declaredOutVariables = new(SymbolEqualityComparer.Default);

    private void DeclareOutVariables(SyntaxNode statement, Action<string> declare, HashSet<string> names)
    {
        var declarations = statement
            .DescendantNodes(node => node == statement
                || node is not (StatementSyntax or CatchClauseSyntax or SwitchExpressionArmSyntax
                    or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<DeclarationExpressionSyntax>()
            .Where(declaration => declaration.Parent is ArgumentSyntax argument && argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
            .Select(declaration => declaration.Designation)
            .OfType<SingleVariableDesignationSyntax>();
        foreach (var designation in declarations)
        {
            var symbol = _model.GetDeclaredSymbol(designation);
            if (symbol is null || !_declaredOutVariables.Add(symbol)) continue;
            declare(LocalDeclarationName(symbol, designation, names));
        }
    }

    private bool TryEmitOutInvocation(IInvocationOperation operation, IMethodSymbol method, out string result)
    {
        var invocation = (InvocationExpressionSyntax)operation.Syntax;
        result = "";
        var outArguments = operation.Arguments.Where(argument => argument.Parameter?.RefKind == RefKind.Out).ToArray();
        if (outArguments.Length == 0) return false;
        if (outArguments.Length != 1 || invocation.ArgumentList.Arguments.Any(argument => argument.NameColon is not null))
            throw UnsupportedSymbol(method, invocation);

        var sourceInputs = operation.Arguments.Where(argument => argument.Parameter?.RefKind != RefKind.Out
            && argument.ArgumentKind != ArgumentKind.DefaultValue).Select(argument => ValueExpression(argument.Value)).ToArray();
        var key = $"out:{invocation.SyntaxTree.FilePath}:{invocation.SpanStart}";
        var inputs = sourceInputs.Select((_, index) => _names.Get($"{key}:argument:{index}", $"arg{index + 1}")).ToArray();
        var receiver = operation.Instance is null ? null : _names.Get(key + ":receiver", "receiver");
        var outType = method.Parameters.Single(parameter => parameter.RefKind == RefKind.Out).Type;
        var fallback = DefaultFieldValue(outType, invocation);
        var owner = method.ContainingType.OriginalDefinition.ToDisplayString();
        var parse = method.ContainingType.SpecialType switch
        {
            SpecialType.System_Int32 => 0,
            SpecialType.System_UInt32 => 1,
            SpecialType.System_Double => 2,
            SpecialType.System_Boolean => 3,
            _ => -1
        };
        string? attempt = (owner, method.Name) switch
        {
            (_, "TryParse") when parse >= 0 && inputs.Length == 1
                && method.Parameters[0].Type.SpecialType == SpecialType.System_String =>
                $"() => {NumericParse(inputs[0], parse)}",
            ("System.Collections.Generic.Queue<T>", "TryDequeue") or ("System.Collections.Generic.Stack<T>", "TryPop") =>
                $"() => {QueueStackTake(receiver!, remove: true)}",
            ("System.Collections.Generic.Queue<T>" or "System.Collections.Generic.Stack<T>", "TryPeek") =>
                $"() => {QueueStackTake(receiver!, remove: false)}",
            _ => null
        };
        string call;
        if (owner == "Workers.Env" && method.Name == "TryGet" && inputs.Length == 1)
            call = $"((environment, key) => environment[key] !== undefined ? [true, environment[key]] : [false, {fallback}])({receiver}, {inputs[0]})";
        else if (attempt is not null)
            call = $"{_helpers.Require(JavaScriptHelper.TryCall)}({attempt}, {fallback})";
        else if (IsSet(method.ContainingType) && IsCompositeKey(method.ContainingType.TypeArguments[0]) && method.Name == "TryGetValue" && inputs.Length == 1)
            call = $"{receiver}.take({inputs[0]}, {fallback}, false)";
        else if (UsesKeyDictionary(method.ContainingType) && method.Name is "TryGetValue" or "Remove" && inputs.Length == 1)
            call = $"{receiver}.take({inputs[0]}, {fallback}, {(method.Name == "Remove" ? "true" : "false")})";
        else if (IsDictionary(method.ContainingType) && method.Name is "TryGetValue" or "Remove" && inputs.Length == 1)
            call = $"{RequireHelperName(JavaScriptHelper.TryCall, "dictionaryTake")}({receiver}, {inputs[0]}, {fallback}, {(method.Name == "Remove" ? "true" : "false")})";
        else
            throw UnsupportedSymbol(method, invocation);

        var output = outArguments[0].Value;
        if (output is IDeclarationExpressionOperation declaration)
        {
            output = declaration.Expression;
            if (output is ILocalReferenceOperation local) DeclareExpressionOutLocal(local);
        }
        string? outputReceiver = null;
        string? outputValue = null;
        string? target;
        if (output is IFieldReferenceOperation { Instance: not null } field
            && field.Instance is not IInstanceReferenceOperation && IsUserInstanceType(field.Field.ContainingType))
        {
            QueueUserType(field.Field.ContainingType, field.Syntax);
            outputReceiver = _names.Get(key + ":outReceiver", "outReceiver");
            outputValue = Expression(field.Instance);
            target = UserMemberAccess(outputReceiver, field.Field);
        }
        else target = output switch
        {
            IDiscardOperation => null,
            ILocalReferenceOperation or IParameterReferenceOperation => Expression(output),
            IFieldReferenceOperation { Instance: IInstanceReferenceOperation } => Expression(output),
            _ => throw Unsupported("WRK108", output.Syntax)
        };
        var outcome = _names.Get(key + ":outcome", "outcome");
        var body = target is null
            ? $"{call}[0]"
            : $"(({outcome}) => ({target} = {CopyValue(outType, outcome + "[1]")}, {outcome}[0]))({call})";
        if (receiver is not null) body = $"({NotNull(receiver)}, {body})";
        var parameters = receiver is null ? inputs : [receiver, .. inputs];
        var values = receiver is null ? sourceInputs : [Expression(operation.Instance!), .. sourceInputs];
        if (outputReceiver is not null)
        {
            parameters = [.. parameters, outputReceiver];
            values = [.. values, outputValue!];
            // Form the field reference before invoking the Try-method, even if it fails.
            body = $"{{ if ({outputReceiver} == null) throw {NullReferenceException()}; return {body}; }}";
        }
        // Evaluate inputs outside TryCall, so exceptions from evaluating arguments propagate.
        result = $"(({string.Join(", ", parameters)}) => {body})({string.Join(", ", values)})";
        return true;
    }

    private void DeclareExpressionOutLocal(ILocalReferenceOperation local)
    {
        if (_declaredOutVariables.Contains(local.Local)) return;
        if (!_expressionTemporaryScopes.TryPeek(out var temporaries)) throw Unsupported("WRK108", local.Syntax);
        _declaredOutVariables.Add(local.Local);
        temporaries.Add(UserIdentifier(local.Local, local.Local.Name));
    }
}
