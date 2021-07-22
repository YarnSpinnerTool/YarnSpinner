namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    struct ConstantValue
    {
        public Yarn.IType type;
        public IConvertible value;
    }

    /// <summary>
    /// A visitor that visits any valid constant value, and returns a <see
    /// cref="Value"/>. Currently only supports terminals, not expressions,
    /// even if those expressions would be constant.
    /// </summary>
    internal class ConstantValueVisitor : YarnSpinnerParserBaseVisitor<ConstantValue>
    {
        private string sourceFileName;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ConstantValueVisitor"/> class.
        /// </summary>
        /// <param name="sourceFileName">The name of the file that is being
        /// visited by this instance.</param>
        public ConstantValueVisitor(string sourceFileName)
        {
            this.sourceFileName = sourceFileName;
        }

        // Default result is an exception - only specific parse nodes can
        // be visited by this visitor
        protected override ConstantValue DefaultResult
        {
            get
            {
                throw new TypeException($"Invalid parse node for {nameof(ConstantValueVisitor)}");
            }
        }

        public override ConstantValue VisitValueNull([NotNull] YarnSpinnerParser.ValueNullContext context)
        {
            throw new TypeException(context, "Null is not a permitted type in Yarn Spinner 2.0 and later", sourceFileName);
        }

        public override ConstantValue VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            if (float.TryParse(context.GetText(), out var result))
            {
                return new ConstantValue { type = BuiltinTypes.Number, value = result };
            }
            throw new FormatException($"Failed to parse {context.GetText()} as a float");
        }

        public override ConstantValue VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            string stringVal = context.STRING().GetText().Trim('"');

            return new ConstantValue { type = BuiltinTypes.String, value = stringVal };
        }

        public override ConstantValue VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return new ConstantValue { type = BuiltinTypes.Boolean, value = false };
        }

        public override ConstantValue VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return new ConstantValue { type = BuiltinTypes.Boolean, value = true };
        }
    }
}
