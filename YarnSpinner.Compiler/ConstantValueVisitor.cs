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
        /// <param name="diagnostics">The global list of existing diagnostic</param>
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
            if (float.TryParse(context.GetText(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
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
    }
}
