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
    internal class TypeCheckVisitor : YarnSpinnerParserBaseVisitor<Yarn.Type>
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
        public TypeCheckVisitor(string sourceFileName, IEnumerable<Declaration> existingDeclarations)
        {
            this.existingDeclarations = existingDeclarations;
            this.NewDeclarations = new List<Declaration>();
            this.sourceFileName = sourceFileName;
        }

        protected override Yarn.Type DefaultResult => Yarn.Type.Undefined;

        public override Yarn.Type VisitNode(YarnSpinnerParser.NodeContext context) {
            currentNodeContext = context;
            foreach (var header in context.header()) {
                if (header.header_key.Text == "title") {
                    currentNodeName = header.header_value.Text;
                }
            }
            Visit(context.body());

            return Yarn.Type.Undefined;
        }

        public override Yarn.Type VisitValueNull([NotNull] YarnSpinnerParser.ValueNullContext context)
        {
            throw new TypeException(context, "Null is not a permitted type in Yarn Spinner 2.0 and later", sourceFileName);
        }

        public override Yarn.Type VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            return Yarn.Type.String;
        }

        public override Yarn.Type VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return Yarn.Type.Bool;
        }

        public override Yarn.Type VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return Yarn.Type.Bool;
        }

        public override Yarn.Type VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            return Yarn.Type.Number;
        }

        public override Yarn.Type VisitValueVar(YarnSpinnerParser.ValueVarContext context)
        {

            return VisitVariable(context.variable());
        }

        public override Yarn.Type VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            // The type of the value depends on the declared type of the
            // variable

            var name = context.VAR_ID().GetText();

            foreach (var declaration in Declarations)
            {
                if (declaration.Name == name)
                {
                    return declaration.ReturnType;
                }
            }

            // We don't have a declaration for this variable. Return
            // Undefined. Hopefully, other context will allow us to infer a
            // type.
            return Yarn.Type.Undefined;

        }

        public override Yarn.Type VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            string functionName = context.function().FUNC_ID().GetText();

            Declaration functionDeclaration = Declarations
                .Where(d => d.DeclarationType == Declaration.Type.Function)
                .FirstOrDefault(d => d.Name == functionName);

            if (functionDeclaration == null) {
                // We don't have a declaration for this function. Create an
                // implicit one.
                functionDeclaration = new Declaration {
                    Name = functionName,
                    DeclarationType = Declaration.Type.Function,
                    IsImplicit = true,
                    ReturnType = Yarn.Type.Undefined,
                    Description = $"Implicit declaration of function at {sourceFileName}:{context.Start.Line}:{context.Start.Column}",
                    SourceFileName = sourceFileName,
                    SourceFileLine = context.Start.Line,
                    SourceNodeName = currentNodeName,
                    SourceNodeLine = context.Start.Line - (this.currentNodeContext.BODY_START().Symbol.Line + 1),
                };

                // Create the array of parameters for this function based
                // on how many we've seen in this call. Set them all to be
                // undefined; we'll bind their type shortly.
                functionDeclaration.Parameters = context.function().expression()
                    .Select(e => new Declaration.Parameter { Type = Yarn.Type.Undefined })
                    .ToArray();

                NewDeclarations.Add(functionDeclaration);
            }
            
            // Check each parameter of the function
            var suppliedParameters = context.function().expression();

            Declaration.Parameter[] expectedParameters = functionDeclaration.Parameters;
            if (suppliedParameters.Length != expectedParameters.Length)
            {
                // Wrong number of parameters supplied
                var parameters = expectedParameters.Length == 1 ? "parameter" : "parameters";
                throw new TypeException(context, $"Function {functionName} expects {expectedParameters.Length} {parameters}, but received {suppliedParameters.Length}", sourceFileName);
            }

            for (int i = 0; i < expectedParameters.Length; i++)
            {
                var suppliedParameter = suppliedParameters[i];

                var expectedType = expectedParameters[i].Type;

                var suppliedType = this.Visit(suppliedParameter);

                if (expectedType == Yarn.Type.Undefined)
                {
                    // The type of this parameter hasn't yet been bound.
                    // Bind this parameter type to what we've resolved the
                    // type to.
                    expectedParameters[i].Type = suppliedType;
                    expectedType = suppliedType;
                }

                if (suppliedType != expectedType)
                {
                    throw new TypeException(context, $"{functionName} parameter {i + 1} expects a {expectedType}, not a {suppliedType}", sourceFileName);
                }
            }

            // Cool, all the parameters check out!

            // Finally, return the return type of this function.
            return functionDeclaration.ReturnType;
        }

        public override Yarn.Type VisitExpValue(YarnSpinnerParser.ExpValueContext context)
        {
            // Value expressions have the type of their inner value
            return Visit(context.value());
        }

        public override Yarn.Type VisitExpParens(YarnSpinnerParser.ExpParensContext context)
        {
            // Parens expressions have the type of their inner expression
            return Visit(context.expression());
        }

        public override Yarn.Type VisitExpAndOrXor(YarnSpinnerParser.ExpAndOrXorContext context)
        {
            return CheckOperation(context, context.expression(), context.op.Text, Yarn.Type.Bool);
        }

        public override Yarn.Type VisitSetVariableToValue([NotNull] YarnSpinnerParser.SetVariableToValueContext context)
        {
            var expressionType = Visit(context.expression());
            var variableType = Visit(context.variable());

            var variableName = context.variable().GetText();

            if (expressionType == Yarn.Type.Undefined) {
                // We don't know what this is set to, so we'll have to
                // assume it's ok. Return the variable type, if known.
                return variableType;
            }

            if (variableType == Yarn.Type.Undefined) {
                // We don't have a type for this variable. Create an
                // implicit declaration that it's this type.

                // Get the position of this reference in the file
                int positionInFile = context.Start.Line;
            
                // The start line of the body is the line after the delimiter
                int nodePositionInFile = this.currentNodeContext.BODY_START().Symbol.Line + 1;
                
                var decl = new Declaration {
                    Name = variableName,
                    DeclarationType = Declaration.Type.Variable,
                    Description = $"{System.IO.Path.GetFileName(sourceFileName)}, node {currentNodeName}, line {positionInFile - nodePositionInFile}",
                    ReturnType = expressionType,
                    DefaultValue = DefaultValueForType(expressionType),
                    SourceFileName = sourceFileName,
                    SourceFileLine = positionInFile,
                    SourceNodeName = currentNodeName,
                    SourceNodeLine = positionInFile - nodePositionInFile,
                    IsImplicit = true,
                };
                NewDeclarations.Add(decl);
            }
            else if (expressionType != variableType)
            {
                throw new TypeException(context, $"{variableName} ({variableType}) cannot be assigned a {expressionType}", sourceFileName);
            }

            return expressionType;
        }

        private Yarn.Type CheckOperation(ParserRuleContext context, ParserRuleContext[] terms, string operationType, params Yarn.Type[] permittedTypes)
        {

            var termTypes = new List<Yarn.Type>();

            var expressionType = Yarn.Type.Undefined;

            foreach (var expression in terms)
            {
                // Visit this expression, and determine its type.
                Yarn.Type type = Visit(expression);

                if (type != Yarn.Type.Undefined)
                {
                    termTypes.Add(type);
                    if (expressionType == Yarn.Type.Undefined) {
                        // This is the first concrete type we've seen. This
                        // will be our expression type.
                        expressionType = type;
                    }
                }
            }

            if (permittedTypes.Length == 1 && expressionType == Yarn.Type.Undefined) {
                // If we aren't sure of the expression type from
                // parameters, but we only have one permitted one, then
                // assume that the expression type is the single permitted
                // type.
                expressionType = permittedTypes.First();
            }

            if (expressionType == Yarn.Type.Undefined)
            {
                // We still don't know what type of expression this is, and
                // don't have a reasonable guess.
                throw new TypeException(context, $"Type of expression {context.GetText()} can't be determined without more context. Use a type cast on at least one of the terms (e.g. the string(), number(), bool() functions)", sourceFileName);
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
                        DeclarationType = Declaration.Type.Variable,
                        Description = $"{System.IO.Path.GetFileName(sourceFileName)}, node {currentNodeName}, line {positionInFile - nodePositionInFile}",
                        ReturnType = expressionType,
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
                var typeList = string.Join(", ", termTypes);
                throw new TypeException(context, $"All terms of {operationType} must be the same, not {typeList}", sourceFileName);
            }
            
            // The expression type must match one of the permitted types.
            // If the type we've found is one of these permitted types,
            // return it - the expression is valid, and we're done.
            if (permittedTypes.Contains(expressionType))
            {
                return expressionType;
            }
            else
            {
                // The expression type wasn't valid!
                var permittedTypesList = string.Join(" or ", permittedTypes);
                var typeList = string.Join(", ", termTypes);

                throw new TypeException(context, $"Terms of '{operationType}' must be {permittedTypesList}, not {typeList}", sourceFileName);
            }

        }

        private static object DefaultValueForType(Yarn.Type expressionType)
        {
            switch (expressionType)
            {
                case Yarn.Type.Number:
                    return 0;
                case Yarn.Type.String:
                    return string.Empty;
                case Yarn.Type.Bool:
                    return false;
                case Yarn.Type.Undefined:
                default:
                    throw new ArgumentOutOfRangeException($"No default value for the Undefined type exists.");
            }
        }

        public override Yarn.Type VisitIf_clause(YarnSpinnerParser.If_clauseContext context)
        {
            // If clauses are required to be boolean
            var expressions = new[] { context.expression() };
            return CheckOperation(context, expressions, "if statement", Yarn.Type.Bool);
        }

        public override Yarn.Type VisitElse_if_clause(YarnSpinnerParser.Else_if_clauseContext context)
        {
            // Else if clauses are required to be boolean
            var expressions = new[] { context.expression() };
            return CheckOperation(context, expressions, "if statement", Yarn.Type.Bool);
        }

        public override Yarn.Type VisitExpAddSub(YarnSpinnerParser.ExpAddSubContext context)
        {

            var expressions = context.expression();

            switch (context.op.Text)
            {
                case "+":
                    // + supports strings and numbers
                    return CheckOperation(context, expressions, context.op.Text, Yarn.Type.String, Yarn.Type.Number);
                case "-":
                    // - supports only numbers
                    return CheckOperation(context, expressions, context.op.Text, Yarn.Type.Number);
                default:
                    throw new InvalidOperationException($"Internal error: {nameof(VisitExpAddSub)} got unexpected op {context.op.Text}");
            }
        }

        public override Yarn.Type VisitExpMultDivMod(YarnSpinnerParser.ExpMultDivModContext context)
        {
            var expressions = context.expression();

            // *, /, % all support numbers only
            return CheckOperation(context, expressions, context.op.Text, Yarn.Type.Number);
        }

        public override Yarn.Type VisitExpPlusMinusEquals(YarnSpinnerParser.ExpPlusMinusEqualsContext context)
        {
            ParserRuleContext[] terms = { context.variable(), context.expression() };

            switch (context.op.Text)
            {
                case "+=":
                    // + supports strings and numbers
                    return CheckOperation(context, terms, context.op.Text, Yarn.Type.String, Yarn.Type.Number);
                case "-=":
                    // - supports only numbers
                    return CheckOperation(context, terms, context.op.Text, Yarn.Type.Number);
                default:
                    throw new InvalidOperationException($"Internal error: {nameof(VisitExpMultDivMod)} got unexpected op {context.op.Text}");
            }
        }

        public override Yarn.Type VisitExpMultDivModEquals(YarnSpinnerParser.ExpMultDivModEqualsContext context)
        {
            ParserRuleContext[] terms = { context.variable(), context.expression() };

            // *, /, % all support numbers only
            return CheckOperation(context, terms, context.op.Text, Yarn.Type.Number);
        }

        public override Yarn.Type VisitExpComparison(YarnSpinnerParser.ExpComparisonContext context)
        {
            ParserRuleContext[] terms = context.expression();

            // <, <=, >, >= all support numbers only
            CheckOperation(context, terms, context.op.Text, Yarn.Type.Number);

            // Comparisons always return bool
            return Yarn.Type.Bool;
        }

        public override Yarn.Type VisitExpEquality(YarnSpinnerParser.ExpEqualityContext context)
        {
            ParserRuleContext[] terms = context.expression();

            // == and != support any defined type, as long as terms are the
            // same type
            CheckOperation(context, terms, context.op.Text, Yarn.Type.Number, Yarn.Type.String, Yarn.Type.Bool);

            // Equality checks always return bool
            return Yarn.Type.Bool;
        }

        public override Yarn.Type VisitExpNegative(YarnSpinnerParser.ExpNegativeContext context)
        {
            ParserRuleContext[] terms = new[] { context.expression() };

            // - supports only number types
            return CheckOperation(context, terms, "-", Yarn.Type.Number);

        }

        public override Yarn.Type VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            ParserRuleContext[] terms = new[] { context.expression() };

            // ! supports only bool types
            return CheckOperation(context, terms, "!", Yarn.Type.Bool);
        }

        public override Yarn.Type VisitExpTypeConversion(YarnSpinnerParser.ExpTypeConversionContext context)
        {
            // Validate the type of the expression; the actual conversion
            // will be done at runtime, and may fail depending on the
            // actual value
            Visit(context.expression());

            // Return a value whose type depends on which type we're using
            switch (context.type().typename.Type)
            {
                case YarnSpinnerLexer.TYPE_NUMBER:
                    return Yarn.Type.Number;
                case YarnSpinnerLexer.TYPE_STRING:
                    return Yarn.Type.String;
                case YarnSpinnerLexer.TYPE_BOOL:
                    return Yarn.Type.Bool;
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported type {context.type().GetText()}");
            }
        }
    }
}
