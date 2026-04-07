// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using System.Collections.Generic;

    /// <summary>
    /// A visitor that visits any valid literal (i.e. numbers, bools, strings),
    /// and returns a <see cref="Value"/>.
    /// </summary>
    internal class LiteralValueVisitor : YarnSpinnerParserBaseVisitor<Value>
    {
        private readonly ParserRuleContext context;
        private readonly string sourceFileName;
        private readonly List<Diagnostic> diagnostics;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="LiteralValueVisitor"/> class.
        /// </summary>
        /// <param name="context">The parser context for this value.</param>
        /// <param name="sourceFileName">The name of the file that is being
        /// visited by this instance.</param>
        /// <param name="diagnostics">The list of diagnostics to add to.</param>
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
                this.diagnostics.Add(DiagnosticDescriptor.InvalidLiteralValue.Create(this.sourceFileName, context, message));
                return new Value(Types.Error, "<ERROR>");
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
                this.diagnostics.Add(DiagnosticDescriptor.InvalidLiteralValue.Create(this.sourceFileName, context, message));
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
    public partial class YarnSpinnerParser
    {
        /// <summary>
        /// Marks that a type represents a <see cref="ParserRuleContext"/> that
        /// represents a literal value.
        /// </summary>
        public interface ILiteralContext { }

        /// <summary>
        /// A number literal value.
        /// </summary>
        public partial class ValueNumberContext : ILiteralContext { }

        /// <summary>
        /// A false boolean literal value.
        /// </summary>
        public partial class ValueFalseContext : ILiteralContext { }

        /// <summary>
        /// A true boolean literal value.
        /// </summary>
        public partial class ValueTrueContext : ILiteralContext { }

        /// <summary>
        /// A string literal value.
        /// </summary>
        public partial class ValueStringContext : ILiteralContext { }
    }
}
