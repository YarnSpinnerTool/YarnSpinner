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
        private readonly ILookup<int, IToken> tokenLineLookup;

        public StyleWarningsVisitor(string fileName, CommonTokenStream tokenStream) : base(fileName)
        {
            this.tokenStream = tokenStream;
            this.tokenLineLookup = tokenStream.GetTokens().ToLookup(token => token.Line);

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
                    // Get the command name to include in the diagnostic message
                    string? commandName = GetCommandName(context);
                    var diagnostic = Diagnostic.CreateDiagnostic(
                        this.FileName,
                        context.Start,
                        DiagnosticDescriptor.LineContentBeforeCommand,
                        commandName ?? "command"
                    );
                    AddDiagnostic(diagnostic);
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
                    // Get the command name to include in the diagnostic message
                    string? commandName = GetCommandName(context);
                    var diagnostic = Diagnostic.CreateDiagnostic(
                        this.FileName,
                        context.Stop,
                        DiagnosticDescriptor.LineContentAfterCommand,
                        commandName ?? "command"
                    );
                    AddDiagnostic(diagnostic);
                }
            }

            return base.VisitStatement(context);
        }

        private static readonly int[] OmitTokenTypes = new[] {
            YarnSpinnerLexer.INDENT,
            YarnSpinnerLexer.DEDENT,
            YarnSpinnerLexer.BLANK_LINE_FOLLOWING_OPTION,
        };

        private List<IToken> GetAllTokensOnLine(CommonTokenStream tokenStream, int line, int channel = Lexer.DefaultTokenChannel)
        {
            return tokenLineLookup[line]
                .Where(t => t.Channel == channel && OmitTokenTypes.Contains(t.Type) == false)
                .ToList();
        }

        private string? GetCommandName(YarnSpinnerParser.StatementContext context)
        {
            // Get all tokens in the statement to find the command keyword
            var tokens = tokenStream.GetTokens(context.Start.TokenIndex, context.Stop.TokenIndex);

            foreach (var token in tokens)
            {
                switch (token.Type)
                {
                    case YarnSpinnerLexer.COMMAND_IF:
                        return "if";
                    case YarnSpinnerLexer.COMMAND_ELSEIF:
                        return "elseif";
                    case YarnSpinnerLexer.COMMAND_ELSE:
                        return "else";
                    case YarnSpinnerLexer.COMMAND_ENDIF:
                        return "endif";
                    case YarnSpinnerLexer.COMMAND_SET:
                        return "set";
                    case YarnSpinnerLexer.COMMAND_DECLARE:
                        return "declare";
                    case YarnSpinnerLexer.COMMAND_JUMP:
                        return "jump";
                    case YarnSpinnerLexer.COMMAND_CALL:
                        return "call";
                }
            }

            return null;
        }
    }
}
