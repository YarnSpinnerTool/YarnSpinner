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

        private string name;
        private CommonTokenStream tokens;
        private IParseTree tree;

        private List<Declaration> knownDeclarations;
        private int typeParameterCount = 0;

        private string sourceFileName = "<not set>";
        private string currentNodeName = null;

        public TypeCheckerListener(string name, CommonTokenStream tokens, IParseTree tree, ref List<Declaration> knownDeclarations)
        {
            this.name = name;
            this.tokens = tokens;
            this.tree = tree;

            this.knownDeclarations = new List<Declaration>(knownDeclarations);
        }

        private Declaration GetKnownDeclaration(string name) => this.knownDeclarations.FirstOrDefault(d => d.Name == name);

        private void AddDiagnostic(ParserRuleContext context, string message, Diagnostic.DiagnosticSeverity severity = Diagnostic.DiagnosticSeverity.Error)
        {
            this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message, severity));
        }

        private TypeEqualityConstraint AddEqualityConstraint(IType a, IType b)
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

            TypeEqualityConstraint item = new TypeEqualityConstraint(a ?? BuiltinTypes.Error, b ?? BuiltinTypes.Error);
            this.TypeEquations.Add(item);
            return item;
        }

        private TypeConvertibleConstraint AddConvertibleConstraint(IType from, IType to)
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

            TypeConvertibleConstraint item = new TypeConvertibleConstraint(from ?? BuiltinTypes.Error, to ?? BuiltinTypes.Error);
            this.TypeEquations.Add(item);
            return item;
        }

        private void AddHasEnumMemberConstraint(IType type, string memberName)
        {
#if DISALLOW_NULL_EQUATION_TERMS
            if (type == null)
            {
                throw new ArgumentNullException($"{nameof(type)}");
            }
#endif
            TypeHasMemberConstraint item = new TypeHasMemberConstraint(type, memberName);
            this.TypeEquations.Add(item);
        }

        private void AddHasNameConstraint(IType type, string name)
        {
#if DISALLOW_NULL_EQUATION_TERMS
            if (type == null)
            {
                throw new ArgumentNullException($"{nameof(type)}");
            }
#endif

            TypeHasNameConstraint item = new TypeHasNameConstraint(type, name);
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

        private List<Diagnostic> diagnostics;

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

            if (declaration != null && declaration.IsImplicit == false)
            {
                this.AddDiagnostic(context, $"Redeclaration of existing variable {name}");
                return;
            }

            // Figure out the value and its type
            var constantValueVisitor = new ConstantValueVisitor(context, name, this.knownDeclarations, ref this.diagnostics);
            var value = constantValueVisitor.Visit(context.value());

            var typeIdentifier = this.GenerateTypeVariable(name);

            // The type of this identifier is equal to the type of its default value.
            this.AddEqualityConstraint(typeIdentifier, value.Type);

            if (context.type != null)
            {
                // We were given an explicit type name. Add the further
                // constraint that whatever type we have has this name.
                this.AddHasNameConstraint(typeIdentifier, context.type.Text);
            }

            string description = Compiler.GetDocumentComments(this.tokens, context);

            if (declaration != null && declaration.IsImplicit)
            {
                // We're replacing an implicit declaration with an explicit one.
                // Before we replace it, we need to make sure that the type
                // variable (which other constraints might rely on!) is
                // connected to the type of the new declaration.
                this.AddEqualityConstraint(declaration.Type, typeIdentifier);

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
            context.Type = BuiltinTypes.Boolean;
        }

        public override void ExitValueFalse([NotNull] YarnSpinnerParser.ValueFalseContext context)
        {
            context.Type = BuiltinTypes.Boolean;
        }

        public override void ExitValueNumber([NotNull] YarnSpinnerParser.ValueNumberContext context)
        {
            context.Type = BuiltinTypes.Number;
        }

        public override void ExitValueString([NotNull] YarnSpinnerParser.ValueStringContext context)
        {
            context.Type = BuiltinTypes.String;
        }

        public override void ExitVariable([NotNull] YarnSpinnerParser.VariableContext context)
        {
            var variableID = context.VAR_ID();

            if (variableID == null)
            {
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
            context.Type = context.expression()?.Type;
        }

        public override void ExitExpAddSub([NotNull] YarnSpinnerParser.ExpAddSubContext context)
        {
            var type = this.GenerateTypeVariable();
            context.Type = type;
            this.AddEqualityConstraint(type, context.expression(0)?.Type);
            this.AddEqualityConstraint(context.expression(0)?.Type, context.expression(1)?.Type);
        }

        public override void ExitExpMultDivMod([NotNull] YarnSpinnerParser.ExpMultDivModContext context)
        {
            var type = this.GenerateTypeVariable();
            context.Type = type;
            this.AddEqualityConstraint(type, context.expression(0)?.Type);
            this.AddEqualityConstraint(context.expression(0)?.Type, context.expression(1)?.Type);
        }

        public override void ExitExpComparison([NotNull] YarnSpinnerParser.ExpComparisonContext context)
        {
            // The result of a comparison is boolean; the types of the
            // expressions must be identical.
            context.Type = BuiltinTypes.Boolean;
            this.AddEqualityConstraint(context.expression(0)?.Type, context.expression(1)?.Type);
        }

        public override void ExitExpEquality([NotNull] YarnSpinnerParser.ExpEqualityContext context)
        {
            // The result of an equality is boolean; the types of the
            // expressions must be identical.
            context.Type = BuiltinTypes.Boolean;
            this.AddEqualityConstraint(context.expression(0)?.Type, context.expression(1)?.Type);
        }

        public override void ExitExpAndOrXor([NotNull] YarnSpinnerParser.ExpAndOrXorContext context)
        {
            // The result of a logical and, or, or xor is boolean; the types of
            // the expressions must also be boolean.
            context.Type = BuiltinTypes.Boolean;
            this.AddEqualityConstraint(context.expression(0)?.Type, BuiltinTypes.Boolean);
            this.AddEqualityConstraint(context.expression(0)?.Type, context.expression(1)?.Type);
        }

        public override void ExitExpNot([NotNull] YarnSpinnerParser.ExpNotContext context)
        {
            // The result of a logical not is boolean; the type of the operand
            // must also be boolean.
            context.Type = BuiltinTypes.Boolean;
            this.AddEqualityConstraint(context.expression()?.Type, BuiltinTypes.Boolean);
        }

        public override void ExitExpNegative([NotNull] YarnSpinnerParser.ExpNegativeContext context)
        {
            // The result of a negation is a number; the type of the operand
            // must also be a number.
            context.Type = BuiltinTypes.Number;
            this.AddEqualityConstraint(context.expression()?.Type, BuiltinTypes.Number);
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
            AddEqualityConstraint(context.expression().Type, BuiltinTypes.Boolean);
        }

        public override void ExitElse_if_clause([NotNull] YarnSpinnerParser.Else_if_clauseContext context)
        {
            // The condition for an elseif statement must be a boolean
            AddEqualityConstraint(context.expression().Type, BuiltinTypes.Boolean);
            base.ExitElse_if_clause(context);
        }

        // TODO: functions seem like the next obvious step, then set statements
    }

    public partial class YarnSpinnerParser
    {
        public partial class ExpressionContext
        {
            public IType Type { get; set; }
        }

        public partial class ValueContext
        {
            public IType Type { get; set; }
        }

        public partial class VariableContext
        {
            public IType Type { get; set; }
        }

        public partial class Function_callContext
        {
            public IType Type { get; set; }
        }
    }
}
