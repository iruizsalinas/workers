using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

internal sealed partial class JavaScriptEmitter
{
    // Roslyn's operation tree is the typed IR. Syntax remains available for diagnostics and
    // platform adapters, but never supplies a fallback for an unrecognized operation.
    private string Expression(ExpressionSyntax syntax)
    {
        // Parentheses and null-forgiving operators have no independent operation in C#.
        if (syntax is ParenthesizedExpressionSyntax parenthesized)
            return $"({Expression(parenthesized.Expression)})";
        if (syntax is CheckedExpressionSyntax checkedExpression)
            return Expression(checkedExpression.Expression);
        if (syntax is PostfixUnaryExpressionSyntax suppression && suppression.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            return Expression(suppression.Operand);
        var operation = _model.GetOperation(syntax) ?? throw Unsupported("WRK101", syntax);
        while (operation.Parent is IConversionOperation { IsImplicit: true } conversion
            && conversion.Syntax.Span == syntax.Span)
            operation = conversion;
        return Expression(operation);
    }

    private string Expression(IOperation operation)
    {
        try
        {
            return operation switch
            {
                IConversionOperation value => Conversion(value),
                ITranslatedQueryOperation value => Expression(value.Operation),
                ITypeOfOperation value => TypeOf(value),
                ICoalesceAssignmentOperation value => CoalesceAssignment(value),
                IParenthesizedOperation value => $"({Expression(value.Operand)})",
                ILiteralOperation value => LiteralConstant(value.ConstantValue.Value, value.Syntax),
                INameOfOperation value => LiteralConstant(value.ConstantValue.Value, value.Syntax),
                IDefaultValueOperation value => DefaultFieldValue(value.Type!, value.Syntax),
                ILocalReferenceOperation value => UserIdentifier(value.Local, value.Local.Name),
                IParameterReferenceOperation value when IsClassPrimaryParameter(value.Parameter) => PrimaryParameterStorage(value.Parameter),
                IParameterReferenceOperation value => UserIdentifier(value.Parameter, value.Parameter.Name)
                    + (value.Parameter.RefKind == RefKind.Out ? ".value" : ""),
                IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver } when _initializerReceivers.TryPeek(out var receiver) => receiver,
                IInstanceReferenceOperation => "this",
                IUnaryOperation value => Unary(value),
                IRangeOperation value => RangeValue(value),
                ITupleOperation value => TupleValue(value.Elements.Select(ValueExpression)),
                IDeconstructionAssignmentOperation value => Deconstruction(value),
                IIncrementOrDecrementOperation value => NumericMutation(value),
                IAwaitOperation value => $"await {Operand(value.Operation)}",
                IBinaryOperation value => Binary(value),
                ICoalesceOperation value => Coalesce(value),
                IConditionalOperation value => $"{Operand(value.Condition)} ? {Expression(value.WhenTrue)} : {Expression(value.WhenFalse!)}",
                IConditionalAccessOperation value => ConditionalAccess(value),
                IConditionalAccessInstanceOperation value => ConditionalReceiver(value),
                ISimpleAssignmentOperation value => SimpleAssignment(value),
                ICompoundAssignmentOperation value => CompoundAssignment(value),
                IWithOperation value => WithExpression(value),
                ISwitchExpressionOperation value => SwitchExpression(value),
                IArrayCreationOperation value => ArrayCreation(value),
                IThrowOperation value => $"(($workers$exception) => {{ throw $workers$exception; }})({ThrowValue(value.Exception!)})",
                IIsPatternOperation value => IsPattern(value),
                IIsTypeOperation value => IsType(value),
                IEventReferenceOperation value => EventAccess(value),
                IEventAssignmentOperation value => EventAssignment(value),
                IFieldReferenceOperation value => Member(value),
                IPropertyReferenceOperation value => Member(value),
                IInvocationOperation value => Invocation(value),
                IArrayElementReferenceOperation value => SequenceAccess(value.ArrayReference, value.Indices.Single()),
                IImplicitIndexerReferenceOperation value when value.Instance.Type is IArrayTypeSymbol
                    || value.Instance.Type?.SpecialType == SpecialType.System_String => SequenceAccess(value.Instance, value.Argument),
                IAnonymousObjectCreationOperation value => AnonymousObject(value),
                IInterpolatedStringOperation value => "`" + string.Concat(value.Parts.Select(InterpolatedPart)) + "`",
                ICollectionExpressionOperation value => Collection(value),
                IObjectCreationOperation value => ObjectCreation(value),
                ITypeParameterObjectCreationOperation value => GenericConstruction(value),
                IDelegateCreationOperation value => DelegateCreation(value),
                IMethodReferenceOperation value => MethodGroup((ExpressionSyntax)value.Syntax, value.Method,
                    value.Syntax is MemberAccessExpressionSyntax member ? member.Expression : null),
                IAnonymousFunctionOperation value => AnonymousFunction(value),
                _ => throw Unsupported("WRK101", operation.Syntax)
            };
        }
        catch (NotSupportedException exception)
        {
            throw WorkerDiagnostics.WithLocation(exception, operation.Syntax);
        }
    }

