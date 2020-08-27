namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;

    /// <summary>
    /// A visitor that visits any valid constant value, and returns a <see
    /// cref="Value"/>. Currently only supports terminals, not expressions,
    /// even if those expressions would be constant.
    /// </summary>
    internal class ConstantValueVisitor : YarnSpinnerParserBaseVisitor<object>
    {

        // Default result is an exception - only specific parse nodes can
        // be visited by this visitor
        protected override object DefaultResult
        {
            get
            {
                throw new InvalidOperationException($"Invalid parse node for {nameof(ConstantValueVisitor)}");
            }
        }

        public override object VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            if (float.TryParse(context.GetText(), out var result))
            {
                return result;
            }
            throw new FormatException($"Failed to parse {context.GetText()} as a float");
        }

        public override object VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            string stringVal = context.STRING().GetText().Trim('"');

            return stringVal;
        }

        public override object VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return false;
        }

        public override object VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return true;
        }
    }
}
