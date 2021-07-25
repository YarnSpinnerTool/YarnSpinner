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

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ConstantValueVisitor"/> class.
        /// </summary>
        /// <param name="context">The parser context for this value.</param>
        /// <param name="sourceFileName">The name of the file that is being
        /// visited by this instance.</param>
        /// <param name="types">The types of values known to this instance.</param>
        public ConstantValueVisitor(ParserRuleContext context, string sourceFileName, IEnumerable<IType> types)
        {
            this.context = context;
            this.sourceFileName = sourceFileName;
            this.types = types;
        }

        // Default result is an exception - only specific parse nodes can
        // be visited by this visitor
        protected override Value DefaultResult
        {
            get
            {
                throw new TypeException(context, $"Expected a constant type", sourceFileName);
            }
        }

        public override Value VisitValueNull([NotNull] YarnSpinnerParser.ValueNullContext context)
        {
            throw new TypeException(context, "Null is not a permitted type in Yarn Spinner 2.0 and later", sourceFileName);
        }

        public override Value VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            if (float.TryParse(context.GetText(), out var result))
            {
                return new Value(BuiltinTypes.Number, result);
            }

            throw new FormatException($"Failed to parse {context.GetText()} as a float");
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
                throw new TypeException(context, $"{enumName} is not a valid enum name", sourceFileName);
            }

            // Ensure that this enum has a member of this name
            var member = enumType.Members.FirstOrDefault(m => m.Name == memberName);

            if (member == null) {
                throw new TypeException(context, $"Enum {enumName} does not have a member called {memberName}", sourceFileName);
            }

            var value = new Value(enumType, member.InternalRepresentation);

            context.EnumType = enumType;
            context.EnumMember = member;

            return value;
        }
    }
}