    private string Operand(IOperation operation)
    {
        var value = Expression(operation);
        while (operation is IConversionOperation conversion) operation = conversion.Operand;
        return operation is IBinaryOperation or IConditionalOperation or IAwaitOperation
            or ISimpleAssignmentOperation or ICompoundAssignmentOperation ? $"({value})" : value;
    }

    private string Coalesce(ICoalesceOperation operation)
    {
        var value = Expression(operation.Value);
        if (!operation.ValueConversion.IsIdentity && !operation.ValueConversion.IsReference
            && operation.Type?.SpecialType != SpecialType.System_Object)
        {
            if (operation.ValueConversion.IsUserDefined) throw Unsupported("WRK108", operation.Syntax);
            var parameter = _names.Get($"coalesce:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "value");
            var source = UnwrapNullable(operation.Value.Type!);
            var target = UnwrapNullable(operation.Type!);
            var widening = source.SpecialType is SpecialType.System_Int32 or SpecialType.System_UInt32
                && target.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64;
            var converted = widening ? parameter : ConvertValue(source, target, parameter, operation.Syntax);
            // Keep the null path lazy while converting a present value exactly once.
            value = $"(({parameter}) => {parameter} == null ? null : {converted})({value})";
        }
        return $"(({value}) ?? ({Expression(operation.WhenNull)}))";
    }

    private string AnonymousObject(IAnonymousObjectCreationOperation operation)
    {
        var printed = new List<(string Name, string Storage, ITypeSymbol Type)>();
        var properties = operation.Initializers.Select(initializer =>
        {
            if (initializer is not ISimpleAssignmentOperation { Target: IPropertyReferenceOperation property } assignment)
                throw Unsupported("WRK101", initializer.Syntax);
            var name = AnonymousPropertyName(property.Property);
            printed.Add((property.Property.Name, name, ResolveType(property.Property.Type)));
            return $"{JavaScriptObjectKey(name)}: {ValueExpression(assignment.Value)}";
        }).ToArray();
        RegisterAnonymousText((INamedTypeSymbol)ResolveType(operation.Type!), printed, operation.Syntax);
        return "{ " + string.Join(", ", properties) + " }";
    }

    private string AnonymousFunction(IAnonymousFunctionOperation operation) => operation.Syntax switch
    {
        ParenthesizedLambdaExpressionSyntax { ExpressionBody: not null } value =>
            $"{AsyncPrefix(value.AsyncKeyword)}({string.Join(", ", value.ParameterList.Parameters.Select(ParameterName))}) => {LambdaExpressionBody(value.ExpressionBody)}",
        ParenthesizedLambdaExpressionSyntax value => Lambda(value),
        SimpleLambdaExpressionSyntax { ExpressionBody: not null } value =>
            $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {LambdaExpressionBody(value.ExpressionBody)}",
        SimpleLambdaExpressionSyntax value => Lambda(value),
        _ => BoundQueryLambda(operation)
    };

    private string LambdaExpressionBody(ExpressionSyntax expression)
    {
        var temporaries = new List<string>();
        _expressionTemporaryScopes.Push(temporaries);
        string body;
        try { body = ValueExpression(expression); }
        finally { _expressionTemporaryScopes.Pop(); }
        if (temporaries.Count != 0)
            return $"{{ let {string.Join(", ", temporaries)}; return {body}; }}";
        // Object literals are parsed as statement blocks in arrow-function bodies.
        return body.StartsWith('{') ? $"({body})" : body;
    }

    private string SimpleAssignment(ISimpleAssignmentOperation operation)
    {
        var right = ValueExpression(operation.Value);
        if (operation.Target is IPropertyReferenceOperation userIndexer && IsUserIndexer(userIndexer.Property))
            return $"((reference, value) => reference.value = value)({UserIndexerReference(userIndexer)}, {right})";
        if (operation.Target is IFieldReferenceOperation { Field.ContainingType.IsTupleType: true })
            return $"((reference, value) => reference.value = value)({AssignmentReference(operation.Target)}, {right})";
        if (operation.Target is IPropertyReferenceOperation baseProperty
            && baseProperty.Syntax is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }
            && HasPropertyDescriptor(baseProperty.Property))
            return BasePropertySet(baseProperty.Property, operation.Syntax, right);
        if (operation.Target is IDiscardOperation) return $"({right})";
        if (operation.Target is IMemberReferenceOperation member && NeedsNullCheck(member.Instance)
            && operation.Target is not IPropertyReferenceOperation { Property.IsIndexer: true })
            // Form the storage reference first; check its receiver only after evaluating the RHS.
            return $"((reference, value) => reference.value = value)({AssignmentReference(operation.Target)}, {right})";
        if (operation.Target is IArrayElementReferenceOperation array)
        {
            _helpers.Require(JavaScriptHelper.SequenceIndex);
            return $"{_helpers.Name("sequenceSet")}({Expression(array.ArrayReference)}, {Expression(array.Indices.Single())}, {right})";
        }
        if (operation.Target is IPropertyReferenceOperation { Property.IsIndexer: true } property)
        {
            if (UsesKeyDictionary(property.Instance?.Type))
                return $"((map, key, value) => {{ {NotNull("map")}.set(key, value); return value; }})({Expression(property.Instance!)}, {ValueExpression(property.Arguments.Single().Value)}, {right})";
            if (property.Property.ContainingType.ToDisplayString() == "Workers.Headers")
            {
                var receiver = Expression(property.Instance!);
                var name = Expression(property.Arguments.Single().Value);
                var key = $"header-assignment:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}";
                var headers = _names.Get(key + ":headers", "headers");
                var header = _names.Get(key + ":name", "name");
                var value = _names.Get(key + ":value", "value");
                return $"(({headers}, {header}, {value}) => {{ {NotNull(headers)}.set({header}, {value}); return {value}; }})({receiver}, {name}, {right})";
            }
            if (IsSequenceType(property.Instance?.Type))
            {
                _helpers.Require(JavaScriptHelper.SequenceIndex);
                return $"{_helpers.Name("sequenceSet")}({Expression(property.Instance!)}, {Expression(property.Arguments.Single().Value)}, {right})";
            }
            if (IsDictionary(property.Instance?.Type))
            {
                _helpers.Require(JavaScriptHelper.DictionaryIndex);
                return $"{_helpers.Name("dictionarySet")}({Expression(property.Instance!)}, {Expression(property.Arguments.Single().Value)}, {right})";
            }
        }
        return $"{Expression(operation.Target)} = {right}";
    }

