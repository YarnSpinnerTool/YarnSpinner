namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// A visitor that walks the parse tree, checking for type consistency
    /// in expressions. Existing type information is provided via the <see
    /// cref="existingDeclarations"/> property. This visitor will also
    /// attempt to infer the type of variables that don't have an explicit
    /// declaration; for each of these, a new Declaration will be created
    /// and made available via the <see cref="NewDeclarations"/> property.
    /// </summary>
    internal class TypeCheckVisitor : YarnSpinnerParserBaseVisitor<Yarn.IType>
    {
        // The collection of variable declarations we know about before
        // starting our work
        private readonly IEnumerable<Declaration> existingDeclarations;

        // The name of the node that we're currently visiting.
        private string currentNodeName = null;

        /// <summary>
        /// The context of the node we're currently in.
        /// </summary>
        private YarnSpinnerParser.NodeContext currentNodeContext;

        /// <summary>
        /// The name of the file we're currently in.
        /// </summary>
        private string sourceFileName;

        /// <summary>
        /// Gets the collection of new variable declarations that were
        /// found as a result of using this <see
        /// cref="TypeCheckVisitor"/> to visit a <see
        /// cref="ParserRuleContext"/>.
        /// </summary>
        public ICollection<Declaration> NewDeclarations { get; private set; }

        private readonly IEnumerable<IType> types;

        /// <summary>
        /// Gets the collection of all declarations - both the ones we received
        /// at the start, and the new ones we've derived ourselves.
        /// </summary>
        public IEnumerable<Declaration> Declarations => this.existingDeclarations.Concat(this.NewDeclarations);

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="TypeCheckVisitor"/> class.
        /// </summary>
        /// <param name="sourceFileName">The name of the source file that
        /// the visitor will operate on.</param>
        /// <param name="existingDeclarations">A collection of <see
        /// cref="Declaration"/> objects that contain type
        /// information.</param>
        /// <param name="types">A collection of type definitions.</param>
        public TypeCheckVisitor(string sourceFileName, IEnumerable<Declaration> existingDeclarations, IEnumerable<IType> types)
        {
            this.existingDeclarations = existingDeclarations;
            this.NewDeclarations = new List<Declaration>();
            this.types = types;
            this.sourceFileName = sourceFileName;
        }

        protected override Yarn.IType DefaultResult => null;

        public override Yarn.IType VisitNode(YarnSpinnerParser.NodeContext context) {
            currentNodeContext = context;
            foreach (var header in context.header()) {
                if (header.header_key.Text == "title") {
                    currentNodeName = header.header_value.Text;
                }
            }
            Visit(context.body());

            return null;
        }

        public override Yarn.IType VisitValueNull([NotNull] YarnSpinnerParser.ValueNullContext context)
        {
            throw new TypeException(context, "Null is not a permitted type in Yarn Spinner 2.0 and later", sourceFileName);
        }

        public override Yarn.IType VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            return BuiltinTypes.String;
        }

        public override Yarn.IType VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return BuiltinTypes.Boolean;
        }

        public override Yarn.IType VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return BuiltinTypes.Boolean;
        }

        public override Yarn.IType VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            return BuiltinTypes.Number;
        }

        public override Yarn.IType VisitValueVar(YarnSpinnerParser.ValueVarContext context)
        {

            return VisitVariable(context.variable());
        }

        public override Yarn.IType VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            // The type of the value depends on the declared type of the
            // variable

            var name = context.VAR_ID().GetText();

            foreach (var declaration in Declarations)
            {
                if (declaration.Name == name)
                {
                    return declaration.Type;
                }
            }

            // We don't have a declaration for this variable. Return
            // Undefined. Hopefully, other context will allow us to infer a
            // type.
            return null;

        }

        public override Yarn.IType VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            string functionName = context.function().FUNC_ID().GetText();

            Declaration functionDeclaration = Declarations
                .Where(d => d.Type is FunctionType)
                .FirstOrDefault(d => d.Name == functionName);

            FunctionType functionType;

            if (functionDeclaration == null) {
                // We don't have a declaration for this function. Create an
                // implicit one.

                functionType = new FunctionType();
                functionType.ReturnType = BuiltinTypes.Undefined;
                
                functionDeclaration = new Declaration {
                    Name = functionName,
                    Type = functionType,
                    IsImplicit = true,
                    Description = $"Implicit declaration of function at {sourceFileName}:{context.Start.Line}:{context.Start.Column}",
                    SourceFileName = sourceFileName,
                    SourceFileLine = context.Start.Line,
                    SourceNodeName = currentNodeName,
                    SourceNodeLine = context.Start.Line - (this.currentNodeContext.BODY_START().Symbol.Line + 1),
                };

                // Create the array of parameters for this function based
                // on how many we've seen in this call. Set them all to be
                // undefined; we'll bind their type shortly.
                var parameterTypes = context.function().expression()
                    .Select(e => BuiltinTypes.Undefined)
                    .ToList();

                foreach (var parameterType in parameterTypes) {
                    functionType.AddParameter(parameterType);
                }

                NewDeclarations.Add(functionDeclaration);
            } else {
                functionType = functionDeclaration.Type as FunctionType;
                if (functionType == null) {
                    throw new InvalidOperationException($"Internal error: decl's type is not a {nameof(FunctionType)}");
                }
            }
            
            // Check each parameter of the function
            var suppliedParameters = context.function().expression();

            var expectedParameters = functionType.Parameters;

            if (suppliedParameters.Length != expectedParameters.Count())
            {
                // Wrong number of parameters supplied
                var parameters = expectedParameters.Count() == 1 ? "parameter" : "parameters";
                throw new TypeException(context, $"Function {functionName} expects {expectedParameters.Count()} {parameters}, but received {suppliedParameters.Length}", sourceFileName);
            }

            for (int i = 0; i < expectedParameters.Count(); i++)
            {
                var suppliedParameter = suppliedParameters[i];

                var expectedType = expectedParameters[i];

                var suppliedType = this.Visit(suppliedParameter);

                if (expectedType == BuiltinTypes.Undefined)
                {
                    // The type of this parameter hasn't yet been bound.
                    // Bind this parameter type to what we've resolved the
                    // type to.
                    expectedParameters[i] = suppliedType;
                    expectedType = suppliedType;
                }

                if (TypeUtil.IsSubType(expectedType, suppliedType) == false)
                {
                    throw new TypeException(context, $"{functionName} parameter {i + 1} expects a {expectedType.Name}, not a {suppliedType.Name}", sourceFileName);
                }
            }

            // Cool, all the parameters check out!

            // Finally, return the return type of this function.
            return functionType.ReturnType;
        }

        public override Yarn.IType VisitExpValue(YarnSpinnerParser.ExpValueContext context)
        {
            // Value expressions have the type of their inner value
            Yarn.IType type = Visit(context.value());
            context.Type = type;
            return type;
        }

        public override Yarn.IType VisitExpParens(YarnSpinnerParser.ExpParensContext context)
        {
            // Parens expressions have the type of their inner expression
            Yarn.IType type = Visit(context.expression());
            context.Type = type;
            return type;
        }

        public override Yarn.IType VisitExpAndOrXor(YarnSpinnerParser.ExpAndOrXorContext context)
        {
            Yarn.IType type = CheckOperation(context, context.expression(), CodeGenerationVisitor.TokensToOperators[context.op.Type], context.op.Text);
            context.Type = type;
            return type;
        }

        public override Yarn.IType VisitSet_statement([NotNull] YarnSpinnerParser.Set_statementContext context)
        {
            var expressionType = Visit(context.expression());
            var variableType = Visit(context.variable());

            var variableName = context.variable().GetText();

            ParserRuleContext[] terms = { context.variable(), context.expression() };

            Yarn.IType type;

            Operator @operator;

            switch (context.op.Type)
            {
                case YarnSpinnerLexer.OPERATOR_ASSIGNMENT:
                    // Straight assignment supports any assignment, as long as it's consistent
                    try {
                        type = CheckOperation(context, terms, Operator.None, context.op.Text, expressionType);
                    } catch (TypeException e) {
                        // Rewrite this TypeException for clarity 
                        throw new TypeException(context, $"{variableName} ({variableType?.Name ?? "undefined"}) cannot be assigned a {expressionType?.Name ?? "undefined"}", sourceFileName);
                    }
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_ADDITION_EQUALS:
                    // += supports strings and numbers
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_ADDITION];
                    type = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION_EQUALS:
                    // -=, *=, /=, %= supports only numbers
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION];
                    type = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION_EQUALS:
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION];
                    type = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_DIVISION_EQUALS:
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_DIVISION];
                    type = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_MODULUS_EQUALS:
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_MODULUS];
                    type = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                default:
                    throw new InvalidOperationException($"Internal error: {nameof(VisitSet_statement)} got unexpected operand {context.op.Text}");
            }

            if (expressionType == BuiltinTypes.Undefined) {
                // We don't know what this is set to, so we'll have to
                // assume it's ok. Return the variable type, if known.
                return variableType;
            }

            return expressionType;
        }

        private Yarn.IType CheckOperation(ParserRuleContext context, ParserRuleContext[] terms, Operator operationType, string operationDescription, params Yarn.IType[] permittedTypes)
        {

            var termTypes = new List<Yarn.IType>();

            var expressionType = BuiltinTypes.Undefined;

            foreach (var expression in terms)
            {
                // Visit this expression, and determine its type.
                Yarn.IType type = Visit(expression);

                if (type != BuiltinTypes.Undefined)
                {
                    termTypes.Add(type);
                    if (expressionType == BuiltinTypes.Undefined) {
                        // This is the first concrete type we've seen. This
                        // will be our expression type.
                        expressionType = type;
                    }
                }
            }

            if (permittedTypes.Length == 1 && expressionType == BuiltinTypes.Undefined) {
                // If we aren't sure of the expression type from
                // parameters, but we only have one permitted one, then
                // assume that the expression type is the single permitted
                // type.
                expressionType = permittedTypes.First();
            }

            if (expressionType == BuiltinTypes.Undefined)
            {
                // We still don't know what type of expression this is, and
                // don't have a reasonable guess.

                // Last-ditch effort: is the operator that we were given
                // valid in exactly one type? In that case, we'll decide
                // it's that type.
                var typesImplementingMethod = types
                    .Where(t => t.Methods != null)
                    .Where(t => t.Methods.ContainsKey(operationType.ToString()));

                if (typesImplementingMethod.Count() == 1)
                {
                    // Only one type implements the operation we were
                    // given. Given no other information, we will assume
                    // that it is this type.
                    expressionType = typesImplementingMethod.First();
                }
                else if (typesImplementingMethod.Count() > 1)
                {
                    // Multiple types implement this operation.
                    IEnumerable<string> typeNames = typesImplementingMethod.Select(t => t.Name);

                    throw new TypeException(context, $"Type of expression \"{context.GetTextWithWhitespace()}\" can't be determined without more context (the compiler thinks it could be {string.Join(", or ", typeNames)}). Use a type cast on at least one of the terms (e.g. the string(), number(), bool() functions)", this.sourceFileName);
                }
                else
                {
                    // No types implement this operation (??)
                    throw new TypeException(context, $"Type of expression \"{context.GetTextWithWhitespace()}\" can't be determined without more context. Use a type cast on at least one of the terms (e.g. the string(), number(), bool() functions)", sourceFileName);
                }
            }

            // Were any of the terms variables for which we don't currently
            // have a declaration for?

            // Start by building a list of all terms that are variables.
            var variableNames = terms
                .OfType<YarnSpinnerParser.ExpressionContext>()
                .Select(c => c.GetChild<YarnSpinnerParser.ValueVarContext>(0))
                .Where(c => c != null)
                .Select(v => v.variable().VAR_ID().GetText())
                .Distinct();
            
            // Build the list of variable names that we don't have a
            // declaration for. We'll check for explicit declarations first.
            var undefinedVariableNames = variableNames
                .Where(name => Declarations.Any(d => d.Name == name) == false);

            if (undefinedVariableNames.Count() > 0)
            {
                // We have references to variables that we don't have a an
                // explicit declaration for! Time to create implicit
                // references for them!

                // Get the position of this reference in the file
                int positionInFile = context.Start.Line;
            
                // The start line of the body is the line after the delimiter
                int nodePositionInFile = this.currentNodeContext.BODY_START().Symbol.Line + 1;

                foreach (var undefinedVariableName in undefinedVariableNames) {
                    // Generate a declaration for this variable here.
                    var decl = new Declaration {
                        Name = undefinedVariableName,
                        Description = $"{System.IO.Path.GetFileName(sourceFileName)}, node {currentNodeName}, line {positionInFile - nodePositionInFile}",
                        Type = expressionType,
                        DefaultValue = DefaultValueForType(expressionType),
                        SourceFileName = sourceFileName,
                        SourceFileLine = positionInFile,
                        SourceNodeName = currentNodeName,
                        SourceNodeLine = positionInFile - nodePositionInFile,
                        IsImplicit = true,
                    };
                    NewDeclarations.Add(decl);
                }
            }

            // All types must be same as the expression type (which is the
            // first defined type we encountered when going through the
            // terms)
            if (termTypes.All(t => t == expressionType) == false)
            {
                // Not all the term types we found were the expression
                // type.
                var typeList = string.Join(", ", termTypes.Select(t => t.Name));
                throw new TypeException(context, $"All terms of {operationDescription} must be the same, not {typeList}", sourceFileName);
            }

            // We've now determined that this expression is of
            // expressionType. In case any of the terms had an undefined
            // type, we'll define it now.
            foreach (var term in terms)
            {
                if (term is YarnSpinnerParser.ExpressionContext expression)
                {
                    if (expression.Type == BuiltinTypes.Undefined)
                    {
                        expression.Type = expressionType;
                    }

                    if (expression.Type is FunctionType functionType && functionType.ReturnType == BuiltinTypes.Undefined)
                    {
                        functionType.ReturnType = expressionType;
                    }
                }
            }

            if (operationType != Operator.None)
            {
                // We need to validate that the type we've selected actually
                // implements this operation.
                var implementingType = TypeUtil.FindImplementingTypeForMethod(expressionType, operationType.ToString());

                if (implementingType == null) {
                    throw new TypeException(context, $"{expressionType.Name} has no implementation defined for {operationDescription}", sourceFileName);
                }
            }

            // Is this expression is required to be one of the specified types?
            if (permittedTypes.Count() > 0)
            {
                // Does the type that we've arrived at match one of those types?
                if (permittedTypes.Contains(expressionType)) {
                    return expressionType;
                }
                else
                {
                    // The expression type wasn't valid!
                    var permittedTypesList = string.Join(" or ", permittedTypes.Select(t => t?.Name ?? "undefined"));
                    var typeList = string.Join(", ", termTypes.Select(t => t.Name));

                    throw new TypeException(context, $"Terms of '{operationDescription}' must be {permittedTypesList}, not {typeList}", sourceFileName);
                }
            } else {
                // We weren't given a specific type. The expression type is
                // therefore only valid if it can use the provided
                // operator.

                // Find a type in 'expressionType's hierarchy that
                // implements this method.
                var implementingTypeForMethod = TypeUtil.FindImplementingTypeForMethod(expressionType, operationType.ToString());

                if (implementingTypeForMethod == null) {
                    // The type doesn't have a method for handling this
                    // operator, and neither do any of its supertypes. This
                    // expression is therefore invalid.
                    throw new TypeException(context, $"Operator {operationDescription} cannot be used with {expressionType.Name} values", sourceFileName);
                } else {
                    return expressionType;
                }
            }
        }

        private static IConvertible DefaultValueForType(Yarn.IType expressionType)
        {
            if (expressionType == BuiltinTypes.String) {
                return default(string);
            } else if (expressionType == BuiltinTypes.Number) {
                return default(float);
            } else if (expressionType == BuiltinTypes.Boolean) {
                return default(bool);
            } else {
                throw new ArgumentOutOfRangeException($"No default value for type {expressionType.Name} exists.");
            }
        }

        public override Yarn.IType VisitIf_clause(YarnSpinnerParser.If_clauseContext context)
        {
            VisitChildren(context);
            // If clauses are required to be boolean
            var expressions = new[] { context.expression() };
            return CheckOperation(context, expressions, Operator.None, "if statement", BuiltinTypes.Boolean);
        }

        public override Yarn.IType VisitElse_if_clause(YarnSpinnerParser.Else_if_clauseContext context)
        {
            VisitChildren(context);
            // Else if clauses are required to be boolean
            var expressions = new[] { context.expression() };
            return CheckOperation(context, expressions, Operator.None, "elseif statement", BuiltinTypes.Boolean);
        }

        public override Yarn.IType VisitExpAddSub(YarnSpinnerParser.ExpAddSubContext context)
        {

            var expressions = context.expression();

            Yarn.IType type;

            var @operator = CodeGenerationVisitor.TokensToOperators[context.op.Type];

            type = CheckOperation(context, expressions, @operator, context.op.Text);

            context.Type = type;

            return type;
        }

        public override Yarn.IType VisitExpMultDivMod(YarnSpinnerParser.ExpMultDivModContext context)
        {
            var expressions = context.expression();

            var @operator = CodeGenerationVisitor.TokensToOperators[context.op.Type];

            // *, /, % all support numbers only
            Yarn.IType type = CheckOperation(context, expressions, @operator, context.op.Text);
            context.Type = type;
            return type;
        }

        

        public override Yarn.IType VisitExpComparison(YarnSpinnerParser.ExpComparisonContext context)
        {
            ParserRuleContext[] terms = context.expression();

            var @operator = CodeGenerationVisitor.TokensToOperators[context.op.Type];

            var type = CheckOperation(context, terms, @operator, context.op.Text);
            context.Type = type;

            // Comparisons always return bool
            return BuiltinTypes.Boolean;
        }

        public override Yarn.IType VisitExpEquality(YarnSpinnerParser.ExpEqualityContext context)
        {
            ParserRuleContext[] terms = context.expression();

            var @operator = CodeGenerationVisitor.TokensToOperators[context.op.Type];

            // == and != support any defined type, as long as terms are the
            // same type
            var determinedType = CheckOperation(context, terms, @operator, context.op.Text);

            context.Type = determinedType;

            // Equality checks always return bool
            return BuiltinTypes.Boolean;
        }

        public override Yarn.IType VisitExpNegative(YarnSpinnerParser.ExpNegativeContext context)
        {
            ParserRuleContext[] terms = new[] { context.expression() };

            var @operator = CodeGenerationVisitor.TokensToOperators[context.op.Type];

            Yarn.IType type = CheckOperation(context, terms, @operator, context.op.Text);
            context.Type = type;
            return type;

        }

        public override Yarn.IType VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            ParserRuleContext[] terms = new[] { context.expression() };

            var @operator = CodeGenerationVisitor.TokensToOperators[context.op.Type];

            // ! supports only bool types
            Yarn.IType type = CheckOperation(context, terms, @operator, context.op.Text);
            context.Type = type;

            return BuiltinTypes.Boolean;
        }

        public override Yarn.IType VisitLine_formatted_text([NotNull] YarnSpinnerParser.Line_formatted_textContext context)
        {
            VisitChildren(context);
            // Any expressions in this line that have no defined type will
            // be forced to be strings
            foreach (var expression in context.expression())
            {
                if (expression.Type == BuiltinTypes.Undefined)
                {
                    expression.Type = BuiltinTypes.String;
                }
            }

            return BuiltinTypes.String;
        }
    }
}
