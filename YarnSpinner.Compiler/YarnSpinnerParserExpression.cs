// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    internal static class ParserRuleContextExtension
    {
        /// <summary>
        /// Returns the original text of this <see cref="YarnSpinnerParser.ExpressionContext"/>, including all
        /// whitespace, comments, and other information that the parser
        /// would otherwise not include.
        /// </summary>
        /// <returns>The original text of this expression.</returns>
        public static string GetTextWithWhitespace(this ParserRuleContext context)
        {
            // We can't use "expressionContext.GetText()" here, because
            // that just concatenates the text of all captured tokens,
            // and doesn't include text on hidden channels (e.g.
            // whitespace and comments).

            // some times it seems that vscode can request a negative interval
            // almost certainly something wrong we are doing
            // but as a non-crashing fallback we prevent this
            if (context.Start.StartIndex > context.Stop.StopIndex)
            {
                return context.GetText();
            }
            else
            {
                var interval = new Interval(context.Start.StartIndex, context.Stop.StopIndex);
                return context.Start.InputStream.GetText(interval);   
            }
        }
    }

    public partial class YarnSpinnerParser : Parser
    {
        public partial class ExpressionContext : ParserRuleContext
        {
            /// <summary>
            /// Gets or sets the type that this expression has been
            /// determined to be by a <see cref="TypeCheckVisitor"/>
            /// object.
            /// </summary>
            public Yarn.IType Type { get; set; }

            /// <summary>
            /// Gets or sets a type hint for the expression.
            /// This is mostly used by <see cref="TypeCheckVisitor"/>
            /// to give a hint that can be used by functions to
            /// influence their type when set to use inference.
            /// Won't be used if a concrete type is already known.
            /// </summary>
            public Yarn.IType Hint { get; set; }
        }

        public partial class ValueContext : ParserRuleContext
        {
            /// <summary>
            /// Gets or sets a type hint for the expression.
            /// This is mostly used by <see cref="TypeCheckVisitor"/>
            /// to give a hint that can be used by functions to
            /// influence their type when set to use inference.
            /// Won't be used if a concrete type is already known.
            /// </summary>
            public Yarn.IType Hint { get; set; }
        }
    }
}