    private string Unary(IUnaryOperation operation)
    {
        if (IsIndexType(operation.Type)) return IndexValue(Expression(operation.Operand), true);
        if (operation.OperatorMethod is { } sourceOperator && sourceOperator.DeclaringSyntaxReferences.Length != 0)
            return SourceOperator(sourceOperator, operation.ConstrainedToType, operation.Syntax, operation.Operand);
        if (operation.OperatorMethod is not null) throw UnsupportedSymbol(operation.OperatorMethod, operation.Syntax);
        if (operation.Type?.SpecialType is SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Decimal)
            throw Unsupported("WRK108", operation.Syntax);
        var operand = Operand(operation.Operand);
        if (operation.OperatorKind == UnaryOperatorKind.Not && operation.Type?.SpecialType == SpecialType.System_Boolean)
            return $"!({operand})";
        if (operation.IsChecked || operation.IsLifted) throw Unsupported("WRK108", operation.Syntax);
        if (operation.ConstantValue is { HasValue: true, Value: int or uint or double } constant)
            return LiteralConstant(constant.Value, operation.Syntax);
        var token = operation.OperatorKind switch
        {
            UnaryOperatorKind.Minus => "-",
            UnaryOperatorKind.Plus => "+",
            UnaryOperatorKind.BitwiseNegation => "~",
            _ => throw Unsupported("WRK108", operation.Syntax)
        };
        return NumericResult($"{token}{operand}", operation.Type?.SpecialType ?? SpecialType.None, operation.Syntax);
    }

