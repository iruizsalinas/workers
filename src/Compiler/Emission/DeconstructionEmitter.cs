using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    private static IOperation DeconstructionTarget(IOperation target) =>
        target is IDeclarationExpressionOperation declaration ? DeconstructionTarget(declaration.Expression) : target;

    private static IEnumerable<ILocalSymbol> DeconstructionLocals(IOperation target)
    {
        if (target is ILocalReferenceOperation { IsDeclaration: true } local) yield return local.Local;
        foreach (var child in target.ChildOperations)
            foreach (var symbol in DeconstructionLocals(child)) yield return symbol;
    }

    private void EmitDeconstructionLocals(IOperation target, int depth)
    {
        var names = DeconstructionLocals(target).Select(local => UserIdentifier(local, local.Name)).ToArray();
        if (names.Length != 0) _output.Append(' ', depth * 2).Append("let ").Append(string.Join(", ", names)).AppendLine(";");
    }

    private string Deconstruction(IDeconstructionAssignmentOperation operation) =>
        DeconstructValue(operation.Target, operation.Value.Type!, Expression(operation.Value),
            _model.GetDeconstructionInfo((AssignmentExpressionSyntax)operation.Syntax), operation.Syntax);

    private string DeconstructValue(IOperation target, ITypeSymbol sourceType, string sourceValue,
        DeconstructionInfo info, SyntaxNode source)
    {
        var body = new System.Text.StringBuilder();
        var references = new List<string>();
        var parameters = new List<string>();
        var assignments = new List<string>();
        var prefix = $"deconstruction:{source.SyntaxTree.FilePath}:{source.SpanStart}";
        var ordinal = 0;
        string Temporary(string label) => _names.Get($"{prefix}:{ordinal++}", label);
        var input = Temporary("source");
        var result = Extract(DeconstructionTarget(target), ResolveType(sourceType), input, info);
        body.Append(string.Join(" ", assignments)).Append(" return ").Append(result).Append(';');
        parameters.Add(input);
        references.Add(CopyValue(sourceType, sourceValue));
        return $"(({string.Join(", ", parameters)}) => {{ {body} }})({string.Join(", ", references)})";

        string Extract(IOperation destination, ITypeSymbol valueType, string value, DeconstructionInfo binding)
        {
            destination = DeconstructionTarget(destination);
            if (destination is ITupleOperation tuple)
            {
                var elements = new List<(string Value, ITypeSymbol Type)>();
                if (binding.Method is { } boundMethod)
                {
                    var method = ResolveMethod(boundMethod.GetConstructedReducedFrom() ?? boundMethod);
                    var outputs = method.Parameters.Where(parameter => parameter.RefKind == RefKind.Out).ToArray();
                    var slots = Temporary("outputs");
                    body.Append("const ").Append(slots).Append(" = ").Append(DeconstructComponents(method, value, source)).Append("; ");
                    elements.AddRange(outputs.Select((parameter, index) => ($"{slots}[{index}]", ResolveType(parameter.Type))));
                }
                else if (valueType is INamedTypeSymbol { IsTupleType: true } inputTuple)
                    elements.AddRange(inputTuple.TupleElements.Select((field, index) => ($"{value}[{index}]", field.Type)));
                else throw Unsupported("WRK101", source);
                if (elements.Count != tuple.Elements.Length) throw Unsupported("WRK101", source);
                var results = tuple.Elements.Select((element, index) =>
                    Extract(element, elements[index].Type, elements[index].Value, binding.Nested[index])).ToArray();
                return TupleValue(results);
            }
            var reference = Temporary("target");
            if (destination is not IDiscardOperation)
            {
                parameters.Add(reference);
                references.Add(AssignmentReference(destination));
            }
            var converted = Temporary("converted");
            var targetType = ResolveType(destination.Type!);
            var conversion = _compilation.ClassifyConversion(valueType, targetType);
            if (conversion.IsUserDefined) throw Unsupported("WRK101", source);
            var expression = conversion.IsIdentity || conversion.IsReference ? value
                : ForeachElementConversion(valueType, targetType, value, source, conversion.IsImplicit);
            body.Append("const ").Append(converted).Append(" = ").Append(CopyValue(targetType, expression)).Append("; ");
            if (destination is not IDiscardOperation) assignments.Add($"{reference}.value = {converted};");
            return converted;
        }
    }

    private string DeconstructComponents(IMethodSymbol boundMethod, string value, SyntaxNode source)
    {
        var method = ResolveMethod(boundMethod.GetConstructedReducedFrom() ?? boundMethod);
        var outputs = method.Parameters.Where(parameter => parameter.RefKind == RefKind.Out).ToArray();
        if (method.ContainingType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.KeyValuePair<TKey, TValue>")
            return value;
        if (method.IsImplicitlyDeclared && method.ContainingType.IsRecord)
            return "((receiver) => [" + string.Join(", ", outputs.Select(parameter => CopyValue(parameter.Type, UserMemberAccess("receiver",
                RecordPositionalMember(method.ContainingType, parameter.Name))))) + $"])({NotNull(value)})";
        var args = outputs.Select((_, index) => $"({{ set value(value) {{ outputs[{index}] = value; }} }})").ToArray();
        var call = method.IsStatic
            ? $"{QueueUserMethod(method, source)}({string.Join(", ", new[] { "receiver" }.Concat(args))})"
            : $"{NotNull("receiver")}.{UserInstanceMethodName(method)}({string.Join(", ", args)})";
        return $"((receiver) => {{ const outputs = []; {call}; return outputs; }})({value})";
    }

    private string AssignmentReference(IOperation target)
    {
        if (target is IPropertyReferenceOperation userIndexer && IsUserIndexer(userIndexer.Property)) return UserIndexerReference(userIndexer);
        if (target is IPropertyReferenceOperation mapIndexer && UsesKeyDictionary(mapIndexer.Instance?.Type))
            return $"((map, key) => ({{ get value() {{ return {NotNull("map")}.get(key); }}, set value(value) {{ {NotNull("map")}.set(key, value); }} }}))({Expression(mapIndexer.Instance!)}, {ValueExpression(mapIndexer.Arguments.Single().Value)})";
        var assigned = _names.Get($"assignment-reference:{target.Syntax.SyntaxTree.FilePath}:{target.Syntax.SpanStart}:value", "assigned");
        if (target is IParameterReferenceOperation primary && IsClassPrimaryParameter(primary.Parameter))
            return $"((receiver) => ({{ get value() {{ return {PrimaryParameterStorage(primary.Parameter, "receiver")}; }}, set value(value) {{ {PrimaryParameterStorage(primary.Parameter, "receiver")} = value; }} }}))(this)";
        if (target is ILocalReferenceOperation or IParameterReferenceOperation)
            return $"({{ get value() {{ return {Expression(target)}; }}, set value({assigned}) {{ {Expression(target)} = {assigned}; }} }})";
        if (target is IPropertyReferenceOperation { Property.IsIndexer: true, Arguments.Length: 1 } indexer)
        {
            var sequence = IsSequenceType(indexer.Instance?.Type);
            var dictionary = IsDictionary(indexer.Instance?.Type);
            if (!sequence && !dictionary) throw Unsupported("WRK101", target.Syntax);
            _helpers.Require(sequence ? JavaScriptHelper.SequenceIndex : JavaScriptHelper.DictionaryIndex);
            var get = _helpers.Name(sequence ? "sequenceIndex" : "dictionaryIndex");
            var set = _helpers.Name(sequence ? "sequenceSet" : "dictionarySet");
            return $"((receiver, index) => ({{ get value() {{ return {get}(receiver, index); }}, set value(value) {{ {set}(receiver, index, value); }} }}))({Expression(indexer.Instance!)}, {Expression(indexer.Arguments[0].Value)})";
        }
        if (target is IArrayElementReferenceOperation array)
        {
            _helpers.Require(JavaScriptHelper.SequenceIndex);
            return $"((array, index) => ({{ get value() {{ return {_helpers.Name("sequenceIndex")}(array, index); }}, set value(value) {{ {_helpers.Name("sequenceSet")}(array, index, value); }} }}))({Expression(array.ArrayReference)}, {Expression(array.Indices.Single())})";
        }
        if (target is IMemberReferenceOperation member)
        {
            if (member.Member is IFieldSymbol { ContainingType.IsTupleType: true })
                return UserOutReference(target);
            if (member.Member.IsStatic)
                return $"({{ get value() {{ return {Expression(target)}; }}, set value({assigned}) {{ {Expression(target)} = {assigned}; }} }})";
            if (target.Syntax is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } && member.Member is IPropertySymbol property
                && HasPropertyDescriptor(property))
                return BasePropertyReference(property, target.Syntax);
            var storage = Member(target, NeedsNullCheck(member.Instance) ? NotNull("receiver") : "receiver");
            return $"((receiver) => ({{ get value() {{ return {storage}; }}, set value(value) {{ {storage} = value; }} }}))({Expression(member.Instance!)})";
        }
        throw Unsupported("WRK101", target.Syntax);
    }
}
