namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Antlr4.Runtime;
    
    public sealed class Diagnostic
    {
        public string FileName = "(not set)";
        public int Line;
        public int Column;
        public string Message = "(internal error: no message provided)";

        public string Context = null;
        public DiagnosticSeverity Severity = DiagnosticSeverity.Error;

        public Diagnostic(string fileName, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            this.FileName = fileName;
            this.Message = message;
            this.Severity = severity;
        }
        
        public Diagnostic(string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        : this(null, message, severity)
        {
        }

        public Diagnostic(string fileName, ParserRuleContext context, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            this.FileName = fileName;
            this.Column = context?.Start.Column ?? 0;
            this.Line = context?.Start.Line ?? 0;
            this.Message = message;
            this.Context = context.GetTextWithWhitespace();
            this.Severity = severity;
        }

        public Diagnostic(string fileName, int line, int column, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error) {
            this.FileName = fileName;
            this.Column = column;
            this.Line = line;
            this.Message = message;
            this.Severity = severity;
        }

        public enum DiagnosticSeverity
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

        public override bool Equals(object obj)
        {
            return obj is Diagnostic problem &&
                   this.FileName == problem.FileName &&
                   this.Line == problem.Line &&
                   this.Column == problem.Column &&
                   this.Message == problem.Message &&
                   this.Context == problem.Context &&
                   this.Severity == problem.Severity;
        }

        public override int GetHashCode()
        {
            int hashCode = -1856104752;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.FileName);
            hashCode = (hashCode * -1521134295) + this.Line.GetHashCode();
            hashCode = (hashCode * -1521134295) + this.Column.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Message);
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Context);
            hashCode = (hashCode * -1521134295) + this.Severity.GetHashCode();
            return hashCode;
        }
    }

    internal sealed class LexerErrorListener : IAntlrErrorListener<int>
    {
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        private readonly string fileName;

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        public LexerErrorListener(string fileName) : base() {
            this.fileName = fileName;
        }


        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            this.diagnostics.Add(new Diagnostic(null, line, charPositionInLine, msg));
        }
    }

    internal sealed class ParserErrorListener : BaseErrorListener
    {
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        private readonly string fileName;

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        public ParserErrorListener(string fileName) : base() {
            this.fileName = fileName;
        }

        public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var diagnostic = new Diagnostic(null, line, charPositionInLine, msg);
            
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

                diagnostic.Context = builder.ToString();
            }

            this.diagnostics.Add(diagnostic);
        }
    }
}