    private string NumericMutation(IIncrementOrDecrementOperation operation)
    {
        if (operation.OperatorMethod is { DeclaringSyntaxReferences.Length: > 0 } method && !operation.IsLifted && IsSimpleMutationTarget(operation.Target))
        {
            var key = $"operator-mutation:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}";
            var old = _names.Get(key + ":old", "old");
            var updatedName = _names.Get(key + ":updated", "updated");
            var next = SourceOperatorCall(method, operation.ConstrainedToType, operation.Syntax, old);
            return Mutate(operation.Target, target => $"(({old}) => {{ const {updatedName} = {next}; {target} = {updatedName}; return {(operation.IsPostfix ? old : updatedName)}; }})({target})");
        }
        if (operation.OperatorMethod is not null || operation.IsChecked || operation.IsLifted
            || !IsSimpleMutationTarget(operation.Target))
            throw Unsupported("WRK108", operation.Syntax);
        var token = operation.Kind == OperationKind.Increment ? "+" : "-";
        var type = operation.Type?.SpecialType ?? SpecialType.None;
        var updated = NumericResult($"$workers$value {token} 1", type, operation.Syntax);
        return Mutate(operation.Target, target => operation.IsPostfix
            ? $"(($workers$value) => {{ {target} = {updated}; return $workers$value; }})({target})"
            : $"({target} = {NumericResult($"{target} {token} 1", type, operation.Syntax)})");
    }

    private string CompoundAssignment(ICompoundAssignmentOperation operation)
    {
        if (operation.OperatorMethod is null && operation.OperatorKind is BinaryOperatorKind.And or BinaryOperatorKind.Or or BinaryOperatorKind.ExclusiveOr
            or BinaryOperatorKind.LeftShift or BinaryOperatorKind.RightShift or BinaryOperatorKind.UnsignedRightShift)
            return BitwiseAssignment(operation);
        if (operation.OperatorMethod is { DeclaringSyntaxReferences.Length: > 0 } method
            && operation.InConversion.IsIdentity && operation.OutConversion.IsIdentity && !operation.IsLifted && IsSimpleMutationTarget(operation.Target))
            return Mutate(operation.Target, target => $"({target} = {SourceOperatorCall(method, operation.ConstrainedToType, operation.Syntax, CopyValue(operation.Target.Type, target), ValueExpression(operation.Value))})", ContainsAwait(operation.Value));
        if (operation.Type?.TypeKind == TypeKind.Delegate && IsSimpleMutationTarget(operation.Target))
            return Mutate(operation.Target, target => CompoundAssignment(operation, target), ContainsAwait(operation.Value));
        if (!IsSimpleMutationTarget(operation.Target)
            || operation.OperatorMethod is not null || !operation.InConversion.IsIdentity
            || !operation.OutConversion.IsIdentity || operation.IsChecked || operation.IsLifted)
            throw Unsupported("WRK108", operation.Syntax);
        return Mutate(operation.Target, target => CompoundAssignment(operation, target), ContainsAwait(operation.Value));
    }

