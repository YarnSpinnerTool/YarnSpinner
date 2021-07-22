namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

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
            /// Returns the original text of this <see cref="ExpressionContext"/>, including all
            /// whitespace, comments, and other information that the parser
            /// would otherwise not include.
            /// </summary>
            /// <returns>The original text of this expression.</returns>
            public string GetTextWithWhitespace()
            {
                // We can't use "expressionContext.GetText()" here, because
                // that just concatenates the text of all captured tokens,
                // and doesn't include text on hidden channels (e.g.
                // whitespace and comments).
                var interval = new Interval(this.Start.StartIndex, this.Stop.StopIndex);
                return this.Start.InputStream.GetText(interval);
            }
        }
    }
}
