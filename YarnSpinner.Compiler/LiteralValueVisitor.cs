namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// A visitor that visits any valid literal (i.e. numbers, bools, strings),
    /// and returns a <see cref="Value"/>.
    /// </summary>
    internal class LiteralValueVisitor : YarnSpinnerParserBaseVisitor<Value>
    {
        private readonly ParserRuleContext context;
        private readonly string sourceFileName;
        private List<Diagnostic> diagnostics;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="LiteralValueVisitor"/> class.
        /// </summary>
        /// <param name="context">The parser context for this value.</param>
        /// <param name="sourceFileName">The name of the file that is being
        /// visited by this instance.</param>
        /// <param name="types">The types of values known to this instance.</param>
        public LiteralValueVisitor(ParserRuleContext context, string sourceFileName, List<Diagnostic> diagnostics)
        {
            this.context = context;
            this.sourceFileName = sourceFileName;
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
                return new Value(Types.Error, null);
            }
        }

        public override Value VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            if (float.TryParse(context.GetText(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return new Value(Types.Number, result);
            }
            else
            {
                string message = $"Failed to parse {context.GetText()} as a float";
                this.diagnostics.Add(new Diagnostic(this.sourceFileName, context, message));
                return new Value(Types.Number, 0f);
            }
        }

        public override Value VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            return new Value(Types.String, context.STRING().GetText().Trim('"'));
        }

        public override Value VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            return new Value(Types.Boolean, false);
        }

        public override Value VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            return new Value(Types.Boolean, true);
        }
    }

    // Mark numbers, true/false, and string as literals, so that other parts of
    // the code can more easily determine whether a parse node is a literal or
    // not. (We do this so that a declaration can check to see if its initial
    // value was of a literal, or of a reference to an identifier.)
    public partial class YarnSpinnerParser {
        public interface ILiteralContext { }
        public partial class ValueNumberContext : ILiteralContext { }
        public partial class ValueFalseContext : ILiteralContext { }
        public partial class ValueTrueContext : ILiteralContext { }
        public partial class ValueStringContext : ILiteralContext { }
    }
}
