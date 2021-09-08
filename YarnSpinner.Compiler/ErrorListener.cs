namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;

    public sealed class Problem
    {
        public string FileName = "(not set)";
        public int Line;
        public int Column;
        public string Message = "(internal error: no message provided)";

        public string Context = null;
        public ProblemSeverity Severity = ProblemSeverity.Error;

        public enum ProblemSeverity
        {
            Error,
            Warning,
            Info
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{this.Line}:{this.Column}: {this.Severity}: {this.Message}");

            if (string.IsNullOrEmpty(this.Context) == false)
            {
                sb.AppendLine();
                sb.AppendLine(this.Context);
            }

            return sb.ToString();
        }
    }

    internal sealed class LexerErrorListener : IAntlrErrorListener<int>
    {
        private readonly List<Problem> problems = new List<Problem>();

        public IEnumerable<Problem> Problems => this.problems;

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            this.problems.Add(new Problem
            {
                Line = line,
                Column = charPositionInLine,
                Message = msg,
            });
        }
    }

    internal sealed class ParserErrorListener : BaseErrorListener
    {
        private readonly List<Problem> problems = new List<Problem>();

        public IEnumerable<Problem> Problems => this.problems;

        public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var problem = new Problem
            {
                Line = line,
                Column = charPositionInLine,
                Message = msg,
            };

            if (offendingSymbol.TokenSource != null)
            {
                StringBuilder builder = new StringBuilder();

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

                problem.Context = builder.ToString();
            }

            this.problems.Add(problem);
        }
    }
}
