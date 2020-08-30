namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;

    internal class DeclarationVisitor : YarnSpinnerParserBaseVisitor<int>
    {

        // The collection of variable declarations we know about before
        // starting our work
        private IEnumerable<Declaration> ExistingVariableDeclarations;

        /// <summary>
        /// The collection of new variable declarations that were found as
        /// a result of using this <see cref="DeclarationVisitor"/>
        /// to visit a <see cref="ParserRuleContext"/>.
        /// </summary>
        public ICollection<Declaration> NewDeclarations { get; private set; }

        private IEnumerable<Declaration> AllDeclarations
        {
            get
            {
                foreach (var decl in existingDeclarations)
                {
                    yield return decl;
                }
                foreach (var decl in NewDeclarations)
                {
                    yield return decl;
                }
            }
        }

        public DeclarationVisitor(IEnumerable<Declaration> existingDeclarations)
        {
            this.existingDeclarations = existingDeclarations;
            this.NewDeclarations = new List<Declaration>();
        }

        public override int VisitNode(YarnSpinnerParser.NodeContext context) {
            currentNodeContext = context;

            foreach (var header in context.header()) {
                if (header.header_key.Text == "title") {
                    currentNodeName = header.header_value.Text;
                }
            }
            Visit(context.body());
            return 0;
        }

        public override int VisitDeclare_statement(YarnSpinnerParser.Declare_statementContext context)
        {

            // Get the name of the variable we're declaring
            string variableName = context.variable().GetText();

            // Does this variable name already exist in our declarations?
            foreach (var decl in AllDeclarations)
            {
                if (decl.Name == variableName)
                {
                    throw new TypeException($"{decl.Name} has already been declared");
                }
            }

            // Figure out the type of the value
            var expressionVisitor = new ExpressionTypeVisitor(null, true);
            var type = expressionVisitor.Visit(context.value());

            // Figure out the value itself
            var constantValueVisitor = new ConstantValueVisitor();
            var value = constantValueVisitor.Visit(context.value());

            // Do we have an explicit type declaration?
            if (context.type() != null)
            {
                Yarn.Type explicitType;

                // Get its type
                switch (context.type().typename.Type)
                {
                    case YarnSpinnerLexer.TYPE_STRING:
                        explicitType = Yarn.Type.String;
                        break;
                    case YarnSpinnerLexer.TYPE_BOOL:
                        explicitType = Yarn.Type.Bool;
                        break;
                    case YarnSpinnerLexer.TYPE_NUMBER:
                        explicitType = Yarn.Type.Number;
                        break;
                    default:
                        throw new ParseException(context, $"Unknown type {context.type().GetText()}");
                }

                // Check that it matches - if it doesn't, that's a type
                // error
                if (explicitType != type)
                {
                    throw new TypeException(context, $"Type {context.type().GetText()} does not match value {context.value().GetText()} ({type})");
                }
            }

            // Get the variable declaration, if we have one
            string description = null;

            if (context.Description != null)
            {
                description = context.Description.Text.Trim('"');
            }

            // We're done creating the declaration!
            var declaration = new Declaration
            {
                Name = variableName,
                ReturnType = type,
                DefaultValue = value,
                Description = description,
                DeclarationType = Declaration.Type.Variable,
            };

            this.NewDeclarations.Add(declaration);

            return 0;
        }

        public override int VisitFunction(YarnSpinnerParser.FunctionContext context) {

            // We've encountered a function call. We need to check to see
            // if this is one that's already known, or if we need to create
            // an implicit declaration for it.

            var functionName = context.FUNC_ID().GetText();

            // Visit this function invocation's parameters in case one of
            // them is an invocation of a function we don't have a
            // declaration for.
            foreach (var param in context.expression()) {
                Visit(param);
            }

            foreach (var decl in this.AllDeclarations) {
                if (decl.DeclarationType == Declaration.Type.Function && decl.Name == functionName) {
                    // We already have a declaration. Nothing left to do here.
                    return 0;
                }
            }

            // We don't have an existing declaration for this function.
            // Create an implicit declaration here, and note that we don't
            // know its return type or its parameters type (the expression
            // that invokes us will attempt to determine the types from
            // context, and bind the return and parameter types to those
            // determinations. If such a determination can't be made, or it
            // conflicts with a previously bound type, a type error
            // occurs.)
            var parameterList = new List<Declaration.Parameter>();
            foreach (var parameter in context.expression())
            {
                parameterList.Add(new Declaration.Parameter
                {
                    type = Yarn.Type.Undefined,
                });
            }

            var declaration = new Declaration {
                DeclarationType = Declaration.Type.Function,
                Name = functionName,
                ReturnType = Yarn.Type.Undefined,
                Parameters = parameterList.ToArray(),                
            };

            this.NewDeclarations.Add(declaration);

            return 0;
        }
    }
}