    private string CompoundAssignment(ICompoundAssignmentOperation operation, string target)
    {
        var type = operation.Type is INamedTypeSymbol { EnumUnderlyingType: { } underlying }
            ? underlying.SpecialType : operation.Type?.SpecialType ?? SpecialType.None;
        var right = Operand(operation.Value);
        if (operation.Type?.TypeKind == TypeKind.Delegate && operation.OperatorKind is BinaryOperatorKind.Add or BinaryOperatorKind.Subtract)
            return $"({target} = {DelegateHelper(operation.OperatorKind == BinaryOperatorKind.Add ? "delegateCombine" : "delegateRemove")}({target}, {right}))";
        if (type == SpecialType.System_String && operation.OperatorKind == BinaryOperatorKind.Add)
            return $"({target} = ({target} ?? \"\") + {StringConcatOperand(operation.Value, right, operation.Syntax)})";
        var integral = type is SpecialType.System_Int32 or SpecialType.System_UInt32;
        var unsigned = type == SpecialType.System_UInt32 ? "true" : "false";
        var result = operation.OperatorKind switch
        {
            BinaryOperatorKind.Multiply when type == SpecialType.System_Int32 => $"Math.imul({target}, {right})",
            BinaryOperatorKind.Multiply when type == SpecialType.System_UInt32 => $"(Math.imul({target}, {right}) >>> 0)",
            BinaryOperatorKind.Divide when integral =>
                $"{_helpers.Require(JavaScriptHelper.IntegerDivide)}({target}, {right}, {unsigned})",
            BinaryOperatorKind.Remainder when integral =>
                $"{_helpers.Require(JavaScriptHelper.IntegerRemainder)}({target}, {right}, {unsigned})",
            BinaryOperatorKind.Add or BinaryOperatorKind.Subtract or BinaryOperatorKind.Multiply or BinaryOperatorKind.Divide or BinaryOperatorKind.Remainder =>
                NumericResult($"{target} {BinaryOperator(operation.OperatorKind)} {right}", type, operation.Syntax),
            _ => throw Unsupported("WRK108", operation.Syntax)
        };
        return $"({target} = {result})";
    }

