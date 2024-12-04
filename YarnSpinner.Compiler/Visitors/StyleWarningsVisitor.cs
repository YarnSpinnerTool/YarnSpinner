// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    internal class StyleWarningsVisitor : DiagnosticsGeneratorVisitor
    {
        private readonly CommonTokenStream tokenStream;

        public StyleWarningsVisitor(string fileName, CommonTokenStream tokenStream) : base(fileName)
        {
            this.tokenStream = tokenStream;
        }

        public override int VisitStatement([NotNull] YarnSpinnerParser.StatementContext context)
        {
            List<IToken>? tokensOnLine = null;

            // If the first token is a COMMAND_START, then it should be the
            // first token on the channel for that line.
            if (context.Start.Type == YarnSpinnerLexer.COMMAND_START)
            {
                tokensOnLine = GetAllTokensOnLine(tokenStream, context.Start.Line);

                var indexOfStartToken = tokensOnLine.IndexOf(context.Start);

                if (indexOfStartToken != 0)
                {
                    AddDiagnostic(new Diagnostic(this.FileName, tokensOnLine[indexOfStartToken - 1], $"Commands should start on a new line", Diagnostic.DiagnosticSeverity.Warning));
                }
            }

            // If the last token is a COMMAND_END, then it should be the last
            // token on the channel for that line.
            if (context.Stop.Type == YarnSpinnerLexer.COMMAND_END)
            {
                tokensOnLine = GetAllTokensOnLine(tokenStream, context.Stop.Line);

                var indexOfStopToken = tokensOnLine.IndexOf(context.Stop);

                if (indexOfStopToken != (tokensOnLine.Count - 1))
                {
                    AddDiagnostic(new Diagnostic(this.FileName, tokensOnLine[indexOfStopToken + 1], $"Commands should have no text after them", Diagnostic.DiagnosticSeverity.Warning));
                }
            }

            return base.VisitStatement(context);
        }

        private static int[] OmitTokenTypes = new[] { YarnSpinnerLexer.INDENT, YarnSpinnerLexer.DEDENT };

        public static List<IToken> GetAllTokensOnLine(CommonTokenStream tokenStream, int line, int channel = Lexer.DefaultTokenChannel)
        {

            return tokenStream.GetTokens()
                .Where(t => t.Channel == channel && t.Line == line && OmitTokenTypes.Contains(t.Type) == false)
                .ToList();
        }
    }
}
