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
    internal class ExpressionTypeVisitor : YarnSpinnerParserBaseVisitor<Value.Type>
    {

        // The variable declarations we know about
        protected IEnumerable<VariableDeclaration> VariableDeclarations;

        // If true, this expression may not involve any variables or
        // function calls
        protected bool RequireConstantExpression;

        // The function declarations we know about
        public Library Library { get; private set; }

        public ExpressionTypeVisitor(IEnumerable<VariableDeclaration> variableDeclarations, Library library, bool requireConstantExpression)
        {
            VariableDeclarations = variableDeclarations;
            RequireConstantExpression = requireConstantExpression;
            Library = library;
        }

        protected override Value.Type DefaultResult => Value.Type.Undefined;

        public override Value.Type VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            return Value.Type.String;
        }

        public override Value.Type VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return Value.Type.Bool;
        }

        public override Value.Type VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return Value.Type.Bool;
        }

        public override Value.Type VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            return Value.Type.Number;
        }

        public override Value.Type VisitValueVar(YarnSpinnerParser.ValueVarContext context)
        {

            return VisitVariable(context.variable());
        }

        public override Value.Type VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            if (RequireConstantExpression)
            {
                throw new TypeException(context, $"Can't use a variable here: expression must be constant");
            }

            // The type of the value depends on the declared type of the
            // variable

            var name = context.VAR_ID().GetText();

            foreach (var declaration in VariableDeclarations)
            {
                if (declaration.name == name)
                {
                    return declaration.type;
                }
            }

            throw new TypeException(context, $"Undeclared variable {name}");

        }

        public override Value.Type VisitValueNull(YarnSpinnerParser.ValueNullContext context)
        {
            throw new TypeException("Null is not a permitted type in Yarn Spinner 2.0 and later");
        }

        public override Value.Type VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {

            if (this.Library == null)
            {
                throw new TypeException($"No library provided. Functions are not available.");
            }

            string functionName = context.function().FUNC_ID().GetText();

            if (this.Library.FunctionExists(functionName) == false)
            {
                throw new TypeException($"Undeclared function {functionName}");
            }

            var function = this.Library.GetFunction(functionName);

            // Check each parameter of the function
            System.Reflection.ParameterInfo[] expectedParameters = function.Method.GetParameters();
            var suppliedParameters = context.function().expression();

            if (suppliedParameters.Length > expectedParameters.Length)
            {
                // Too many parameters supplied
                var parameters = expectedParameters.Length == 1 ? "parameter" : "parameters";
                throw new TypeException($"Function {functionName} expects {expectedParameters.Length} {parameters}, but received {suppliedParameters.Length}");
            }

            var typeMappings = new List<(Type NativeType, Value.Type YarnType)>
            {
                (typeof(string), Value.Type.String),
                (typeof(bool), Value.Type.Bool),
                (typeof(int), Value.Type.Number),
                (typeof(float), Value.Type.Number),
                (typeof(double), Value.Type.Number),
                (typeof(sbyte), Value.Type.Number),
                (typeof(byte), Value.Type.Number),
                (typeof(short), Value.Type.Number),
                (typeof(ushort), Value.Type.Number),
                (typeof(uint), Value.Type.Number),
                (typeof(long), Value.Type.Number),
                (typeof(ulong), Value.Type.Number),
                (typeof(float), Value.Type.Number),
            };

            for (int i = 0; i < expectedParameters.Length; i++)
            {
                System.Reflection.ParameterInfo parameter = expectedParameters[i];

                if (i >= suppliedParameters.Length)
                {
                    if (parameter.IsOptional == false)
                    {
                        // Not enough parameters supplied
                        var parameters = expectedParameters.Length == 1 ? "parameter" : "parameters";
                        throw new TypeException($"Function {functionName} expects {expectedParameters.Length} {parameters}, but received {suppliedParameters.Length}");
                    }
                    else
                    {
                        // A parameter wasn't supplied, but it was
                        // optional, so that's ok. Stop checking parameters
                        // here, because there aren't any more.
                        break;
                    }
                }

                var suppliedParameter = suppliedParameters[i];

                var expectedType = parameter.ParameterType;

                var suppliedType = this.Visit(suppliedParameter);

                bool expectedTypeIsValid = false;

                foreach (var mapping in typeMappings)
                {
                    if (mapping.NativeType.IsAssignableFrom(expectedType))
                    {
                        if (suppliedType != mapping.YarnType)
                        {
                            throw new TypeException($"{functionName} parameter {i + 1} expects a {mapping.YarnType}, not a {suppliedType}");
                        }
                        else
                        {
                            // This parameter's expected type is valid, and
                            // the supplied type can be used with it.
                            expectedTypeIsValid = true;
                            break;
                        }
                    }
                }

                if (expectedTypeIsValid == false)
                {
                    throw new TypeException($"{functionName} cannot be called: parameter {i + 1}'s type ({expectedType}) cannot be used in Yarn functions");
                }
            }

            // Cool, all the parameters check out!

            // Last thing: check the return type. This will be the type of
            // this function call.
            var returnType = function.Method.ReturnType;

            foreach (var mapping in typeMappings)
            {
                if (mapping.NativeType.IsAssignableFrom(returnType))
                {
                    return mapping.YarnType;
                }
            }

            // Argh, this is an invalid function
            throw new TypeException($"Function {functionName} can't be called, because it returns an invalid type ({returnType})");
        }

        public override Value.Type VisitExpValue(YarnSpinnerParser.ExpValueContext context)
        {
            // Value expressions have the type of their inner value
            return Visit(context.value());
        }

        public override Value.Type VisitExpParens(YarnSpinnerParser.ExpParensContext context)
        {
            // Parens expressions have the type of their inner expression
            return Visit(context.expression());
        }

        public override Value.Type VisitExpAndOrXor(YarnSpinnerParser.ExpAndOrXorContext context)
        {
            // And/Or/Xor expressions require both child expressions to be
            // Bool, and have a type of Bool
            var expressions = context.expression();

            Value.Type left = Visit(expressions[0]);
            Value.Type right = Visit(expressions[1]);

            if (left != Value.Type.Bool || right != Value.Type.Bool)
            {
                throw new TypeException(context, $"Both sides of {context.op.Text} must be bool, not {left} + {right}");
            }

            return Value.Type.Bool;
        }

        private Value.Type CheckOperation(ParserRuleContext context, ParserRuleContext[] terms, string operationType, params Value.Type[] permittedTypes)
        {

            var types = new List<Value.Type>();

            var expressionType = Value.Type.Undefined;

            foreach (var expression in terms)
            {
                Value.Type type = Visit(expression);
                types.Add(type);
                if (expressionType == Value.Type.Undefined)
                {
                    // This is the first concrete type we've seen. This
                    // will be our expression type.
                    expressionType = type;
                }
            }

            // The expression type that we've seen must not be Any or
            // Undefined - it needs to be a concrete type.
            if (expressionType == Value.Type.Undefined)
            {
                throw new TypeException(context, $"Can't determine a type for operands");
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

            // Types must match one of the permitted types
            foreach (var type in permittedTypes)
            {
                if (expressionType == type)
                {
                    // This is one of the permitted types, so this
                    // expression is valid
                    return expressionType;
                }
            }

            var permittedTypesList = string.Join(" or ", permittedTypes);
            typeList = string.Join(", ", types);

            throw new TypeException(context, $"Terms of {operationType} must be {permittedTypesList}, not {typeList}");
        }

        public override Value.Type VisitExpAddSub(YarnSpinnerParser.ExpAddSubContext context)
        {

            var expressions = context.expression();

            switch (context.op.Text)
            {
                case "+":
                    // + supports strings and numbers
                    return CheckOperation(context, expressions, context.op.Text, Value.Type.String, Value.Type.Number);
                case "-":
                    // - supports only numbers
                    return CheckOperation(context, expressions, context.op.Text, Value.Type.Number);
                default:
                    throw new InvalidOperationException($"Internal error: {nameof(VisitExpAddSub)} got unexpected op {context.op.Text}");
            }
        }

        public override Value.Type VisitExpMultDivMod(YarnSpinnerParser.ExpMultDivModContext context)
        {
            var expressions = context.expression();

            // *, /, % all support numbers only
            return CheckOperation(context, expressions, context.op.Text, Value.Type.Number);
        }

        public override Value.Type VisitExpPlusMinusEquals(YarnSpinnerParser.ExpPlusMinusEqualsContext context)
        {
            ParserRuleContext[] terms = { context.variable(), context.expression() };

            switch (context.op.Text)
            {
                case "+=":
                    // + supports strings and numbers
                    return CheckOperation(context, terms, context.op.Text, Value.Type.String, Value.Type.Number);
                case "-=":
                    // - supports only numbers
                    return CheckOperation(context, terms, context.op.Text, Value.Type.Number);
                default:
                    throw new InvalidOperationException($"Internal error: {nameof(VisitExpMultDivMod)} got unexpected op {context.op.Text}");
            }
        }

        public override Value.Type VisitExpMultDivModEquals(YarnSpinnerParser.ExpMultDivModEqualsContext context)
        {
            ParserRuleContext[] terms = { context.variable(), context.expression() };

            // *, /, % all support numbers only
            return CheckOperation(context, terms, context.op.Text, Value.Type.Number);
        }

        public override Value.Type VisitExpComparison(YarnSpinnerParser.ExpComparisonContext context)
        {
            ParserRuleContext[] terms = context.expression();

            // <, <=, >, >= all support numbers only
            CheckOperation(context, terms, context.op.Text, Value.Type.Number);

            // Comparisons always return bool
            return Value.Type.Bool;
        }

        public override Value.Type VisitExpEquality(YarnSpinnerParser.ExpEqualityContext context)
        {
            ParserRuleContext[] terms = context.expression();

            // == and != support any defined type, as long as terms are the
            // same type
            CheckOperation(context, terms, context.op.Text, Value.Type.Number, Value.Type.String, Value.Type.Bool);

            // Equality checks always return bool
            return Value.Type.Bool;
        }

        public override Value.Type VisitExpNegative(YarnSpinnerParser.ExpNegativeContext context)
        {
            ParserRuleContext[] terms = new[] { context.expression() };

            // - supports only number types
            return CheckOperation(context, terms, "-", Value.Type.Number);

        }

        public override Value.Type VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            ParserRuleContext[] terms = new[] { context.expression() };

            // ! supports only bool types
            return CheckOperation(context, terms, "!", Value.Type.Bool);
        }


    }
}
