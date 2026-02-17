// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

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

        // Commands that are allowed to have line content on the same line
        private static readonly HashSet<string> FlowControlCommands = new HashSet<string>
        {
            "if", "elseif", "else", "endif",
            "once", "endonce"
        };

        public IEnumerable<Diagnostic> Diagnostics => diagnostics;

        public SyntaxValidationListener(string fileName, CommonTokenStream tokens)
        {
            this.fileName = fileName;
            this.tokens = tokens;
        }

        public override void EnterLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            // Check for malformed commands in plain text
            CheckMalformedCommandsInText(context);

            // Check for line content on the same line as a command
            // This detects patterns like:
            // - "text before <<set $x>>"
            // - "<<set $x>> text after"
            CheckLineContentWithCommand(context);
        }

        private void CheckMalformedCommandsInText(YarnSpinnerParser.Line_statementContext context)
        {
            // Get the source line to check for malformed syntax
            if (context.Start == null) return;

            var sourceText = context.Start.InputStream.ToString();
            var lines = sourceText.Split('\n');
            var lineNum = context.Start.Line - 1;

            if (lineNum >= lines.Length) return;

            var lineText = lines[lineNum];

            // Check for stray >> (command end without command start)
            // Count << and >> to see if they're balanced
            var commandStarts = 0;
            var commandEnds = 0;
            var lastCommandEndPos = -1;

            for (int i = 0; i < lineText.Length - 1; i++)
            {
                if (lineText[i] == '<' && lineText[i + 1] == '<')
                {
                    commandStarts++;
                    i++; // Skip next char
                }
                else if (lineText[i] == '>' && lineText[i + 1] == '>')
                {
                    commandEnds++;
                    lastCommandEndPos = i;
                    i++; // Skip next char
                }
            }

            // If we have more >> than <<, there's a stray >>
            if (commandEnds > commandStarts && lastCommandEndPos >= 0)
            {
                var diagnostic = Diagnostic.CreateDiagnostic(
                    fileName,
                    new Range(lineNum, lastCommandEndPos, lineNum, lastCommandEndPos + 2),
                    DiagnosticDescriptor.StrayCommandEnd
                );
                diagnostics.Add(diagnostic);
            }

            // Check for command keywords in text (outside of << >>)
            // This catches "set $foo" or "declare $bar" appearing as dialogue
            var commandKeywords = new[] { "set", "declare", "jump", "call", "local" };

            foreach (var keyword in commandKeywords)
            {
                // Look for keyword followed by whitespace and $
                var pattern = new Regex($@"\b{keyword}\s+\$", RegexOptions.IgnoreCase);
                var match = pattern.Match(lineText);

                if (match.Success)
                {
                    // Check if this match is inside a << >> block
                    var matchPos = match.Index;
                    var insideCommand = false;

                    int commandDepth = 0;
                    for (int i = 0; i < matchPos && i < lineText.Length - 1; i++)
                    {
                        if (lineText[i] == '<' && lineText[i + 1] == '<')
                        {
                            commandDepth++;
                            i++;
                        }
                        else if (lineText[i] == '>' && lineText[i + 1] == '>')
                        {
                            commandDepth--;
                            i++;
                        }
                    }

                    insideCommand = commandDepth > 0;

                    if (!insideCommand)
                    {
                        var diagnostic = Diagnostic.CreateDiagnostic(
                            fileName,
                            new Range(lineNum, match.Index, lineNum, match.Index + keyword.Length),
                            DiagnosticDescriptor.UnenclosedCommand,
                            keyword
                        );
                        diagnostics.Add(diagnostic);
                        break; // Only report first instance
                    }
                }
            }
        }

        private void CheckLineContentWithCommand(YarnSpinnerParser.Line_statementContext context)
        {
            // Get all tokens on this line
            var startToken = context.Start;
            var stopToken = context.Stop;

            if (startToken == null || stopToken == null)
            {
                return;
            }

            var lineTokens = new List<IToken>();
            for (int i = startToken.TokenIndex; i <= stopToken.TokenIndex; i++)
            {
                lineTokens.Add(tokens.Get(i));
            }

            // Check if this line has both a command and text content
            bool hasCommand = false;
            bool hasTextBeforeCommand = false;
            bool hasTextAfterCommand = false;
            string? commandName = null;
            IToken? commandStartToken = null;
            IToken? commandEndToken = null;

            for (int i = 0; i < lineTokens.Count; i++)
            {
                var token = lineTokens[i];

                // Found a command start marker
                if (token.Type == YarnSpinnerLexer.COMMAND_START)
                {
                    hasCommand = true;
                    commandStartToken = token;

                    // Check if there's non-whitespace text BEFORE this command
                    for (int j = 0; j < i; j++)
                    {
                        var prevToken = lineTokens[j];
                        // Skip whitespace and hashtags (which can appear before commands in options)
                        if (prevToken.Type == YarnSpinnerLexer.WHITESPACE ||
                            prevToken.Type == YarnSpinnerLexer.HASHTAG)
                        {
                            continue;
                        }
                        // Found text content before command
                        hasTextBeforeCommand = true;
                        break;
                    }
                }

                // Found a command end marker
                if (token.Type == YarnSpinnerLexer.COMMAND_END)
                {
                    hasCommand = true;
                    commandEndToken = token;

                    // Check if there's non-whitespace text after this command
                    for (int j = i + 1; j < lineTokens.Count; j++)
                    {
                        var nextToken = lineTokens[j];
                        // Skip whitespace
                        if (nextToken.Type == YarnSpinnerLexer.WHITESPACE)
                        {
                            continue;
                        }
                        // Skip newlines (end of line)
                        if (nextToken.Type == YarnSpinnerLexer.NEWLINE)
                        {
                            break;
                        }
                        // Found text content after command
                        hasTextAfterCommand = true;
                        break;
                    }
                }

                // Detect command type (if, set, declare, etc.)
                if (token.Type == YarnSpinnerLexer.COMMAND_IF ||
                    token.Type == YarnSpinnerLexer.COMMAND_ELSEIF ||
                    token.Type == YarnSpinnerLexer.COMMAND_ELSE ||
                    token.Type == YarnSpinnerLexer.COMMAND_ENDIF)
                {
                    commandName = token.Text.ToLower();
                }
                else if (token.Type == YarnSpinnerLexer.COMMAND_SET)
                {
                    commandName = "set";
                }
                else if (token.Type == YarnSpinnerLexer.COMMAND_DECLARE)
                {
                    commandName = "declare";
                }
                else if (token.Type == YarnSpinnerLexer.COMMAND_JUMP)
                {
                    commandName = "jump";
                }
                else if (token.Type == YarnSpinnerLexer.COMMAND_CALL)
                {
                    commandName = "call";
                }
            }

            // If we have text before a command (non-flow-control), report it
            if (hasCommand && hasTextBeforeCommand && commandName != null && commandStartToken != null)
            {
                if (!FlowControlCommands.Contains(commandName))
                {
                    var diagnostic = Diagnostic.CreateDiagnostic(
                        fileName,
                        new Range(
                            commandStartToken.Line - 1,
                            commandStartToken.Column,
                            commandStartToken.Line - 1,
                            commandStartToken.Column + 2
                        ),
                        DiagnosticDescriptor.LineContentBeforeCommand,
                        commandName
                    );
                    diagnostics.Add(diagnostic);
                }
            }

            // If we have text after a command (non-flow-control), report it
            if (hasCommand && hasTextAfterCommand && commandName != null && commandEndToken != null)
            {
                if (!FlowControlCommands.Contains(commandName))
                {
                    var diagnostic = Diagnostic.CreateDiagnostic(
                        fileName,
                        new Range(
                            commandEndToken.Line - 1,
                            commandEndToken.Column + commandEndToken.Text.Length,
                            commandEndToken.Line - 1,
                            commandEndToken.Column + commandEndToken.Text.Length + 1
                        ),
                        DiagnosticDescriptor.LineContentAfterCommand,
                        commandName
                    );
                    diagnostics.Add(diagnostic);
                }
            }
        }
    }
}
