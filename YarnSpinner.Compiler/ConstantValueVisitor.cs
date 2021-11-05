namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// A visitor that visits any valid constant value, and returns a <see
    /// cref="Value"/>. Currently only supports terminals, not expressions,
    /// even if those expressions would be constant.
    /// </summary>
    internal class ConstantValueVisitor : YarnSpinnerParserBaseVisitor<Value>
    {
        private readonly ParserRuleContext context;
        private readonly string sourceFileName;
        private readonly IEnumerable<IType> types;
        private List<Diagnostic> diagnostics;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ConstantValueVisitor"/> class.
        /// </summary>
        /// <param name="context">The parser context for this value.</param>
        /// <param name="sourceFileName">The name of the file that is being
        /// visited by this instance.</param>
        /// <param name="types">The types of values known to this instance.</param>
        public ConstantValueVisitor(ParserRuleContext context, string sourceFileName, IEnumerable<IType> types, ref List<Diagnostic> diagnostics)
        {
            this.context = context;
            this.sourceFileName = sourceFileName;
            this.types = types;
            this.diagnostics = diagnostics;
        }

        // Default result is an exception - only specific parse nodes can
        // be visited by this visitor
        protected override Value DefaultResult
        {
            get
            {
                string message = $"Expected a constant type";
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                return new Value(BuiltinTypes.Undefined, null);
            }
        }

        public override Value VisitValueNull([NotNull] YarnSpinnerParser.ValueNullContext context)
        {
            const string message = "Null is not a permitted type in Yarn Spinner 2.0 and later";
            this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
            return new Value(BuiltinTypes.Undefined, null);
        }

        public override Value VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            if (float.TryParse(context.GetText(), out var result))
            {
                return new Value(BuiltinTypes.Number, result);
            }
            else
            {
                string message = $"Failed to parse {context.GetText()} as a float";
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                return new Value(BuiltinTypes.Number, 0f);
            }
        }

        public override Value VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            return new Value(BuiltinTypes.String, context.STRING().GetText().Trim('"'));
        }

        public override Value VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return new Value(BuiltinTypes.Boolean, false);
        }

        public override Value VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return new Value(BuiltinTypes.Boolean, true);
        }

        public override Value VisitValueEnumCase([NotNull] YarnSpinnerParser.ValueEnumCaseContext context)
        {
            var enumName = context.enumCase().enumName.Text;
            var memberName = context.enumCase().memberName.Text;

            // Ensure that a type with this name exists, and that it is an Enum
            var enumType = types.OfType<EnumType>().FirstOrDefault(t => t.Name == enumName);

            if (enumType == null) {
                this.diagnostics.Add(new Diagnostic(
                    sourceFileName,
                    context,
                    $"{enumName} is not a valid enum name"));
                return new Value(BuiltinTypes.Undefined, null);
            }

            // Ensure that this enum has a member of this name
            var member = enumType.Members.FirstOrDefault(m => m.Name == memberName);

            if (member == null) {
                this.diagnostics.Add(new Diagnostic(
                    sourceFileName,
                    context,
                    $"Enum {enumName} does not have a member called {memberName}"));
                return new Value(BuiltinTypes.Undefined, null);
            }

            context.EnumType = enumType;
            context.EnumMember = member;

            return new Value(context.EnumType, member.RawValue.InternalValue);
        }
    }
}
