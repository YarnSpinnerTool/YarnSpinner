// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Validates syntax patterns that the parser accepts but are semantically incorrect.
    /// This includes:
    /// - Stray command end markers without matching start markers
    /// - Command keywords outside of command blocks
    /// - Line content on the same line as non-flow-control commands
    /// </summary>
    internal class SyntaxValidationListener : YarnSpinnerParserBaseListener
    {
        private readonly string fileName;
        private readonly CommonTokenStream tokens;
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        private int mostRecentLineStatementLine = -1;
        private int mostRecentCommandStatementLine = -1;
        private YarnSpinnerParser.Command_statementContext? mostRecentCommandContext = null;

        public IEnumerable<Diagnostic> Diagnostics => diagnostics;

        public SyntaxValidationListener(string fileName, CommonTokenStream tokens)
        {
            this.fileName = fileName;
            this.tokens = tokens;
        }

        public override void EnterLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            mostRecentLineStatementLine = context.Start.Line;

            if (mostRecentCommandStatementLine == mostRecentLineStatementLine)
            {
                var dialogue = context.GetTextWithWhitespace().Trim();
                // this means we have entered a line statement that is on the same line as a command we have previously exited
                // ergo the line statement is AFTER a command on the same line, which we do not want
                // but there are two forms of this
                // one is you have a normal line following the command which is poor form
                // the other you have extra trailing chevrons which can occur due to autocarrot being autocarrotey

                // in all cases the range is just the whole line after the command
                // so we can set that up now
                var range = new Range(
                    context.Start.Line - 1,
                    context.Start.Column,
                    context.Stop.Line - 1,
                    context.Stop.Column
                );

                // basically if the line content is just >'s then it gets it's own special error code
                int numberOfTrailingChevrons;
                for (numberOfTrailingChevrons = 0; numberOfTrailingChevrons < dialogue.Length; numberOfTrailingChevrons++)
                {
                    if (dialogue[numberOfTrailingChevrons] != '>')
                    {
                        break;
                    }
                }
                
                if (numberOfTrailingChevrons == dialogue.Length)
                {
                    // we are following a command but somehow don't have a command context?!
                    if (mostRecentCommandContext == null)
                    {
                        var impossibleDiag = Diagnostic.CreateDiagnostic(fileName, range, DiagnosticDescriptor.InternalError, "While attempting to warn about line content following commands found a line following a command but we have no command context!");
                        diagnostics.Add(impossibleDiag);
                        return;
                    }

                    var diagnostic = Diagnostic.CreateDiagnostic(
                        fileName,
                        range,
                        DiagnosticDescriptor.RogueChevronWithCommand,
                        mostRecentCommandContext.command_formatted_text().GetTextWithWhitespace().Trim()
                    );
                    diagnostics.Add(diagnostic);
                }
                else
                {
                    var diagnostic = Diagnostic.CreateDiagnostic(
                        fileName,
                        range,
                        DiagnosticDescriptor.LineContentAfterCommand,
                        dialogue
                    );

                    var fullLine = ErrorUtility.LineOfCurrentToken(tokens, context.Start.InputStream);
                    diagnostic.Context = ErrorUtility.GenerateContextMessage(fullLine, diagnostic.Range.Start.Character, diagnostic.Range.End.Character);

                    diagnostics.Add(diagnostic);
                }
            }
            else
            {
                CheckMalformedCommandsInText(context);
            }
        }

        public override void EnterCommand_statement([NotNull] YarnSpinnerParser.Command_statementContext context)
        {
            mostRecentCommandStatementLine = context.Start.Line;
            mostRecentCommandContext = context;

            // do a check to see if the command name is <blah
            // if it is then it is a command with extra chevrons
            // which is almost certainly indicative of an error
            var commandName = context.command_formatted_text().GetTextWithWhitespace().Trim();
            if (commandName.StartsWith("<"))
            {
                var fullLine = context.GetTextWithWhitespace().Trim();
                int i;
                for (i = 0; i < fullLine.Length; i++)
                {
                    if (fullLine[i] != '<')
                    {
                        break;
                    }
                }
                // range = the first start of the first token of the command as many chevrons as there are -2 because we want those as they are required for the command
                var range = new Range(
                    context.Start.Line - 1,
                    context.Start.Column,
                    context.Start.Line - 1,
                    context.Start.Column + i - 2
                );
                var diagnostic = Diagnostic.CreateDiagnostic(fileName, range, DiagnosticDescriptor.RogueChevronWithCommand, commandName.TrimStart('<'));
                diagnostics.Add(diagnostic);
            }
        }

        private void CheckMalformedCommandsInText(YarnSpinnerParser.Line_statementContext context)
        {
            // Get the source line to check for malformed syntax
            if (context.Start == null)
            {
                return;
            }

            var lineText = context.line_formatted_text().GetTextWithWhitespace().Trim();

            // this shouldn't be possible but it never hurts to double check
            // this really should also be a compilation error right?
            if (string.IsNullOrWhiteSpace(lineText))
            {
                return;
            }

            // determining the number of chevrons at either end of line
            // we need to know this for later diag generation
            // should probably do this with one loop
            int numberOfStartingChevrons = 0;
            int numberOfClosingChevrons = 0;

            for (int i = 0; i < lineText.Length; i++)
            {
                if (lineText[i] != '<')
                {
                    break;
                }
                numberOfStartingChevrons += 1;
            }
            for (int i = 1; i < lineText.Length - 1; i++)
            {
                if (lineText[lineText.Length - i] != '>')
                {
                    break;
                }
                numberOfClosingChevrons += 1;
            }

            // <some actual command>
            if (numberOfStartingChevrons == 1 && numberOfClosingChevrons == 1)
            {
                var start = context.line_formatted_text().Start;
                var stop = context.line_formatted_text().Stop;

                var diagnostic = Diagnostic.CreateDiagnostic(
                    fileName,
                    new Range(context.Start.Line - 1, 0, context.Start.Line - 1, stop.StopIndex - start.StartIndex + 1),
                    DiagnosticDescriptor.SingularCommandWrap,
                    lineText
                );
                diagnostics.Add(diagnostic);
                return;
            }

            // some actual command>>
            // <some actual command>>
            if ((numberOfStartingChevrons == 0 || numberOfStartingChevrons == 1) && (numberOfClosingChevrons == 2))
            {
                var start = context.line_formatted_text().Start;
                var stop = context.line_formatted_text().Stop;
                // the indices are zero indexed but we need them to be exclusive ranges
                // so the final token spot needs to be moved down one index before we subtract the number of chevrons
                var endTokenPosition = stop.StopIndex + 1 - start.StartIndex - numberOfClosingChevrons;

                var diagnostic = Diagnostic.CreateDiagnostic(
                    fileName,
                    new Range(context.Start.Line - 1, endTokenPosition, context.Start.Line - 1, endTokenPosition + numberOfClosingChevrons),
                    DiagnosticDescriptor.StrayCommandEnd,
                    new string('>', numberOfClosingChevrons)
                );
                diagnostics.Add(diagnostic);
                return;
            }

            // at this point we are done dealing with unbalanced chevrons
            if (numberOfClosingChevrons + numberOfStartingChevrons != 0)
            {
                return;
            }

            string? addendum;
            switch (ParseLineAsCommand(lineText))
            {
                case StatementType.Declare:
                    addendum = "declare";
                    break;
                case StatementType.Set:
                    addendum = "set";
                    break;
                case StatementType.Jump:
                    addendum = "jump";
                    break;
                case StatementType.Detour:
                    addendum = "detour";
                    break;
                default:
                    return;
            }

            var missingChevronDiag = Diagnostic.CreateDiagnostic(
                fileName,
                new Range(context.Start.Line - 1, 0, context.Start.Line - 1, addendum.Length),
                DiagnosticDescriptor.UnenclosedCommand,
                addendum,
                lineText
            );
            diagnostics.Add(missingChevronDiag);

            return;
        }

        internal enum StatementType
        {
            None, Set, Declare, Jump, Detour,
        }
        internal class AnyErrorsDetectedErrorListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
        {
            internal bool hasErrors = false;
            public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                hasErrors = true;
            }

            public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            {
                hasErrors = true;
            }
        }

        internal static StatementType ParseLineAsCommand(string source)
        {
            // our assumption here is that it's a valid built in command, just missing it's chevrons
            // so if we bolt those on it should then return a valid command
            ICharStream input = CharStreams.fromString($"<<{source}>>");

            // Initialize a lexer, and set it to be in body mode already.
            // this way we skip over expecting any node header content
            // it might be possible to force it directly into command mode and then manually add the command start/end to the token stream
            // but for now this is easy enough
            var lexer = new YarnSpinnerLexer(input);
            lexer.PushMode(YarnSpinnerLexer.BodyMode);

            var tokens = new CommonTokenStream(lexer);

            var parser = new YarnSpinnerParser(tokens);

            // Define custom error listeners to capture lexical and parsing
            // errors during these phases.
            AnyErrorsDetectedErrorListener yarnErrorListener = new();

            lexer.RemoveErrorListeners();
            parser.RemoveErrorListeners();
            lexer.AddErrorListener(yarnErrorListener);
            parser.AddErrorListener(yarnErrorListener);

            // if we've got any errors now there is no point in going on, we clearly don't have a valid command
            // it's possible though even without errors we have a statement that isn't one that we care about (like a line)
            var statement = parser.statement();
            if (yarnErrorListener.hasErrors)
            {
                return StatementType.None;
            }

            if (statement != null)
            {
                // now we check each of the statement types we care about
                // and if it isn't null that means it is a valid form of that command (excluding the chevrons)
                if (statement.declare_statement() != null)
                {
                    return StatementType.Declare;
                }
                if (statement.set_statement() != null)
                {
                    return StatementType.Set;
                }
                var jump = statement.jump_statement();
                if (jump != null)
                {
                    // jump has some subforms so we need to check each of them
                    if (jump is YarnSpinnerParser.DetourToExpressionContext)
                    {
                        return StatementType.Detour;
                    }
                    if (jump is YarnSpinnerParser.DetourToNodeNameContext)
                    {
                        return StatementType.Detour;
                    }
                    return StatementType.Jump;
                }
            }

            return StatementType.None;
        }
    }
}