    // Capture a member receiver before a getter or RHS can change the original variable.
    private string Mutate(IOperation operation, Func<string, string> emit, bool isAsync = false)
    {
        if (operation is IArrayElementReferenceOperation || operation is IPropertyReferenceOperation { Property.IsIndexer: true })
        {
            var reference = _names.Get($"indexer-mutation:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "reference");
            var mutation = emit(reference + ".value");
            return Capture(reference, AssignmentReference(operation), mutation);
        }
        if (operation is IFieldReferenceOperation { Field.ContainingType.IsTupleType: true })
        {
            var reference = _names.Get($"tuple-mutation:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "reference");
            var mutation = emit(reference + ".value");
            return Capture(reference, AssignmentReference(operation), mutation);
        }
        if (operation is IPropertyReferenceOperation propertyReference
            && operation.Syntax is MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax }
            && HasPropertyDescriptor(propertyReference.Property))
        {
            var reference = _names.Get($"base-mutation:{operation.Syntax.SyntaxTree.FilePath}:{operation.Syntax.SpanStart}", "reference");
            var mutation = emit(reference + ".value");
            var argument = BasePropertyReference(propertyReference.Property, operation.Syntax);
            return Capture(reference, argument, mutation);
        }
        var instance = operation switch
        {
            IFieldReferenceOperation field => field.Instance,
            IPropertyReferenceOperation property => property.Instance,
            _ => null
        };
        if (instance is null || instance is IInstanceReferenceOperation) return emit(Expression(operation));
        var source = operation.Syntax;
        var receiver = _names.Get($"mutation:{source.SyntaxTree.FilePath}:{source.SpanStart}", "receiver");
        var body = emit(Member(operation, NeedsNullCheck(instance) ? NotNull(receiver) : receiver));
        return Capture(receiver, Expression(instance), body);

        string Capture(string parameter, string argument, string mutation)
        {
            if (!isAsync) return $"(({parameter}) => {mutation})({argument})";
            if (!_expressionTemporaryScopes.TryPeek(out var temporaries)) throw Unsupported("WRK108", operation.Syntax);
            temporaries.Add(parameter);
            return $"(({parameter} = {argument}), {mutation})";
        }
    }

    private static bool ContainsAwait(IOperation operation) => operation is IAwaitOperation
        || operation is not IAnonymousFunctionOperation && operation.ChildOperations.Any(ContainsAwait);

    // Clone before evaluating initializers, then apply each assignment in source order.
    private string WithExpression(IWithOperation operation)
    {
        var value = operation.Syntax;
        if (operation.Operand.Type is not INamedTypeSymbol { IsRecord: true } type || !IsUserInstanceType(type))
            throw Unsupported("WRK101", value);
        var temporary = _names.Get($"with:{value.SyntaxTree.FilePath}:{value.SpanStart}", "copy");
        var assignments = operation.Initializer.Initializers.Select(expression => expression switch
        {
            ISimpleAssignmentOperation { Target: IMemberReferenceOperation member } assignment =>
                $"{UserMemberAccess(temporary, member.Member)} = {ValueExpression(assignment.Value)};",
            _ => throw Unsupported("WRK106", expression.Syntax)
        });
        string clone;
        if (UsesUserHierarchy(type))
            clone = $"((source) => {{ if (source == null) throw {NullReferenceException()}; return source.{RecordCloneSlot}(); }})({Expression(operation.Operand)})";
        else if (type.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared && IsUserRecordCopyConstructor(constructor)))
        {
            var record = QueueUserType(type, value);
            clone = $"{record}.$copy({Expression(operation.Operand)})";
        }
        else
        {
            var copy = "Object.assign(Object.create(Object.getPrototypeOf(source)), source)";
            if (!RequiresUserClass(type)) copy = $"Object.defineProperty({copy}, {PlainRecordIdentitySlot}, {{ value: source[{PlainRecordIdentitySlot}] }})";
            clone = $"((source) => {copy})({NotNull(Expression(operation.Operand))})";
        }
        var isAsync = ContainsAwait(operation.Initializer);
        return isAsync ? AsyncInitializerBody(value, [temporary], [clone], string.Join(" ", assignments), temporary)
            : $"(({temporary}) => {{ {string.Join(" ", assignments)} return {temporary}; }})({clone})";
    }

    private static bool IsSimpleMutationTarget(IOperation target) => target switch
    {
        IArrayElementReferenceOperation => true,
        IPropertyReferenceOperation { Property.IsIndexer: true } property when IsSequenceType(property.Instance?.Type) => true,
        IPropertyReferenceOperation { Property.IsIndexer: true } property when IsDictionary(property.Instance?.Type) => true,
        ILocalReferenceOperation or IParameterReferenceOperation => true,
        IFieldReferenceOperation { Field.ContainingType.IsTupleType: true } => true,
        IFieldReferenceOperation { Field.HasConstantValue: false } field => field.Field.DeclaringSyntaxReferences.Length != 0,
        IPropertyReferenceOperation property =>
            property.Property.DeclaringSyntaxReferences.Length != 0
            && PropertyAccessor(property.Property, getter: false) is not null,
        _ => false
    };

    private static string NumericResult(string expression, SpecialType type, SyntaxNode source) => type switch
    {
        SpecialType.System_Int32 => $"(({expression}) | 0)",
        SpecialType.System_UInt32 => $"(({expression}) >>> 0)",
        SpecialType.System_Single => $"Math.fround({expression})",
        SpecialType.System_Double => $"({expression})",
        _ => throw Unsupported("WRK108", source)
    };

