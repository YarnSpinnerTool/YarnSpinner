// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

#define DISALLOW_NULL_EQUATION_TERMS

using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using TypeChecker;

namespace Yarn.Compiler
{
    internal class TypeCheckerListener : YarnSpinnerParserBaseListener
    {
        private const string NodeHeaderTitle = "title";
        public List<TypeConstraint> TypeEquations = new List<TypeConstraint>();

        private CommonTokenStream tokens;
        private IParseTree tree;
        private List<Declaration> knownDeclarations;
        private int typeParameterCount = 0;

        private string sourceFileName = "<not set>";
        private string currentNodeName = null;

        /// <summary>
        /// Gets the collection of file-level hashtags that were found as a
        /// result of using this <see cref="TypeCheckerListener"/> to visit a
        /// <see cref="ParserRuleContext"/>.
        /// </summary>
        public ICollection<string> FileTags { get; } = new List<string>();

        // Maps the names of types as they appear in the language (string, bool,
        // number) to actual type objects.
        //
        // TODO: maybe the 'real' type names should be lowercased too? Rather
        // than relying on this kind of mapping?
        private static Dictionary<string, IType> LanguageTypeNames = new Dictionary<string, IType> {
            { "string", Types.String },
            { "number", Types.Number },
            { "bool", Types.Boolean },
        };

        public TypeCheckerListener(string sourceFileName, CommonTokenStream tokens, IParseTree tree, ref List<Declaration> knownDeclarations)
        {
            this.sourceFileName = sourceFileName;
            this.tokens = tokens;
            this.tree = tree;
            this.knownDeclarations = knownDeclarations;
        }

        private Declaration GetKnownDeclaration(string name) => this.knownDeclarations.FirstOrDefault(d => d.Name == name);

