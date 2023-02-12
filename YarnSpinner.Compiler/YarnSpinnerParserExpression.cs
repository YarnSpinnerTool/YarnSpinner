namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    internal static class ParserRuleContextExtension
    {
        /// <summary>
        /// Returns the original text of this <see cref="ExpressionContext"/>, including all
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
}
