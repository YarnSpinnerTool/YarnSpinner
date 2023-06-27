// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

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
        // {0} = variable name
        private const string CantDetermineVariableTypeError = "Can't figure out the type of variable {0} given its context. Specify its type with a <<declare>> statement.";

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

        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();

        // the list of variables we aren't actually sure about
        public List<DeferredTypeDiagnostic> deferredTypes = new List<DeferredTypeDiagnostic>();

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

        public IEnumerable<Diagnostic> Diagnostics => diagnostics;

        public override Yarn.IType VisitNode(YarnSpinnerParser.NodeContext context)
        {
            currentNodeContext = context;
            foreach (var header in context.header())
            {
                if (header.header_key.Text == "title")
                {
                    currentNodeName = header.header_value.Text;
                }
            }

            var body = context.body();

            if (body != null)
            {
                base.Visit(body);
            }

            return null;
        }

        public override Yarn.IType VisitValueNull([NotNull] YarnSpinnerParser.ValueNullContext context)
        {
            this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, "Null is not a permitted type in Yarn Spinner 2.0 and later"));

            return BuiltinTypes.Undefined;
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
            var name = context.VAR_ID()?.GetText();

            if (name == null)
            {
                // We don't have a variable name for this Variable context.
                // The parser will have generated an error for us in an
                // earlier stage; here, we'll bail out.
                return BuiltinTypes.Undefined;
            }

            foreach (var declaration in Declarations)
            {
                if (declaration.Name == name)
                {
                    return declaration.Type;
                }
            }

            // do we already have a potential warning about this?
            // no need to make more
            foreach (var hmm in deferredTypes)
            {
                if (hmm.Name == name)
                {
                    return BuiltinTypes.Undefined;
                }
            }

            // creating a new diagnostic for us having an undefined variable
            // this won't get added into the existing diags though because its possible a later pass will clear it up
            // so we save this as a potential diagnostic for the compiler itself to resolve
            var diagnostic = new Diagnostic(sourceFileName, context, string.Format(CantDetermineVariableTypeError, name));
            deferredTypes.Add(DeferredTypeDiagnostic.CreateDeferredTypeDiagnostic(name, diagnostic));

            // We don't have a declaration for this variable. Return
            // Undefined. Hopefully, other context will allow us to infer a
            // type.
            return BuiltinTypes.Undefined;
        }

        public override Yarn.IType VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            string functionName = context.function_call().FUNC_ID().GetText();

            Declaration functionDeclaration = Declarations
                .Where(d => d.Type is FunctionType)
                .FirstOrDefault(d => d.Name == functionName);

            FunctionType functionType;

            if (functionDeclaration == null)
            {
                // We don't have a declaration for this function. Create an
                // implicit one.

                functionType = new FunctionType();
                // because it is an implicit declaration we will use the type hint to give us a return type
                functionType.ReturnType = context.Hint != BuiltinTypes.Undefined ? context.Hint : BuiltinTypes.Undefined;

                functionDeclaration = new Declaration
                {
                    Name = functionName,
                    Type = functionType,
                    IsImplicit = true,
                    Description = $"Implicit declaration of function at {sourceFileName}:{context.Start.Line}:{context.Start.Column}",
                    SourceFileName = sourceFileName,
                    SourceNodeName = currentNodeName,
                    Range = new Range
                    {
                        Start =
                        {
                            Line = context.Start.Line - 1,
                            Character = context.Start.Column,
                        },
                        End =
                        {
                            Line = context.Stop.Line - 1,
                            Character = context.Stop.Column + context.Stop.Text.Length,
                        },
                    },
                };

                // Create the array of parameters for this function based
                // on how many we've seen in this call. Set them all to be
                // undefined; we'll bind their type shortly.
                var parameterTypes = context.function_call().expression()
                    .Select(e => BuiltinTypes.Undefined)
                    .ToList();

                foreach (var parameterType in parameterTypes)
                {
                    functionType.AddParameter(parameterType);
                }

                NewDeclarations.Add(functionDeclaration);
            }
            else
            {
                var a = (FunctionType)functionDeclaration.Type;
                functionType = functionDeclaration.Type as FunctionType;
                if (functionType == null)
                {
                    throw new InvalidOperationException($"Internal error: decl's type is not a {nameof(FunctionType)}");
                }

                // we have an existing function but its undefined
                // if we also have a type hint we can use that to update it
                if (functionType.ReturnType == BuiltinTypes.Undefined && context.Hint != BuiltinTypes.Undefined)
                {
                    NewDeclarations.Remove(functionDeclaration);
                    functionType.ReturnType = context.Hint;
                    functionDeclaration.Type = functionType;
                    NewDeclarations.Add(functionDeclaration);
                }
            }

            // Check each parameter of the function
            var suppliedParameters = context.function_call().expression();

            var expectedParameters = functionType.Parameters;

            if (suppliedParameters.Length != expectedParameters.Count())
            {
                // Wrong number of parameters supplied
                var parameters = expectedParameters.Count() == 1 ? "parameter" : "parameters";

                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context,  $"Function {functionName} expects {expectedParameters.Count()} {parameters}, but received {suppliedParameters.Length}"));

                return functionType.ReturnType;
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
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, $"{functionName} parameter {i + 1} expects a {expectedType?.Name ?? "undefined"}, not a {suppliedType?.Name ?? "undefined"}"));
                    return functionType.ReturnType;
                }
            }

            // Cool, all the parameters check out!

            // Finally, return the return type of this function.
            return functionType.ReturnType;
        }

        public override Yarn.IType VisitExpValue(YarnSpinnerParser.ExpValueContext context)
        {
            // passing the hint from the expression down into the values within
            context.value().Hint = context.Hint;
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
            var variableContext = context.variable();
            var expressionContext = context.expression();

            if (expressionContext == null || variableContext == null)
            {
                return BuiltinTypes.Undefined;
            }

            var variableType = base.Visit(variableContext);
            if (variableType != BuiltinTypes.Undefined)
            {
                // giving the expression a hint just in case it is needed to help resolve any ambiguity on the expression
                // currently this is only useful in situations where we have a function as the rvalue of a known lvalue
                expressionContext.Hint = variableType;
            }

            var expressionType = base.Visit(expressionContext);

            var variableName = variableContext.GetText();

            ParserRuleContext[] terms = { variableContext, expressionContext };

            Operator @operator;

            switch (context.op.Type)
            {
                case YarnSpinnerLexer.OPERATOR_ASSIGNMENT:
                    // Straight assignment supports any assignment, as long
                    // as it's consistent; we already know the type of the
                    // expression, so let's check to see if it's assignable
                    // to the type of the variable
                    if (variableType != BuiltinTypes.Undefined && TypeUtil.IsSubType(variableType, expressionType) == false)
                    {
                        string message = $"{variableName} ({variableType?.Name ?? "undefined"}) cannot be assigned a {expressionType?.Name ?? "undefined"}";
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                    }
                    else if (variableType == BuiltinTypes.Undefined && expressionType != BuiltinTypes.Undefined)
                    {
                        // This variable was undefined, but we have a
                        // defined type for the value it was set to. Create
                        // an implicit declaration for the variable!

                        // The start line of the body is the line after the delimiter
                        int nodePositionInFile = this.currentNodeContext.BODY_START().Symbol.Line + 1;

                        // Attempt to get a default value for the given type. If
                        // we can't get one, we can't create the definition.
                        var canCreateDefaultValue = TryGetDefaultValueForType(expressionType, out var defaultValue);

                        if (!canCreateDefaultValue)
                        {
                            diagnostics.Add(new Diagnostic(sourceFileName, variableContext, string.Format(CantDetermineVariableTypeError, variableName)));
                            break;
                        }

                        // Generate a declaration for this variable here.
                        var decl = new Declaration
                        {
                            Name = variableName,
                            Description = $"Implicitly declared in {System.IO.Path.GetFileName(sourceFileName)}, node {currentNodeName}",
                            Type = expressionType,
                            DefaultValue = defaultValue,
                            SourceFileName = sourceFileName,
                            SourceNodeName = currentNodeName,
                            Range = new Range
                            {
                                Start =
                                {
                                    Line = variableContext.Start.Line - 1,
                                    Character = variableContext.Start.Column,
                                },
                                End =
                                {
                                    Line = variableContext.Stop.Line - 1,
                                    Character = variableContext.Stop.Column + variableContext.GetText().Length,
                                },
                            },
                            IsImplicit = true,
                        };
                        NewDeclarations.Add(decl);
                    }
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_ADDITION_EQUALS:
                    // += supports strings and numbers
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_ADDITION];
                    expressionType = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION_EQUALS:
                    // -=, *=, /=, %= supports only numbers
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION];
                    expressionType = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION_EQUALS:
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION];
                    expressionType = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_DIVISION_EQUALS:
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_DIVISION];
                    expressionType = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_MODULUS_EQUALS:
                    @operator = CodeGenerationVisitor.TokensToOperators[YarnSpinnerLexer.OPERATOR_MATHS_MODULUS];
                    expressionType = CheckOperation(context, terms, @operator, context.op.Text);
                    break;
                default:
                    throw new InvalidOperationException($"Internal error: {nameof(VisitSet_statement)} got unexpected operand {context.op.Text}");
            }

            if (variableType == BuiltinTypes.Undefined && expressionType == BuiltinTypes.Undefined)
            {
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, $"Type of expression \"{context.GetTextWithWhitespace()}\" can't be determined without more context. Please declare one or more terms.", Diagnostic.DiagnosticSeverity.Error));
            }

            // at this point we have either fully resolved the type of the expression or been unable to do so
            // we return the type of the expression regardless and rely on either elements to catch the issue
            return expressionType;
        }

        // ok so what do we actually need to do in here?
        // we need to do a few different things
        // basically we need to go through the various types in the expression
        // if any are known we need to basically log that
        // then at the end if there are still unknowns we check if the operation itself forces a type
        // so if we have say Undefined = Undefined + Number then we know that only one operation supports + Number and that is Number + Number
        // so we can slot the type into the various parts
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
                    if (expressionType == BuiltinTypes.Undefined)
                    {
                        // This is the first concrete type we've seen. This
                        // will be our expression type.
                        expressionType = type;
                    }
                }
            }

            if (permittedTypes.Length == 1 && expressionType == BuiltinTypes.Undefined)
            {
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

                    string message = $"Type of expression \"{context.GetTextWithWhitespace()}\" can't be determined without more context (the compiler thinks it could be {string.Join(", or ", typeNames)}). Use a type cast on at least one of the terms (e.g. the string(), number(), bool() functions)";

                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                    return BuiltinTypes.Undefined;
                }
                else
                {
                    // No types implement this operation (??)
                    string message = $"Type of expression \"{context.GetTextWithWhitespace()}\" can't be determined without more context. Use a type cast on at least one of the terms (e.g. the string(), number(), bool() functions)";
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                    return BuiltinTypes.Undefined;
                }
            }

            // to reach this point we have either worked out the final type of the expression
            // or had to give up, and if we gave up we have nothing left to do
            // there are then two parts to this, first we need to declare the implict type of any variables (that appears to be working)
            // or the implicit type of any function.
            // annoyingly the function will already have an implicit definition created for it
            // we will have to strip that out and add in a new one with the new return type
            foreach (var term in terms)
            {
                if (term is YarnSpinnerParser.ExpValueContext)
                {
                    var value = ((YarnSpinnerParser.ExpValueContext)term).value();
                    if (value is YarnSpinnerParser.ValueFuncContext)
                    {
                        var id = ((YarnSpinnerParser.ValueFuncContext)value).function_call().FUNC_ID().GetText();

                        Declaration functionDeclaration = NewDeclarations.Where(d => d.Type is FunctionType).FirstOrDefault(d => d.Name == id);
                        if (functionDeclaration != null)
                        {
                            var func = functionDeclaration.Type as FunctionType;
                            if (func?.ReturnType == BuiltinTypes.Undefined)
                            {
                                NewDeclarations.Remove(functionDeclaration);
                                func.ReturnType = expressionType;
                                NewDeclarations.Add(functionDeclaration);
                            }
                        }
                        else
                        {
                            Visit(term);
                        }
                    }
                }
            }

            // Were any of the terms variables for which we don't currently
            // have a declaration for?

            // Start by building a list of all terms that are variables.
            // These are either variable values, or variable names . (The
            // difference between these two is that a ValueVarContext
            // occurs in syntax where the value of the variable is used
            // (like an expression), while a VariableContext occurs in
            // syntax where it's just a variable name (like a set
            // statements)

            // All VariableContexts in the terms of this expression (but
            // not in the children of those terms)
            var variableContexts = terms
                .Select(c => c.GetChild<YarnSpinnerParser.ValueVarContext>(0)?.variable())
                .Concat(terms.Select(c => c.GetChild<YarnSpinnerParser.VariableContext>(0)))
                .Concat(terms.OfType<YarnSpinnerParser.VariableContext>())
                .Concat(terms.OfType<YarnSpinnerParser.ValueVarContext>().Select(v => v.variable()))
                .Where(c => c != null);

            // Build the list of variable contexts that we don't have a
            // declaration for. We'll check for explicit declarations first.
            var undefinedVariableContexts = variableContexts
                .Where(v => Declarations.Any(d => d.Name == v.VAR_ID().GetText()) == false)
                .Distinct();

            if (undefinedVariableContexts.Count() > 0)
            {
                // We have references to variables that we don't have a an
                // explicit declaration for! Time to create implicit
                // references for them!

                // Get the position of this reference in the file
                int positionInFile = context.Start.Line;

                // The start line of the body is the line after the delimiter
                int nodePositionInFile = this.currentNodeContext.BODY_START().Symbol.Line + 1;

                foreach (var undefinedVariableContext in undefinedVariableContexts)
                {
                    // We can only create an implicit declaration for a variable
                    // if we have a default value for it, because all variables
                    // are required to have a value. If we can't, it's generally
                    // because we couldn't figure out a concrete type for the
                    // variable given the context.
                    var canGetDefaultValue = TryGetDefaultValueForType(expressionType, out var defaultValue);

                    // If we can't produce this, then we can't generate the
                    // declaration.
                    if (!canGetDefaultValue)
                    {
                        this.diagnostics.Add(new Diagnostic(sourceFileName, undefinedVariableContext, string.Format(CantDetermineVariableTypeError, undefinedVariableContext.VAR_ID().GetText())));
                        continue;
                    }

                    // Generate a declaration for this variable here.
                    var decl = new Declaration
                    {
                        Name = undefinedVariableContext.VAR_ID().GetText(),
                        Description = $"Implicitly declared in {System.IO.Path.GetFileName(sourceFileName)}, node {currentNodeName}",
                        Type = expressionType,
                        DefaultValue = defaultValue,
                        SourceFileName = sourceFileName,
                        SourceNodeName = currentNodeName,
                        Range = new Range
                        {
                            Start =
                            {
                                Line = undefinedVariableContext.Start.Line - 1,
                                Character = undefinedVariableContext.Start.Column,
                            },
                            End =
                            {
                                Line = undefinedVariableContext.Stop.Line - 1,
                                Character = undefinedVariableContext.Stop.Column + undefinedVariableContext.Stop.Text.Length,
                            },
                        },
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
                string message = $"All terms of {operationDescription} must be the same, not {typeList}";
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                return BuiltinTypes.Undefined;
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

                if (implementingType == null)
                {
                    string message = $"{expressionType.Name} has no implementation defined for {operationDescription}";
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                    return BuiltinTypes.Undefined;
                }
            }

            // Is this expression is required to be one of the specified types?
            if (permittedTypes.Count() > 0)
            {
                // Is the type that we've arrived at compatible with one of
                // the permitted types?
                if (permittedTypes.Any(t => TypeUtil.IsSubType(t, expressionType)))
                {
                    // It's compatible! Great, return the type we've
                    // determined.
                    return expressionType;
                }
                else
                {
                    // The expression type wasn't valid!
                    var permittedTypesList = string.Join(" or ", permittedTypes.Select(t => t?.Name ?? "undefined"));
                    var typeList = string.Join(", ", termTypes.Select(t => t.Name));

                    string message = $"Terms of '{operationDescription}' must be {permittedTypesList}, not {typeList}";
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                    return BuiltinTypes.Undefined;
                }
            }
            else
            {
                // We weren't given a specific type. The expression type is
                // therefore only valid if it can use the provided
                // operator.

                // Find a type in 'expressionType's hierarchy that
                // implements this method.
                var implementingTypeForMethod = TypeUtil.FindImplementingTypeForMethod(expressionType, operationType.ToString());

                if (implementingTypeForMethod == null)
                {
                    // The type doesn't have a method for handling this
                    // operator, and neither do any of its supertypes. This
                    // expression is therefore invalid.
                    
                    string message = $"Operator {operationDescription} cannot be used with {expressionType.Name} values";
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                    
                    return BuiltinTypes.Undefined;
                }
                else
                {
                    return expressionType;
                }
            }
        }

        private static bool TryGetDefaultValueForType(Yarn.IType expressionType, out IConvertible defaultValue)
        {
            if (expressionType == BuiltinTypes.String)
            {
                defaultValue = string.Empty;
                return true;
            }
            else if (expressionType == BuiltinTypes.Number)
            {
                defaultValue = default(float);
                return true;
            }
            else if (expressionType == BuiltinTypes.Boolean)
            {
                defaultValue = default(bool);
                return true;
            }
            else
            {
                defaultValue = null;
                return false;
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

        public override IType VisitJumpToExpression([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            // The expression's type must resolve to a string.
            return CheckOperation(context, new[] { context.expression() }, Operator.None, "jump statement", BuiltinTypes.String);
        }
    }
}