        private void AddDiagnostic(ParserRuleContext context, string message, Diagnostic.DiagnosticSeverity severity = Diagnostic.DiagnosticSeverity.Error)
        {
            this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message, severity));
        }

        private TypeEqualityConstraint AddEqualityConstraint(IType a, IType b, ParserRuleContext context, FailureMessageProvider failureMessageProvider)
        {
#if DISALLOW_NULL_EQUATION_TERMS
            if (a == null)
            {
                throw new ArgumentNullException($"{nameof(a)}");
            }

            if (b == null)
            {
                throw new ArgumentNullException($"{nameof(b)}");
            }
#endif

            TypeEqualityConstraint item = new TypeEqualityConstraint(a ?? Types.Error, b ?? Types.Error);
            item.SourceFileName = sourceFileName;
            item.SourceRange = GetRange(context);
            item.FailureMessageProvider = failureMessageProvider;

            this.TypeEquations.Add(item);
            return item;
        }

        private TypeConvertibleConstraint AddConvertibleConstraint(IType from, IType to, ParserRuleContext context, FailureMessageProvider failureMessageProvider)
        {
#if DISALLOW_NULL_EQUATION_TERMS
            if (from == null)
            {
                throw new ArgumentNullException($"{nameof(from)}");
            }

            if (to == null)
            {
                throw new ArgumentNullException($"{nameof(to)}");
            }
#endif

            TypeConvertibleConstraint item = new TypeConvertibleConstraint(from ?? Types.Error, to ?? Types.Error);
            item.SourceFileName = sourceFileName;
            item.SourceRange = GetRange(context);
            item.FailureMessageProvider = failureMessageProvider;

            this.TypeEquations.Add(item);
            return item;
        }

        private void AddHasEnumMemberConstraint(IType type, string memberName, ParserRuleContext context, FailureMessageProvider failureMessageProvider)
        {
#if DISALLOW_NULL_EQUATION_TERMS
            if (type == null)
            {
                throw new ArgumentNullException($"{nameof(type)}");
            }
#endif
            TypeHasMemberConstraint item = new TypeHasMemberConstraint(type, memberName);
            item.SourceFileName = sourceFileName;
            item.SourceRange = GetRange(context);
            item.FailureMessageProvider = failureMessageProvider;

            this.TypeEquations.Add(item);
        }

        private void AddHasNameConstraint(IType type, string name, ParserRuleContext context, FailureMessageProvider failureMessageProvider)
        {
#if DISALLOW_NULL_EQUATION_TERMS
            if (type == null)
            {
                throw new ArgumentNullException($"{nameof(type)}");
            }
#endif

            TypeHasNameConstraint item = new TypeHasNameConstraint(type, name);
            item.SourceFileName = sourceFileName;
            item.SourceRange = GetRange(context);
            item.FailureMessageProvider = failureMessageProvider;

            this.TypeEquations.Add(item);
        }

        private TypeVariable GenerateTypeVariable(string name = null)
        {
            string variableName;
            if (name != null)
            {
                variableName = "T(" + name + ")";
            }
            else
            {
                variableName = "T" + this.typeParameterCount++;
            }

            return new TypeVariable(variableName);
        }

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        private List<Diagnostic> diagnostics = new List<Diagnostic>();

        public override void ExitFile_hashtag([NotNull] YarnSpinnerParser.File_hashtagContext context)
        {
            this.FileTags.Add(context.text.Text);
        }

        public override void ExitHeader([NotNull] YarnSpinnerParser.HeaderContext context)
        {
            // We don't do any type checking here, but we do want to know if
            // this header was the 'title' header, because we want to know the
            // name of the current node we're in for diagnostic purposes.
            if (context.header_key.Text == NodeHeaderTitle)
            {
                this.currentNodeName = context.header_value.Text;
            }
        }

        public override void ExitDeclare_statement([NotNull] YarnSpinnerParser.Declare_statementContext context)
        {
            YarnSpinnerParser.VariableContext variableContext = context.variable();
            var name = variableContext?.VAR_ID()?.GetText();
            Declaration declaration = this.GetKnownDeclaration(name);

            // Do we already have a explicit declaration for a variable with
            // this name? It's an error if we do.
            if (declaration != null && declaration.IsImplicit == false)
            {
                this.AddDiagnostic(context, $"Redeclaration of existing variable {name}");
                return;
            }

            // Figure out the type of the declaration; we'll determine its initial value later
            var constantValueVisitor = new LiteralValueVisitor(context, name, ref this.diagnostics);
            
            var typeIdentifier = this.GenerateTypeVariable(name);

            // The type of this identifier is equal to the type of its default value.
            this.AddEqualityConstraint(typeIdentifier, context.value().Type, context, s => $"The type of {name}'s initial value \"{context.value().GetText()}\" ({context.value().Type.Substitute(s)}) doesn't match the type of the variable {typeIdentifier.Substitute(s)}.");

            if (context.type != null)
            {
                // We were given an explicit type name. Add the further
                // constraint that whatever type we have has this name.
                string typeName = context.type.Text;

                if (LanguageTypeNames.TryGetValue(typeName, out var type)) {
                    // Constrain the type of this variable to the named type.
                    this.AddEqualityConstraint(typeIdentifier, type, context, s => $"{name}'s type ({typeIdentifier.Substitute(s)}) must be {type.Substitute(s)}");
                } else {
                    // We don't have a built-in mapping of this name to a type.
                    // Add a constraint such that, whatever the type this
                    // variable is, the type's name is equal to what's specified
                    // here.
                    this.AddHasNameConstraint(typeIdentifier, typeName, context, s => $"{name}'s type must be {typeName}");
                }
            }

            string description = Compiler.GetDocumentComments(this.tokens, context);

            if (declaration != null && declaration.IsImplicit)
            {
                // We're replacing an implicit declaration with an explicit one.
                // Before we replace it, we need to make sure that the type
                // variable (which other constraints might rely on!) is
                // connected to the type of the new declaration.
                this.AddEqualityConstraint(declaration.Type, typeIdentifier, context, s => $"{name} was declared to be a {typeIdentifier.Substitute(s)}, but it was used elsewhere as a {declaration.Type.Substitute(s)}");

                // Remove the implicit declaration from the known declarations
                // in preparation for adding the new one.
                this.knownDeclarations.Remove(declaration);
            }

            declaration = new Declaration
            {
                Name = name,
                Type = typeIdentifier,
                Description = description,
                DefaultValue = value.InternalValue,
                SourceFileName = this.sourceFileName,
                SourceNodeName = this.currentNodeName,
                Range = GetRange(variableContext),
                IsImplicit = false,
            };

            this.knownDeclarations.Add(declaration);
        }

        private static Range GetRange(ParserRuleContext context)
        {
            return new Range
            {
                Start =
                    {
                        Line = context.Start.Line - 1,
                        Character = context.Start.Column,
                    },
                End =
                    {
                        Line = context.Stop.Line - 1,
                        Character = context.Stop.Column + context.GetText().Length,
                    },
            };
        }

        public override void ExitValueTrue([NotNull] YarnSpinnerParser.ValueTrueContext context)
        {
            context.Type = Types.Boolean;
        }

        public override void ExitValueFalse([NotNull] YarnSpinnerParser.ValueFalseContext context)
        {
            context.Type = Types.Boolean;
        }

        public override void ExitValueNumber([NotNull] YarnSpinnerParser.ValueNumberContext context)
        {
            context.Type = Types.Number;
        }

        public override void ExitValueString([NotNull] YarnSpinnerParser.ValueStringContext context)
        {
            context.Type = Types.String;
        }

        public override void ExitValueVar([NotNull] YarnSpinnerParser.ValueVarContext context)
        {
            context.Type = context.variable().Type;
        }

        public override void ExitVariable([NotNull] YarnSpinnerParser.VariableContext context)
        {
            var variableID = context.VAR_ID();

            if (variableID == null)
            {
                // This is a parse error, so the type of this node is the error
                // type.
                context.Type = Types.Error;
                return;
            }

            string name = variableID.GetText();
            var declaration = this.GetKnownDeclaration(name);

            if (declaration == null)
            {
                var typeVariable = this.GenerateTypeVariable(name);
                declaration = new Declaration
                {
                    Name = name,
                    Type = typeVariable,
                    Description = $"Implicitly declared in {this.sourceFileName}, node {this.currentNodeName}",
                    Range = GetRange(context),
                    IsImplicit = true,
                    SourceFileName = this.sourceFileName,
                    SourceNodeName = this.currentNodeName,
                };
                this.knownDeclarations.Add(declaration);
            }

            context.Type = declaration.Type;
        }

        public override void ExitExpParens([NotNull] YarnSpinnerParser.ExpParensContext context)
        {
            // The type of a parentheses expression is equal to the type of its
            // child.
            context.Type = context.expression()?.Type ?? Types.Error;
        }

        public override void ExitExpAddSub([NotNull] YarnSpinnerParser.ExpAddSubContext context)
        {
            var type = this.GenerateTypeVariable();
            context.Type = type;

            IType operandAType = context.expression(0)?.Type ?? Types.Error;
            IType operandBType = context.expression(1)?.Type ?? Types.Error;

            string op = context.op.Text;

            this.AddEqualityConstraint(type, operandAType, context, s => $"Operation '{op}' can't be used with a value of type {operandAType.Substitute(s)}");
            this.AddEqualityConstraint(operandAType, operandBType, context, s => $"Operation '{op}'s values must both be the same type, not {operandAType.Substitute(s)} and {operandBType.Substitute(s)}");
        }

        public override void ExitExpMultDivMod([NotNull] YarnSpinnerParser.ExpMultDivModContext context)
        {
            var type = this.GenerateTypeVariable();
            context.Type = type;

            IType operandAType = context.expression(0)?.Type ?? Types.Error;
            IType operandBType = context.expression(1)?.Type ?? Types.Error;

            string op = context.op.Text;

            this.AddEqualityConstraint(type, operandAType, context, s => $"Operation '{op}' can't be used with a value of type {operandAType.Substitute(s)}");
            this.AddEqualityConstraint(operandAType, operandBType, context, s => $"Operation '{op}'s values must both be the same type, not {operandAType.Substitute(s)} and {operandBType.Substitute(s)}");
        }

        public override void ExitExpComparison([NotNull] YarnSpinnerParser.ExpComparisonContext context)
        {
            // The result of a comparison is boolean; the types of the
            // expressions must be identical.
            context.Type = Types.Boolean;

            IType operandAType = context.expression(0)?.Type ?? Types.Error;
            IType operandBType = context.expression(1)?.Type ?? Types.Error;

            string op = context.op.Text;

            this.AddEqualityConstraint(operandAType, operandBType, context, s => $"Operation '{op}'s values must both be the same type, not {operandAType.Substitute(s)} and {operandBType.Substitute(s)}");
        }

        public override void ExitExpEquality([NotNull] YarnSpinnerParser.ExpEqualityContext context)
        {
            // The result of an equality is boolean; the types of the
            // expressions must be identical.
            context.Type = Types.Boolean;
            
            IType operandAType = context.expression(0)?.Type ?? Types.Error;
            IType operandBType = context.expression(1)?.Type ?? Types.Error;

            string op = context.op.Text;

            this.AddEqualityConstraint(operandAType, operandBType, context, s => $"Operation '{op}'s values must both be the same type, not {operandAType.Substitute(s)} and {operandBType.Substitute(s)}");
        }

        public override void ExitExpAndOrXor([NotNull] YarnSpinnerParser.ExpAndOrXorContext context)
        {
            // The result of a logical and, or, or xor is boolean; the types of
            // the expressions must also be boolean.
            var exp0Type = context.expression(0)?.Type;
            var exp1Type = context.expression(1)?.Type;
            if (exp0Type == null || exp1Type == null) {
                context.Type = Types.Error;
                return;
            }

            context.Type = Types.Boolean;
            IType type0 = context.expression(0).Type;
            IType type1 = context.expression(1).Type;

            this.AddEqualityConstraint(type0, Types.Boolean, context, s => $"{context.op.Text} operands must be {Types.Boolean}, not {type0.Substitute(s)}");
            this.AddEqualityConstraint(type0, type1, context, s => $"{context.op.Text} operands must be the same type, not {type0} and {type1}");
        }

        public override void ExitExpNot([NotNull] YarnSpinnerParser.ExpNotContext context)
        {
            // The result of a logical not is boolean; the type of the operand
            // must also be boolean.
            context.Type = Types.Boolean;
            
            IType type = context.expression()?.Type ?? Types.Error;

            this.AddEqualityConstraint(type, Types.Boolean, context,  s => $"{context.op.Text} operand must be {Types.Boolean}, not {type.Substitute(s)}");
        }

        public override void ExitExpNegative([NotNull] YarnSpinnerParser.ExpNegativeContext context)
        {
            // The result of a negation is a number; the type of the operand
            // must also be a number.
            context.Type = Types.Number;
            IType type = context.expression()?.Type ?? Types.Error;
            this.AddEqualityConstraint(type, Types.Number, context, s => $"{context.op.Text} operand must be {Types.Boolean}, not {type.Substitute(s)}");
        }

        public override void ExitExpValue([NotNull] YarnSpinnerParser.ExpValueContext context)
        {
            // An expression containing a value has the same type as the value
            // it contains.
            context.Type = context.value()?.Type;
        }

        public override void ExitIf_clause([NotNull] YarnSpinnerParser.If_clauseContext context)
        {
            // The condition for an if statement must be a boolean
            AddEqualityConstraint(context.expression().Type, Types.Boolean, context.expression(), s => $"if statement's expression must be a {Types.Boolean}, not a {context.expression().Type.Substitute(s)}");
            base.ExitIf_clause(context);
        }

        public override void ExitElse_if_clause([NotNull] YarnSpinnerParser.Else_if_clauseContext context)
        {
            // The condition for an elseif statement must be a boolean
            AddEqualityConstraint(context.expression().Type, Types.Boolean, context.expression(), s => $"else if statement's expression must be a {Types.Boolean}, not a {context.expression().Type.Substitute(s)}");
            base.ExitElse_if_clause(context);
        }

        public override void ExitSet_statement([NotNull] YarnSpinnerParser.Set_statementContext context)
        {
            // The type of the expression must be convertible to the type of the variable
            IType variableType = context.variable().Type;
            IType expressionType = context.expression().Type;
            string variableName = context.variable().GetText();

            this.AddConvertibleConstraint(context.expression().Type, context.variable().Type, context, s => $"{variableName} ({variableType.Substitute(s)}) cannot be assigned a {expressionType.Substitute(s)}");
            base.ExitSet_statement(context);
        }

        public override void ExitValueFunc([NotNull] YarnSpinnerParser.ValueFuncContext context)
        {
            // ValueFunc is just a wrapper around a function call, so it just
            // uses the same type variable
            context.Type = context.function_call().Type;
            base.ExitValueFunc(context);
        }

        public override void ExitFunction_call([NotNull] YarnSpinnerParser.Function_callContext context)
        {
            // If we already have a declaration for this function, then use information from that
            string functionName = context.FUNC_ID().GetText();
            var functionDecl = this.GetKnownDeclaration(functionName);

            FunctionType functionType;

            if (functionDecl == null) {
                // We don't know about this function. We'll need to create a new declaration.

                TypeVariable returnType = this.GenerateTypeVariable($"return from {functionName}");

                functionType = new FunctionType(returnType);

                int count = 1;
                foreach (var expression in context.expression()) {
                    var parameterType = GenerateTypeVariable($"{context.FUNC_ID()} param {count}");
                    functionType.AddParameter(parameterType);

                    count++;
                }

                functionDecl = new Declaration
                {
                    Name = functionName,
                    IsImplicit = true,
                    SourceFileName = sourceFileName,
                    Range = GetRange(context),
                    Type = functionType,
                };

                this.knownDeclarations.Add(functionDecl);
            } else {
                functionType = (FunctionType)functionDecl.Type;
            }

            context.Type = GenerateTypeVariable();

            int actualParameters = context.expression().Count();
            int expectedParameters = functionType.Parameters.Count();

            // Check to see if we have the expected number of parameters
            if (actualParameters != expectedParameters) {
                // We don't! Create a diagnostic message and make this
                // expression be the Error type.

                string message;

                var expectedEnglishPlural = expectedParameters != 1;
                var actualEnglishPlural = actualParameters != 1;

                // If the function declaration is implicit, give a message here
                // that hedges a bit - we don't know if _this_ call is the
                // incorrect one.
                if (functionDecl.IsImplicit) {
                    message = $"{functionName} was called elsewhere with {expectedParameters} {(expectedEnglishPlural ? "parameters" : "parameter")}, but is called with {actualParameters} {(actualEnglishPlural ? "parameters" : "parameter")} here";
                } else {
                    message = $"{functionName} expects {expectedParameters} {(expectedEnglishPlural ? "parameters" : "parameter")}, not {actualParameters}";
                }

                diagnostics.Add(new Diagnostic(sourceFileName, context, message));
                context.Type = Types.Error;
                return;
            }

            for (int paramID = 0; paramID < expectedParameters; paramID ++)
            {
                var expectedType = functionType.Parameters[paramID];
                var parameterExpression = context.expression()[paramID];
                var actualType = parameterExpression.Type;

                AddConvertibleConstraint(actualType, expectedType, parameterExpression, s => $"{parameterExpression.GetText()} ({parameterExpression.Type.Substitute(s)}) is not convertible to {expectedType.Substitute(s)}");
            }

            // The type of this function call is the return type of the function
            AddEqualityConstraint(context.Type, functionType.ReturnType, context, s => $"Call to {functionDecl.Name} returns {functionType.ReturnType}, not {context.Type}");

            base.ExitFunction_call(context);
        }

        public override void ExitLine_condition([NotNull] YarnSpinnerParser.Line_conditionContext context) {
            if (context.expression() != null) {
                AddEqualityConstraint(context.expression().Type, Types.Boolean, context, s => $"line condition's expression must be a {Types.Boolean}, not a {context.expression().Type.Substitute(s)}");
            }
            base.ExitLine_condition(context);
        }

        public override void ExitJumpToExpression([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            if (context.expression() != null) {
                // The type of the expression must resolve to a string.
                AddEqualityConstraint(context.expression()?.Type,
                    Types.String,
                    context,
                    s => $"jump statement's expression must be a {Types.String}, not a {context.expression().Type.Substitute(s)}");
            }

            base.ExitJumpToExpression(context);
        }

        /*
        MERGED FROM features/enums

        public override void ExitEnum_statement([NotNull] YarnSpinnerParser.Enum_statementContext context)
        {
            // We've just finished walking an enum statement! We're almost
            // ready to add its declaration.

            // First: are there any types with the same name as this?
            if (this.typeDeclarations.Any(t => t.Name == context.name.Text))
            {
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, $"Cannot declare new enum {context.name.Text}: a type with this name already exists"));
                return;
            }

            // Get its description, if any
            var description = Compiler.GetDocumentComments(this.tokens, context, false);

            // Create the new type.
            var enumType = new EnumType(context.name.Text, description);

            // What is the type of this enum's raw values?
            var permittedRawValueTypes = new[]
            {
                BuiltinTypes.Number,
                BuiltinTypes.String,
            };

            // The type of the raw values this enum is using.
            IType typeOfRawValues = null;

            foreach (var caseStatement in context.enum_case_statement())
            {
                if (caseStatement.rawValue == null)
                {
                    // No raw value in this case statement.
                    caseStatement.RawValue = null;
                }
                else
                {
                    // This case statement has a raw value. Parse it.
                    Value value = new LiteralValueVisitor(context, this.sourceFileName, ref this.diagnostics).Visit(caseStatement.rawValue);

                    caseStatement.RawValue = value;

                    if (typeOfRawValues == null)
                    {
                        // This is the first raw value we've seen; set the
                        // raw type of the enum to this type.
                        typeOfRawValues = value.Type;
                    }
                    else if (TypeUtil.IsSubType(typeOfRawValues, value.Type) == false)
                    {
                        // We already had a raw type, and this case
                        // statement uses an incompatible type. Report an error.
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, caseStatement, $"Enum member raw values may only be of a single type (they can't be {typeOfRawValues.Name} and {value.Type.Name})"));
                        return;
                    }

                    // Report an error if this value isn't an allowable type.
                    if (permittedRawValueTypes.Contains(value.Type) == false)
                    {
                        this.diagnostics.Add(new Diagnostic(
                            this.sourceFileName,
                            caseStatement,
                            $"Invalid type: enum raw values cannot be {value.Type?.Name ?? "undefined"} (they must be of type {string.Join(" or ", permittedRawValueTypes.Select(t => t.Name))})"));

                        return;
                    }
                }
            }

            if (typeOfRawValues == null)
            {
                // We never saw a raw value, so default it to number.
                typeOfRawValues = BuiltinTypes.Number;
            }

            // If typeOfRawValues is BuiltinTypes.Number, we will use this
            // value to automatically assign a number to each successive
            // one.
            int numberIncrement = 0;

            // The hash codes of the raw values we've assigned
            var rawValueHashes = new HashSet<int>();

            // Now walk through the list of case statements, generating
            // EnumMembers for each one.
            for (int i = 0; i < context.enum_case_statement().Length; i++)
            {
                var @case = context.enum_case_statement(i);

                // Report an error if we have a duplicate member
                if (enumType.Members.Any(existingMember => existingMember.Name == @case.name.Text))
                {
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, @case, $"Enum {enumType.Name} already has a case called {@case.name.Text}"));
                    return;
                }

                // Get the documentation comments for this case, if any
                var caseDescription = Compiler.GetDocumentComments(this.tokens, @case);

                // Does this case statement have a raw value?
                Value rawValue;

                if (typeOfRawValues == BuiltinTypes.Number)
                {
                    if (@case.RawValue != null)
                    {
                        rawValue = @case.RawValue;

                        // Start incrementing from this point
                        numberIncrement = @case.RawValue.ConvertTo<int>();
                    }
                    else
                    {
                        rawValue = new Value(BuiltinTypes.Number, numberIncrement);
                    }

                    numberIncrement += 1;
                }
                else if (typeOfRawValues == BuiltinTypes.String)
                {
                    if (@case.RawValue != null)
                    {
                        rawValue = @case.RawValue;
                    }
                    else
                    {
                        // We don't have a default we can use!
                        this.diagnostics.Add(new Diagnostic(this.sourceFileName, @case, "All enum cases must have a value, if strings are used"));
                        return;
                    }
                }
                else
                {
                    this.diagnostics.Add(new Diagnostic(this.sourceFileName, @case, $"Internal error: invalid enum case raw value type {typeOfRawValues.Name}"));
                    return;
                }

                // Check to see if we've assigned this raw value already
                var hash = rawValue.InternalValue.GetHashCode();

                if (rawValueHashes.Contains(hash))
                {
                    // They're not allowed to be the same!
                    this.diagnostics.Add(new Diagnostic(
                        this.sourceFileName,
                        @case,
                        $"Enum member raw values must be unique"
                    ));
                    return;
                }

                rawValueHashes.Add(hash);

                var member = new EnumMember
                {
                    Name = @case.name.Text,
                    RawValue = rawValue,
                    Description = caseDescription,
                };

                enumType.AddMember(member);
            }

            this.typeDeclarations.Add(enumType);
        }
        */
    }

    public interface ITypedContext {
        IType Type { get; set; }
    }

    public partial class YarnSpinnerParser
    {
        public partial class ExpressionContext : ITypedContext
        {
            /// <summary>
            /// Gets or sets the type that this expression has been
            /// determined to be by a <see cref="TypeCheckVisitor"/>
            /// object.
            /// </summary>
            public IType Type { get; set; }
        }

        public partial class ValueContext : ITypedContext
        {
            public IType Type { get; set; }
        }

        public partial class VariableContext : ITypedContext
        {
            public IType Type { get; set; }
        }

        public partial class Function_callContext : ITypedContext
        {
            public IType Type { get; set; }
        }

        public partial class Enum_case_statementContext : ParserRuleContext {
            /// <summary>
            /// Gets or sets the 'raw value' of this enum case, which is
            /// the underlying value that this enum represents.
            /// </summary>
            internal Yarn.Value RawValue { get; set; }
        }

        public partial class ValueEnumCaseContext : ValueContext
        {
            /// <summary>
            /// Gets or sets the enum type object that this value context
            /// refers to.
            /// </summary>
            /// <value></value>
            internal Yarn.EnumType EnumType { get; set; }

            /// <summary>
            /// Gets or sets the enum member that this value context
            /// represents.
            /// </summary>
            /// <value></value>
            internal Yarn.EnumMember EnumMember { get; set; }
        }
    }
}
