namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    public partial class YarnSpinnerParser : Parser
    {
        public partial class ExpressionContext : ParserRuleContext
        {
            public Yarn.Type Type { get; set; }

            public string GetTextWithWhitespace() {
                // Get the original text of this ExpressionContext. We
                // can't use "expressionContext.GetText()" here, because
                // that just concatenates the text of all captured tokens,
                // and doesn't include text on hidden channels (e.g.
                // whitespace and comments).
                var interval = new Interval(this.Start.StartIndex, this.Stop.StopIndex);
                return this.Start.InputStream.GetText(interval);
            }
        }
    }
}
