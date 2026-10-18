using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private void EmitScopeVariableDeclarations(SyntaxNode scope, string indent) =>
        DeclareScopeVariables(scope, name => _output.Append(indent).Append("let ").Append(name).AppendLine(";"));

    private void DeclareScopeVariables(SyntaxNode scope, Action<string> declare)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        DeclareOutVariables(scope, declare, names);
        DeclarePatternVariables(StatementPatternDesignations(scope), declare, names);
    }

    private string LocalDeclarationName(ISymbol symbol, SingleVariableDesignationSyntax designation, HashSet<string> names)
    {
        var name = UserIdentifier(symbol, designation.Identifier);
        // Case-label locals are emitted before the switch; reserve a symbol-specific
        // name so successive switch statements cannot collide in the enclosing block.
        if (designation.Ancestors().Any(node => node is SwitchLabelSyntax) || !names.Add(name))
        {
            name = _names.Get($"local-variable:{designation.SyntaxTree.FilePath}:{designation.SpanStart}", $"local${symbol.Name}");
            _userIdentifiers[symbol] = name;
            names.Add(name);
        }
        return name;
    }

    private void EmitStatement(StatementSyntax statement, int depth) =>
        EmitStatement(_model.GetOperation(statement) ?? throw Unsupported("WRK100", statement), depth);

    private void EmitStatement(IOperation operation, int depth)
    {
        var indent = new string(' ', depth * 2);
        _diagnosticNode = operation.Syntax;
        if (operation.Syntax is StatementSyntax statement
            && operation is not (IForLoopOperation or IWhileLoopOperation))
            EmitScopeVariableDeclarations(statement, indent);
        switch (operation)
        {
            case IReturnOperation value when value.Kind == OperationKind.YieldReturn:
                _output.Append(indent).Append("yield ").Append(ValueExpression(value.ReturnedValue!)).AppendLine(";");
                break;
            case IReturnOperation value when value.Kind is OperationKind.Return or OperationKind.YieldBreak:
                _output.Append(indent).Append("return");
                if (value.ReturnedValue is not null) _output.Append(' ').Append(ValueExpression(value.ReturnedValue));
                _output.AppendLine(";");
                break;
            case IVariableDeclarationGroupOperation declarations:
                foreach (var variable in declarations.Declarations.SelectMany(declaration => declaration.Declarators))
                    _output.Append(indent).Append("let ").Append(VariableDeclaration(variable)).AppendLine(";");
                break;
            case IExpressionStatementOperation expression:
                if (expression.Operation is IDeconstructionAssignmentOperation deconstruction)
                    EmitDeconstructionLocals(deconstruction.Target, depth);
                _output.Append(indent).Append(Expression(expression.Operation)).AppendLine(";");
                break;
            case IConditionalOperation conditional:
                _output.Append(indent).Append("if (").Append(Expression(conditional.Condition)).AppendLine(") {");
                EmitEmbedded(conditional.WhenTrue, depth + 1);
                _output.Append(indent).Append('}');
                if (conditional.WhenFalse is not null)
                {
                    _output.AppendLine(" else {");
                    EmitEmbedded(conditional.WhenFalse, depth + 1);
                    _output.Append(indent).Append('}');
                }
                _output.AppendLine();
                break;
            case IForEachLoopOperation loop:
                var info = _model.GetForEachStatementInfo((CommonForEachStatementSyntax)loop.Syntax);
                if (info.GetEnumeratorMethod is { DeclaringSyntaxReferences.Length: > 0 })
                    throw new NotSupportedException(
                        "WRK108: Source-defined foreach enumeration protocols are outside the Workers profile; " +
                        "use iterator methods returning IEnumerable<T> or IAsyncEnumerable<T> instead.");
                var collection = loop.Collection;
                while (collection is IConversionOperation { IsImplicit: true } conversion) collection = conversion.Operand;
                var enumerable = NeedsNullCheck(collection) ? NotNull(Expression(collection)) : Expression(collection);
                if (collection.Type?.SpecialType == SpecialType.System_String)
                    enumerable = $"{_helpers.Require(JavaScriptHelper.LinqValues)}({enumerable})";
                else if (IsDictionary(collection.Type) && !UsesKeyDictionary(collection.Type)) enumerable = $"Object.entries({enumerable})";
                else if (!loop.IsAsynchronous && !IsArrayBackedList(collection.Type)
                    && collection.Type is INamedTypeSymbol { TypeKind: TypeKind.Interface } contract
                    && (contract.ToDisplayString() == "System.Collections.IEnumerable"
                        || contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                        || contract.AllInterfaces.Any(item => item.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)))
                    enumerable = $"{_helpers.Require(JavaScriptHelper.LinqValues)}({enumerable})";
                if (loop.Syntax is ForEachVariableStatementSyntax deconstructingLoop)
                {
                    var element = _names.Get($"foreach-deconstruction:{loop.Syntax.SyntaxTree.FilePath}:{loop.Syntax.SpanStart}", "element");
                    _output.Append(indent).Append(loop.IsAsynchronous ? "for await (const " : "for (const ")
                        .Append(element).Append(" of ").Append(enumerable).AppendLine(") {");
                    EmitDeconstructionLocals(loop.LoopControlVariable, depth + 1);
                    _output.Append(indent).Append("  ").Append(DeconstructValue(loop.LoopControlVariable,
                        ResolveType(info.ElementType!), element, _model.GetDeconstructionInfo(deconstructingLoop), loop.Syntax)).AppendLine(";");
                    EmitEmbedded(loop.Body, depth + 1);
                    _output.Append(indent).AppendLine("}");
                    break;
                }
                if (loop.Locals.Length != 1 || loop.LoopControlVariable is not IVariableDeclaratorOperation)
                    throw Unsupported("WRK108", loop.Syntax);
                var local = UserIdentifier(loop.Locals[0], loop.Locals[0].Name);
                var item = local;
                string? converted = null;
                if (ResolveType(loop.Locals[0].Type).SpecialType == SpecialType.System_Object && info.ElementType is not null)
                {
                    var candidate = _names.Get($"foreach-object:{loop.Syntax.SyntaxTree.FilePath}:{loop.Syntax.SpanStart}", "item");
                    var boxed = ObjectConversion(info.ElementType, candidate, loop.Syntax);
                    if (boxed != candidate)
                    {
                        item = candidate;
                        converted = boxed;
                    }
                }
                else if (!info.ElementConversion.IsIdentity
                    && !(info.ElementConversion.IsImplicit && info.ElementConversion.IsReference)
                    && loop.Locals[0].Type.SpecialType != SpecialType.System_Object)
                {
                    if (info.ElementConversion.IsUserDefined || info.ElementType is null)
                        throw Unsupported("WRK108", loop.Syntax);
                    var checkedContext = loop.Syntax.Ancestors().OfType<CheckedStatementSyntax>().FirstOrDefault();
                    if (info.ElementConversion.IsExplicit
                        && (info.ElementConversion.IsNumeric || info.ElementConversion.IsNullable || info.ElementConversion.IsEnumeration)
                        && (checkedContext is null ? _compilation.Options.CheckOverflow : checkedContext.Keyword.IsKind(SyntaxKind.CheckedKeyword)))
                        throw Unsupported("WRK108", loop.Syntax);
                    item = _names.Get($"foreach:{loop.Syntax.SyntaxTree.FilePath}:{loop.Syntax.SpanStart}", "item");
                    converted = ForeachElementConversion(info.ElementType, loop.Locals[0].Type, item, loop.Syntax,
                        info.ElementConversion.IsImplicit);
                }
                if (converted is null && CopyValue(loop.Locals[0].Type, "value") != "value")
                {
                    item = _names.Get($"foreach-copy:{loop.Syntax.SyntaxTree.FilePath}:{loop.Syntax.SpanStart}", "item");
                    converted = CopyValue(loop.Locals[0].Type, item);
                }
                _output.Append(indent).Append(loop.IsAsynchronous ? "for await (const " : "for (const ")
                    .Append(item).Append(" of ").Append(enumerable).AppendLine(") {");
                if (converted is not null)
                    _output.Append(indent).Append("  const ").Append(local).Append(" = ").Append(converted).AppendLine(";");
                EmitEmbedded(loop.Body, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case ITryOperation value:
                EmitTry(value, depth);
                break;
            case ISwitchOperation value:
                EmitSwitch(value, depth);
                break;
            case IBranchOperation { BranchKind: BranchKind.Break }:
                _output.Append(indent).AppendLine("break;");
                break;
            case IBranchOperation { BranchKind: BranchKind.Continue } branch:
                _output.Append(indent).Append(_loopContinueTargets.TryGetValue(branch.Target, out var continueTarget)
                    ? $"break {continueTarget};" : "continue;").AppendLine();
                break;
            case IThrowOperation value:
                var exception = value.Exception is not null ? ThrowValue(value.Exception)
                    : _caughtExceptions.TryPeek(out var caught) ? caught : throw Unsupported("WRK108", value.Syntax);
                _output.Append(indent).Append("throw ").Append(exception).AppendLine(";");
                break;
            case IWhileLoopOperation loop:
                EmitWhile(loop, depth);
                break;
            case IForLoopOperation loop:
                EmitFor(loop, depth);
                break;
            case IUsingOperation { Resources: IVariableDeclarationGroupOperation } value:
                var source = (UsingStatementSyntax)value.Syntax;
                _output.Append(indent).AppendLine("{");
                EmitUsing(source.Declaration!, value.IsAsynchronous, body => EmitEmbedded(value.Body, body), depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case IBlockOperation block:
                _output.Append(indent).AppendLine("{");
                EmitStatements(block.Operations, depth + 1);
                _output.Append(indent).AppendLine("}");
                break;
            case IEmptyOperation:
                break;
            default:
                throw Unsupported("WRK100", operation.Syntax);
        }
    }

    private string ForeachElementConversion(ITypeSymbol source, ITypeSymbol target, string value, SyntaxNode node, bool implicitConversion)
    {
        source = ResolveType(source);
        target = ResolveType(target);
        if (target.SpecialType == SpecialType.System_Object) return ObjectConversion(source, value, node);
        if (source.IsReferenceType && target.IsReferenceType)
            return implicitConversion ? value : CheckedReferenceConversion(target, value, node);
        var from = UnwrapNullable(source);
        var to = UnwrapNullable(target);
        var widening = implicitConversion && from.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
            && to.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64;
        var converted = widening ? value : ConvertValue(from, to, value, node);
        if (!IsNullable(source)) return converted;
        return IsNullable(target)
            ? $"{value} == null ? null : {converted}"
            : $"(() => {{ if ({value} == null) throw {NewBuiltInException("System.InvalidOperationException", "Nullable object must have a value.")}; return {converted}; }})()";
    }

    private string VariableDeclaration(IVariableDeclaratorOperation variable) =>
        UserIdentifier(variable.Symbol, variable.Symbol.Name)
        + (variable.Initializer is null ? "" : " = " + ValueExpression(variable.Initializer.Value));

    private string ForInitializer(IOperation operation) => operation switch
    {
        IVariableDeclarationGroupOperation group => "let " + string.Join(", ", group.Declarations
            .SelectMany(declaration => declaration.Declarators).Select(VariableDeclaration)),
        IExpressionStatementOperation expression => Expression(expression.Operation),
        _ => throw Unsupported("WRK100", operation.Syntax)
    };

    private void EmitEmbedded(StatementSyntax statement, int depth) =>
        EmitEmbedded(_model.GetOperation(statement) ?? throw Unsupported("WRK100", statement), depth);

    private void EmitEmbedded(IOperation operation, int depth)
    {
        if (operation is IBlockOperation block) EmitStatements(block.Operations, depth);
        else EmitStatement(operation, depth);
    }

    private void EmitStatements(IReadOnlyList<StatementSyntax> statements, int depth) =>
        EmitStatements(statements.Select(statement => _model.GetOperation(statement)
            ?? throw Unsupported("WRK100", statement)).ToArray(), depth);

    // Using declarations own the remaining operations in their enclosing block.
    private void EmitStatements(IReadOnlyList<IOperation> statements, int depth)
    {
        var frame = new LocalFunctionFrame(statements.OfType<ILocalFunctionOperation>());
        _localFunctionFrames.Push(frame);
        try
        {
            for (var index = 0; index < statements.Count; index++)
            {
                if (statements[index] is ILocalFunctionOperation) continue;
                if (statements[index] is IUsingDeclarationOperation resource)
                {
                    _diagnosticNode = resource.Syntax;
                    EmitScopeVariableDeclarations(resource.Syntax, new string(' ', depth * 2));
                    var local = (LocalDeclarationStatementSyntax)resource.Syntax;
                    var rest = statements.Skip(index + 1).ToArray();
                    EmitUsing(local.Declaration, resource.IsAsynchronous, body => EmitStatements(rest, body), depth);
                    return;
                }
                EmitStatement(statements[index], depth);
            }
        }
        finally
        {
            EmitLocalFunctions(frame, depth);
            _localFunctionFrames.Pop();
        }
    }

    private void EmitUsing(VariableDeclarationSyntax declaration, bool isAsync, Action<int> body, int depth, int index = 0)
    {
        if (index == declaration.Variables.Count)
        {
            body(depth);
            return;
        }
        var indent = new string(' ', depth * 2);
        var variable = declaration.Variables[index];
        var operation = (IVariableDeclaratorOperation)_model.GetOperation(variable)!;
        var local = operation.Symbol;
        var name = UserIdentifier(local, local.Name);
        _output.Append(indent).Append("const ").Append(name).Append(" = ")
            .Append(Expression(operation.Initializer!.Value)).AppendLine(";");
        var dispose = DisposeInvocation(local.Type, name, isAsync, variable);
        _output.Append(indent).AppendLine("try {");
        EmitUsing(declaration, isAsync, body, depth + 1, index + 1);
        _output.Append(indent).AppendLine("} finally {");
        _output.Append(indent).Append("  if (").Append(name).Append(" != null) ")
            .Append(isAsync ? "await " : "").Append(dispose).AppendLine(";");
        _output.Append(indent).AppendLine("}");
    }

    private string DisposeInvocation(ITypeSymbol type, string receiver, bool isAsync, SyntaxNode source)
    {
        var method = (isAsync ? DisposeMethod(type, "System.IAsyncDisposable", "DisposeAsync") : null)
            ?? DisposeMethod(type, "System.IDisposable", "Dispose");
        if (method is not null && BindingIntrinsicRegistry.TryGet(method, out var intrinsic))
            return EmitBindingIntrinsic(receiver, method, intrinsic, []);
        if (method is { IsStatic: false } && IsUserInstanceType(method.ContainingType))
            return $"{receiver}.{UserInstanceMethodName(method)}()";
        throw Unsupported("WRK108", source);
    }

    private IMethodSymbol? DisposeMethod(ITypeSymbol type, string interfaceName, string methodName) =>
        _compilation.GetTypeByMetadataName(interfaceName)?.GetMembers(methodName).SingleOrDefault() is { } member
            ? type.FindImplementationForInterfaceMember(member) as IMethodSymbol
            : null;
}