    private string Lambda(ParenthesizedLambdaExpressionSyntax value)
    {
        var parameters = string.Join(", ", value.ParameterList.Parameters.Select(ParameterName));
        return $"{AsyncPrefix(value.AsyncKeyword)}({parameters}) => {LambdaBlock(value.Block!)}";
    }

    private string Lambda(SimpleLambdaExpressionSyntax value)
    {
        return $"{AsyncPrefix(value.AsyncKeyword)}{ParameterName(value.Parameter)} => {LambdaBlock(value.Block!)}";
    }

    private static string AsyncPrefix(SyntaxToken token) =>
        token.IsKind(SyntaxKind.AsyncKeyword) ? "async " : "";

    private string LambdaBlock(BlockSyntax block)
    {
        var start = _output.Length;
        EmitWithExpressionScope(1, () => EmitStatements(block.Statements, 1));
        var statements = _output.ToString(start, _output.Length - start).TrimEnd();
        _output.Length = start;
        return statements.Length == 0 ? "{}" : $"{{\n{statements}\n}}";
    }

    // Single-dimensional arrays: byte arrays are Uint8Array, other arrays are JavaScript arrays whose
    // elements start at the element type's default value.
    private string ArrayCreation(IArrayCreationOperation operation)
    {
        if (ResolveType(operation.Type!) is not IArrayTypeSymbol { Rank: 1 } array)
            throw Unsupported("WRK101", operation.Syntax);
        var bytes = array.ElementType.SpecialType == SpecialType.System_Byte;
        if (operation.Initializer is { } initializer)
        {
            var items = "[" + string.Join(", ", initializer.ElementValues.Select(ValueExpression)) + "]";
            return bytes ? $"Uint8Array.from({items})" : items;
        }
        var length = Expression(operation.DimensionSizes.Single());
        if (bytes) return $"new Uint8Array({length})";
        var fallback = DefaultValueText(array.ElementType, operation.Syntax);
        return $"((length) => {{ if (!Number.isInteger(length) || length < 0) throw new RangeError(\"Arithmetic operation resulted in an overflow.\"); " +
               $"return Array.from({{ length }}, () => {fallback}); }})({length})";
    }

    private string Collection(ICollectionExpressionOperation operation)
    {
        var elementType = operation.Type is IArrayTypeSymbol array ? array.ElementType
            : (operation.Type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();
        var items = "[" + string.Join(", ", operation.Elements.Select(element => element is ISpreadOperation spread
            ? "..." + Spread(spread, elementType) : ValueExpression(element))) + "]";
        if (operation.Type is INamedTypeSymbol set
            && set.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.HashSet<T>")
        {
            if (IsCompositeKey(set.TypeArguments[0])) return KeyCollection(set, operation.Syntax, items);
            if (!SupportsLinqEquality(set.TypeArguments[0])) throw UnsupportedSymbol(set, operation.Syntax);
            return CreateSet(items);
        }
        return operation.Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte }
            ? $"Uint8Array.from({items})"
            : IsListType(operation.Type) ? CreateList(items) : items;
    }

    private string Spread(ISpreadOperation operation, ITypeSymbol? elementType)
    {
        var values = Expression(operation.Operand);
        if (operation.Operand.Type is not IArrayTypeSymbol && !IsArrayBackedList(operation.Operand.Type))
            values = HelperInvocation(JavaScriptHelper.LinqValues, [values]);
        if (elementType?.SpecialType == SpecialType.System_Object && operation.ElementType is { } sourceElement)
        {
            var boxed = ObjectConversion(sourceElement, "value", operation.Syntax);
            return boxed == "value" ? values : $"Array.from({values}, value => {boxed})";
        }
        if (operation.ElementConversion.IsIdentity || operation.ElementConversion.IsReference)
            return elementType is null ? values : CopySequenceValues(elementType, values);
        if (elementType is null || operation.ElementType is null || operation.ElementConversion.IsUserDefined)
            throw Unsupported("WRK108", operation.Syntax);
        var converted = ConvertValue(operation.ElementType, elementType, "value", operation.Syntax);
        return $"Array.from({values}, value => {converted})";
    }

}
