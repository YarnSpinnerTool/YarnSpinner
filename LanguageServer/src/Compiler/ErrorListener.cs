namespace Yarn.Compiler
{
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Antlr4.Runtime;

    internal sealed class LexerErrorListener : IAntlrErrorListener<int>
    {
        private static readonly LexerErrorListener instance = new LexerErrorListener();

        public static LexerErrorListener Instance => instance;

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append($"Error on line {line} at position {charPositionInLine + 1}:");
            builder.AppendLine(msg);

            throw new ParseException(builder.ToString());
        }
    }

    internal sealed class ParserErrorListener : BaseErrorListener
    {
        public static ParserErrorListener Instance { get; } = new ParserErrorListener();

        public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            StringBuilder builder = new StringBuilder();

            // the human readable message
            object[] format = new object[] { line, charPositionInLine + 1 };
            builder.AppendFormat(CultureInfo.CurrentCulture, "Error on line {0} at position {1}:\n", format);

            // the actual error message
            builder.AppendLine(msg);
#if DEBUG
            builder.AppendLine($"Debug: Offending symbol type: {recognizer.Vocabulary.GetSymbolicName(offendingSymbol.Type)}");
#endif

            if (offendingSymbol.TokenSource != null)
            {
                // the line with the error on it
                string input = offendingSymbol.TokenSource.InputStream.ToString();
                string[] lines = input.Split('\n');
                string errorLine = lines[line - 1];
                builder.AppendLine(errorLine);

                // adding indicator symbols pointing out where the error is
                // on the line
                int start = offendingSymbol.StartIndex;
                int stop = offendingSymbol.StopIndex;
                if (start >= 0 && stop >= 0)
                {
                    // the end point of the error in "line space"
                    int end = (stop - start) + charPositionInLine + 1;
                    for (int i = 0; i < end; i++)
                    {
                        // move over until we are at the point we need to
                        // be
                        if (i >= charPositionInLine && i < end)
                        {
                            builder.Append("^");
                        }
                        else
                        {
                            builder.Append(" ");
                        }
                    }
                }
            }

            throw new ParseException(null, builder.ToString(), null);
        }
    }
}