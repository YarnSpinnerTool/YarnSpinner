using Antlr4.Runtime;
using Antlr4.Runtime.Misc;

namespace YarnLanguageServer
{
    internal static class ParserRuleContextExtension
    {
        /// <summary>
        /// Returns the original text of this <see cref="ExpressionContext"/>,
        /// including all whitespace, comments, and other information that the
        /// parser would otherwise not include.
        /// </summary>
        /// <param name="context">The parser context to get the original text
        /// for.</param>
        /// <returns>The original text of this expression.</returns>
        public static string GetTextWithWhitespace(this ParserRuleContext context)
        {
            // We can't use "expressionContext.GetText()" here, because
            // that just concatenates the text of all captured tokens,
            // and doesn't include text on hidden channels (e.g.
            // whitespace and comments).
            var interval = new Interval(context.Start.StartIndex, context.Stop.StopIndex);
            if (interval.Length <= 0) {
                return string.Empty;
            } else {
                return context.Start.InputStream.GetText(interval);
            }
        }

        public static Yarn.Compiler.Position ToPosition(this Antlr4.Runtime.IToken token) {
            return new Yarn.Compiler.Position
            {
                Line = token.Line - 1,
                Character = token.Column,
            };
        }
    }
}
