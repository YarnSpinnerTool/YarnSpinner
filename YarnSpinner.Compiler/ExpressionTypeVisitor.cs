namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;

    /// <summary>
    /// A Visitor that walks an expression parse tree and returns its type.
    /// Call the <see cref="Visit"/> method to begin checking. If a single
    /// valid type for the parse tree can't be found, a TypeException is
    /// thrown.
    /// </summary>
    internal class ExpressionTypeVisitor : YarnSpinnerParserBaseVisitor<Yarn.Type>
    {

        // The variable declarations we know about
        protected IEnumerable<Declaration> Declarations;

        // If true, this expression may not involve any variables or
        // function calls
        protected bool RequireConstantExpression;

        public ExpressionTypeVisitor(IEnumerable<Declaration> variableDeclarations, bool requireConstantExpression)
        {
            Declarations = variableDeclarations;
            RequireConstantExpression = requireConstantExpression;            
        }

        protected override Yarn.Type DefaultResult => Yarn.Type.Undefined;

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
            if (RequireConstantExpression)
            {
                throw new TypeException(context, $"Can't use a variable here: expression must be constant");
            }

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

            throw new TypeException(context, $"Undeclared variable {name}");

        }

        public override Yarn.Type VisitValueNull(YarnSpinnerParser.ValueNullContext context)
        {
            throw new TypeException(context, "Null is not a permitted type in Yarn Spinner 2.0 and later");
        }

        public override Yarn.Type VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            string functionName = context.function().FUNC_ID().GetText();

            Declaration functionDeclaration = default;
            var declarationFound = false;

            foreach (var decl in Declarations) {
                if (decl.DeclarationType == Declaration.Type.Function && decl.Name == functionName) {
                    functionDeclaration = decl;
                    declarationFound = true;
                }
            }

            if (!declarationFound)
            {
                throw new TypeException(context, $"Undeclared function {functionName}");
            }

            // Check each parameter of the function
            var suppliedParameters = context.function().expression();

            Declaration.Parameter[] expectedParameters = functionDeclaration.Parameters;
            if (suppliedParameters.Length != expectedParameters.Length)
            {
                // Wrong number of parameters supplied
                var parameters = expectedParameters.Length == 1 ? "parameter" : "parameters";
                throw new TypeException(context, $"Function {functionName} expects {expectedParameters.Length} {parameters}, but received {suppliedParameters.Length}");
            }

            for (int i = 0; i < expectedParameters.Length; i++)
            {
                var suppliedParameter = suppliedParameters[i];

                var expectedType = expectedParameters[i].Type;

                var suppliedType = this.Visit(suppliedParameter);

                if (expectedType == Yarn.Type.Undefined)
                {
                    // The type of this parameter hasn't yet been bound.
                    // Bind this parameter type to what we've resolved the type to.
                    expectedParameters[i].Type = suppliedType;
                    expectedType = suppliedType;
                }

                if (suppliedType != expectedType) {
                    throw new TypeException(context, $"{functionName} parameter {i + 1} expects a {expectedType}, not a {suppliedType}");
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

        private Yarn.Type CheckOperation(ParserRuleContext context, ParserRuleContext[] terms, string operationType, params Yarn.Type[] permittedTypes)
        {

            var types = new List<Yarn.Type>();

            var expressionType = Yarn.Type.Undefined;

            foreach (var expression in terms)
            {
                Yarn.Type type = Visit(expression);
                types.Add(type);
                if (type != Yarn.Type.Undefined && expressionType == Yarn.Type.Undefined)
                {
                    // This is the first concrete type we've seen. This
                    // will be our expression type.
                    expressionType = type;
                }
            }

            // Do we have a known expression type, and were any of the
            // terms a function call whose return type is currently
            // unbound?
            if (expressionType == Yarn.Type.Undefined)
            {
                // We don't know what type of expression this is.
                throw new TypeException(context, $"Type of expression {context.GetText()} can't be determined without more context. Use a type cast on at least one of the terms (e.g. the string(), number(), bool() functions)");
            }
            else
            {
                // If so, bind their return types now.
                for (int i = 0; i < terms.Length; i++)
                {
                    ParserRuleContext expression = terms[i];
                    string funcName;

                    // If this is a "value that's in an expression", get
                    // the nested value
                    if (expression is YarnSpinnerParser.ExpValueContext expValueContext) {
                        expression = expValueContext.value();
                    }

                    if (expression is YarnSpinnerParser.ValueFuncContext valueFuncContext)
                    {
                        funcName = valueFuncContext.function().FUNC_ID().GetText();
                    }
                    else if (expression is YarnSpinnerParser.FunctionContext funcContext)
                    {
                        funcName = funcContext.FUNC_ID().GetText();
                    }
                    else
                    {
                        // Not a function term, so nothing to do
                        continue;
                    }

                    Declaration functionDeclaration = null;

                    foreach (var decl in this.Declarations)
                    {
                        if (decl.DeclarationType == Declaration.Type.Function && decl.Name == funcName)
                        {
                            functionDeclaration = decl;
                            break;
                        }
                    }

                    if (functionDeclaration == null)
                    {
                        throw new TypeException(context, $"Can't check return value of {funcName}: Function is undeclared");
                    }

                    if (functionDeclaration.ReturnType != Yarn.Type.Undefined)
                    {
                        // This function declaration is already bound
                        continue;
                    }

                    // Bind the function declaration's return type!
                    functionDeclaration.ReturnType = expressionType;

                    // Also update the type that we decided upon
                    types[i] = expressionType;
                }
            }

            string typeList;

            // All types must be same as the expression type
            for (int i = 1; i < types.Count; i++)
            {
                if (types[i] != expressionType)
                {
                    typeList = string.Join(", ", types);
                    throw new TypeException(context, $"All terms of {operationType} must be the same, not {typeList}");
                }
            }

            // The expression type must match one of the permitted types
            foreach (var type in permittedTypes)
            {
                if (expressionType == type)
                {
                    // This is one of the permitted types, so this
                    // expression is valid
                    return expressionType;
                }
            }

            // The expression type wasn't valid!
            var permittedTypesList = string.Join(" or ", permittedTypes);
            typeList = string.Join(", ", types);

            throw new TypeException(context, $"Terms of {operationType} must be {permittedTypesList}, not {typeList}");
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
            switch (context.type().typename.Type) {
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
