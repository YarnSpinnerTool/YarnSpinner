using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace YarnLanguageServer
{
    internal class LexerDiagnosticErrorListener : IAntlrErrorListener<int>
    {
        public List<Diagnostic> Errors { get; set; } = new List<Diagnostic>();

        void IAntlrErrorListener<int>.SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var range = new Range(line - 1, charPositionInLine, line - 1, charPositionInLine + 1); // Not sure how to get token length here
            var d = new Diagnostic
            {
                Message = msg,
                Severity = DiagnosticSeverity.Error,
                Range = range,
            };

            Errors.Add(d);
        }
    }

    internal class ParserDiagnosticErrorListener : BaseErrorListener
    {
        public List<Diagnostic> Errors { get; set; } = new List<Diagnostic>();

        public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var range = new Range(line - 1, charPositionInLine, line - 1, charPositionInLine + (offendingSymbol?.Text?.Length ?? 1000));
            var d = new Diagnostic
            {
                Message = msg,
                Severity = DiagnosticSeverity.Error,
                Range = range,
            };

            Errors.Add(d);
        }
    }
}