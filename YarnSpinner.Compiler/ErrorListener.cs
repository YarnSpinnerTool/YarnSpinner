// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    /// A diagnostic message that describes an error, warning or informational
    /// message that the user can take action on.
    /// </summary>
    /// <remarks>
    /// Diagnostics are presented to the user as the result of compilation,
    /// through the <see cref="CompilationResult"/> class's <see
    /// cref="CompilationResult.Diagnostics"/> property.
    /// </remarks>
    [Serializable]
    public sealed class Diagnostic
    {
        /// <summary>
        /// Gets or sets the path, URI or file-name that the issue occurred in.
        /// </summary>
        public string FileName { get; set; } = "(not set)";

        /// <summary>
        /// Gets or sets the range of the file indicated by <see
        /// cref="FileName"/> that the issue occurred in.
        /// </summary>
        public Range Range { get; set; } = Range.InvalidRange;

        /// <summary>
        /// Gets or sets the description of the issue.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the source text of <see cref="FileName"/> containing
        /// the issue.
        /// </summary>
        public string? Context { get; set; } = null;

        /// <summary>
        /// Gets or sets the severity of the issue.
        /// </summary>
        public DiagnosticSeverity Severity { get; set; } = DiagnosticSeverity.Error;

        /// <summary>
        /// Gets or sets the error code for this diagnostic.
        /// </summary>
        /// <remarks>
        /// Error codes help users look up documentation and categorize issues.
        /// Follows the format YS0001, YS0002, etc.
        /// </remarks>
        public string? Code { get; set; } = null;

        /// <summary>
        /// Gets the zero-indexed line number in FileName at which the issue
        /// begins.
        /// </summary>
        [Obsolete("Use Range.Start.Line")]
        public int Line => Range.Start.Line;

        /// <summary>
        /// Gets the zero-indexed character number in FileName at which the
        /// issue begins.
        /// </summary>
        [Obsolete("Use Range.Start.Character")]
        public int Column => Range.Start.Character;

        /// <summary>
        /// Initializes a new instance of the <see cref="Diagnostic"/> class.
        /// </summary>
        /// <param name="fileName"><inheritdoc cref="FileName"
        /// path="/summary/node()"/></param>
        /// <param name="message"><inheritdoc cref="Message"
        /// path="/summary/node()"/></param>
        /// <param name="severity"><inheritdoc cref="Severity"
        /// path="/summary/node()"/></param>
        public Diagnostic(string fileName, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            this.FileName = fileName;
            this.Message = message;
            this.Severity = severity;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Diagnostic"/> class.
        /// </summary>
        /// <param name="message"><inheritdoc cref="Message"
        /// path="/summary/node()"/></param>
        /// <param name="severity"><inheritdoc cref="Severity"
        /// path="/summary/node()"/></param>
        public Diagnostic(string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        : this("(unknown)", message, severity)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Diagnostic"/> class.
        /// </summary>
        /// <param name="fileName"><inheritdoc cref="FileName"
        /// path="/summary/node()"/></param>
        /// <param name="context">The parse node at which the error
        /// occurred.</param>
        /// <param name="message"><inheritdoc cref="Message"
        /// path="/summary/node()"/></param>
        /// <param name="severity"><inheritdoc cref="Severity"
        /// path="/summary/node()"/></param>
        [Obsolete("Use " + nameof(CreateDiagnostic) + " to create diagnostics.")]
        public Diagnostic(string fileName, ParserRuleContext? context, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            this.FileName = fileName;

            // TODO: maybe fail instead of silently dropping this? if the
            // context is null, then the range is set to (0,0-0,0), which isn't
            // super useful
            if (context != null)
            {
                this.Range = new Range(
                    context.Start.Line - 1,
                    context.Start.Column,
                    context.Stop.Line - 1,
                    context.Stop.Column + context.Stop.Text.Length);

                this.Context = context.GetTextWithWhitespace();
            }
            this.Message = message;
            this.Severity = severity;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Diagnostic"/> class.
        /// </summary>
        /// <param name="fileName"><inheritdoc cref="FileName"
        /// path="/summary/node()"/></param>
        /// <param name="token">The token at which the error
        /// occurred.</param>
        /// <param name="message"><inheritdoc cref="Message"
        /// path="/summary/node()"/></param>
        /// <param name="severity"><inheritdoc cref="Severity"
        /// path="/summary/node()"/></param>
        [Obsolete("Use " + nameof(CreateDiagnostic) + " to create diagnostics.")]
        public Diagnostic(string fileName, IToken token, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            this.FileName = fileName;

            this.Range = new Range(
                token.Line - 1,
                token.Column,
                token.Line - 1,
                token.Column + token.Text.Length);

            this.Message = message;
            this.Context = token.Text;
            this.Severity = severity;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Diagnostic"/> class.
        /// </summary>
        /// <param name="fileName"><inheritdoc cref="FileName"
        /// path="/summary/node()"/></param>
        /// <param name="range"><inheritdoc cref="Range"
        /// path="/summary/node()"/></param>
        /// <param name="message"><inheritdoc cref="Message"
        /// path="/summary/node()"/></param>
        /// <param name="severity"><inheritdoc cref="Severity"
        /// path="/summary/node()"/></param>
        [Obsolete("Use " + nameof(CreateDiagnostic) + " to create diagnostics.")]
        public Diagnostic(string fileName, Range range, string message, DiagnosticSeverity severity = DiagnosticSeverity.Error)
        {
            this.FileName = fileName;
            this.Range = range ?? Range.InvalidRange;
            this.Message = message;
            this.Severity = severity;
        }

        // 'Identifier' is obsolete (TODO: re enable this after we finish making
        // the constructor for Diagnostic private, so that the only way to
        // create a diagnostic is via a DiagnosticDescriptor)
#pragma warning disable CS0618

        // ===== FACTORY METHODS USING DIAGNOSTICDESCRIPTOR =====
        // These methods ensure error codes and messages are always correctly paired

        /// <summary>
        /// Creates a new <see cref="Diagnostic"/> using a <see cref="DiagnosticDescriptor"/>.
        /// </summary>
        /// <param name="fileName">The file where the diagnostic occurred.</param>
        /// <param name="descriptor">The diagnostic descriptor defining the error code and message template.</param>
        /// <param name="args">Arguments to format the message template.</param>
        /// <returns>A new diagnostic instance.</returns>
        public static Diagnostic CreateDiagnostic(string fileName, DiagnosticDescriptor descriptor, params object[] args)
        {
            return new Diagnostic(fileName, descriptor.FormatMessage(args), descriptor.DefaultSeverity)
            {
                Code = descriptor.Code
            };
        }

        /// <summary>
        /// Creates a new <see cref="Diagnostic"/> using a <see cref="DiagnosticDescriptor"/> with range information.
        /// </summary>
        /// <param name="fileName">The file where the diagnostic occurred.</param>
        /// <param name="range">The range in the file where the diagnostic occurred.</param>
        /// <param name="descriptor">The diagnostic descriptor defining the error code and message template.</param>
        /// <param name="args">Arguments to format the message template.</param>
        /// <returns>A new diagnostic instance.</returns>
        public static Diagnostic CreateDiagnostic(string fileName, Range range, DiagnosticDescriptor descriptor, params object[] args)
        {
            return new Diagnostic(fileName, range, descriptor.FormatMessage(args), descriptor.DefaultSeverity)
            {
                Code = descriptor.Code
            };
        }

        /// <summary>
        /// Creates a new <see cref="Diagnostic"/> using a <see cref="DiagnosticDescriptor"/> with parser context.
        /// </summary>
        /// <param name="fileName">The file where the diagnostic occurred.</param>
        /// <param name="context">The parser context where the diagnostic occurred.</param>
        /// <param name="descriptor">The diagnostic descriptor defining the error code and message template.</param>
        /// <param name="args">Arguments to format the message template.</param>
        /// <returns>A new diagnostic instance.</returns>
        public static Diagnostic CreateDiagnostic(string fileName, ParserRuleContext? context, DiagnosticDescriptor descriptor, params object[] args)
        {
            return new Diagnostic(fileName, context, descriptor.FormatMessage(args), descriptor.DefaultSeverity)
            {
                Code = descriptor.Code
            };
        }

        /// <summary>
        /// Creates a new <see cref="Diagnostic"/> using a <see cref="DiagnosticDescriptor"/> with token information.
        /// </summary>
        /// <param name="fileName">The file where the diagnostic occurred.</param>
        /// <param name="token">The token where the diagnostic occurred.</param>
        /// <param name="descriptor">The diagnostic descriptor defining the error code and message template.</param>
        /// <param name="args">Arguments to format the message template.</param>
        /// <returns>A new diagnostic instance.</returns>
        public static Diagnostic CreateDiagnostic(string fileName, IToken token, DiagnosticDescriptor descriptor, params object[] args)
        {
            return new Diagnostic(fileName, token, descriptor.FormatMessage(args), descriptor.DefaultSeverity)
            {
                Code = descriptor.Code
            };
        }
#pragma warning restore CS0618


        /// <summary>
        /// The severity of the issue.
        /// </summary>
        public enum DiagnosticSeverity
        {
            /// <summary>
            /// An informational diagnostic.
            /// </summary>
            /// <remarks>
            /// Infos represent possible issues or steps that the user may wish
            /// to fix, but are unlikely to cause problems.
            /// </remarks>
            Info = 1,

            /// <summary>
            /// A warning.
            /// </summary>
            /// <remarks>
            /// Warnings represent possible problems that the user should fix,
            /// but do not cause the compilation process to fail.
            /// </remarks>
            Warning = 2,

            /// <summary>
            /// An error.
            /// </summary>
            /// <remarks>
            /// If a Yarn source file contains errors, it cannot be compiled,
            /// and the compilation process will fail.
            /// </remarks>
            Error = 3,

        }

        /// <inheritdoc/>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"{this.Range.Start.Line + 1}:{this.Range.Start.Character}: {this.Severity}: {this.Message}");

            if (string.IsNullOrEmpty(this.Context) == false)
            {
                sb.AppendLine();
                sb.AppendLine(this.Context);
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is Diagnostic problem &&
                   this.FileName == problem.FileName &&
                   this.Range.Equals(problem.Range) &&
                   this.Message == problem.Message &&
                   this.Context == problem.Context &&
                   this.Severity == problem.Severity;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = -1856104752;
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.FileName);
            hashCode = (hashCode * -1521134295) + this.Range.GetHashCode();
            hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Message);

            if (this.Context != null)
            {
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Context);
            }
            hashCode = (hashCode * -1521134295) + this.Severity.GetHashCode();
            return hashCode;
        }
    }

    internal sealed class YarnErrorListener: IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        private readonly string fileName;

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        public YarnErrorListener(string fileName) : base()
        {
            this.fileName = fileName;
        }

        private HashSet<int> cursedLines = new();
        
        // this is the lexer error event
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            if (cursedLines.Contains(line))
            {
                return;
            }

            Range range = new Range(line - 1, charPositionInLine, line - 1, charPositionInLine + 1);
            var diagnostic = new Diagnostic(this.fileName, range, msg);

            // Assign error code for lexer errors
            if (msg.ToLowerInvariant().Contains("token recognition error"))
            {
                // Check if we're in CommandMode or ExpressionMode - this indicates an unclosed command
                if (recognizer is Lexer lexer && lexer.ModeStack != null && lexer.ModeStack.Count > 0)
                {
                    // If we have a mode stack, we're likely inside an unclosed command
                    diagnostic.Code = DiagnosticDescriptor.UnclosedCommand.Code;
                    var descriptor = DiagnosticDescriptor.GetDescriptor(DiagnosticDescriptor.UnclosedCommand.Code);
                    if (descriptor != null)
                    {
                        diagnostic.Message = descriptor.MessageTemplate;
                    }
                    cursedLines.Add(line);
                }
                else
                {
                    diagnostic.Code = DiagnosticDescriptor.SyntaxError.Code;
                }
            }

            this.diagnostics.Add(diagnostic);
        }

        // this is the parser error event
        public void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            if (cursedLines.Contains(line))
            {
                return;
            }

            Range range = new(line - 1, charPositionInLine, line - 1, charPositionInLine + 1);
            if (recognizer is not Parser parser)
            {
                this.diagnostics.Add(new Diagnostic(this.fileName, range, "ARGH")); // this needs to change lol
                return;
            }

            var name = ErrorUtility.GetFriendlyNameForRuleContext(parser.RuleContext, false);

            if (parser.RuleContext.RuleIndex == YarnSpinnerParser.RULE_line_condition)
            {
                // we are a borked line condition
                // which means either it isn't an if
                // or the expression inside the if is wrong
                // or we are missing the end >> ?

                // this means we have text inside the line condition that isn't an if
                // this isn't allowed, we have a command on the same line after a line
                // so we want to generate that type of diagnostic
                if (offendingSymbol.Type != YarnSpinnerLexer.COMMAND_IF)
                {
                    var rightMostTerminators = new HashSet<int>
                    {
                        YarnSpinnerLexer.COMMAND_END,
                    };
                    var diagInfo = ErrorUtility.DiagnosticInfo(parser, rightMostTerminators, line, charPositionInLine);

                    var diag = Diagnostic.CreateDiagnostic(
                        this.fileName,
                        diagInfo.range,
                        DiagnosticDescriptor.CommandFollowingLine,
                        diagInfo.tokenText
                    );

                    var fullLine = ErrorUtility.LineOfCurrentToken(parser);
                    if (fullLine != null)
                    {
                        diag.Context = ErrorUtility.GenerateContextMessage(fullLine, diagInfo.range.Start.Character, diagInfo.range.End.Character);
                    }

                    this.diagnostics.Add(diag);
                    cursedLines.Add(line);
                    return;
                }
            }
            else if (parser.RuleContext.RuleIndex == YarnSpinnerParser.RULE_command_statement)
            {
                // we are a borked command
                // which means many different things
                // for now we are just looking at if they missed the close of the command
                if (offendingSymbol.Type == YarnSpinnerLexer.COMMAND_TEXT_NEWLINE)
                {
                    // in this case because we intend on using the command start as the point of our diagnostic we can just use the start and end of that token
                    // indexes are 0 indexed but ranges are 1 indexed hence the +1
                    // at some point if I keep telling myself this I will manage to actually remember it
                    // a mans reach should exceed his grasp
                    var end = parser.RuleContext.Start.StopIndex - parser.RuleContext.Start.StartIndex + 1;
                    Range validRange = new Range(line - 1, charPositionInLine, line - 1, charPositionInLine + 1);
                    // Range validRange = new Range(line - 1, parser.RuleContext.Start.Column, line - 1, end);

                    var diag = Diagnostic.CreateDiagnostic(
                        this.fileName,
                        validRange,
                        DiagnosticDescriptor.UnclosedCommand
                    );

                    this.diagnostics.Add(diag);
                    cursedLines.Add(line);
                    return;
                }
            }

            if (e is NoViableAltException exn)
            {
                msg = ErrorUtility.ReportNoViableAlternative(parser, exn);
            }
            else if (e is InputMismatchException exi)
            {
                msg = ErrorUtility.ReportInputMismatch(parser, exi);
            }

            var diagnostic = new Diagnostic(this.fileName, range, msg);

            if (offendingSymbol.TokenSource != null && offendingSymbol.Type != -1) // -1 token type means it isn't actually a token at all so we can't trust it's values
            {
                StringBuilder builder = new StringBuilder();

                // the line with the error on it
                string errorLine = ErrorUtility.LineOfCurrentToken(parser);
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

                diagnostic.Range = new Range(offendingSymbol.Line - 1, offendingSymbol.Column, offendingSymbol.Line - 1, offendingSymbol.Column + offendingSymbol.Text.Length);
            }

            // Assign error codes based on ANTLR message patterns
            diagnostic.Code = CategorizeParserError(msg);

            // If we assigned a code, update the message to be user-friendly
            if (!string.IsNullOrEmpty(diagnostic.Code))
            {
                var descriptor = DiagnosticDescriptor.GetDescriptor(diagnostic.Code);
                if (descriptor != null)
                {
                    diagnostic.Message = descriptor.MessageTemplate;
                }
            }

            this.diagnostics.Add(diagnostic);
        }

        /// <summary>
        /// Categorizes parser errors from ANTLR messages and assigns appropriate error codes
        /// </summary>
        private string? CategorizeParserError(string message)
        {
            var msg = message.ToLowerInvariant();

            // YS0004: Missing delimiter (=== or ---)
            if (msg.Contains("missing") && (msg.Contains("===") || msg.Contains("'==='") || msg.Contains("delimiter")))
            {
                return DiagnosticDescriptor.MissingDelimiter.Code;
            }

            // YS0006: Unclosed command (missing >>)
            // Match direct "missing >>" messages
            if (msg.Contains("missing") && (msg.Contains("'>'") || msg.Contains(">>")))
            {
                return DiagnosticDescriptor.UnclosedCommand.Code;
            }
            // Match "unexpected" errors with command keywords
            // Pattern: "unexpected 'keyword'" or "unexpected 'keyword'" (different quote styles)
            if (msg.Contains("unexpected"))
            {
                // Check for command keywords with word boundaries
                if (System.Text.RegularExpressions.Regex.IsMatch(msg, @"\b(set|call|jump|detour|return|declare|once|endonce|enum|endenum|case|local)\b") ||
                    System.Text.RegularExpressions.Regex.IsMatch(msg, @"'(set|if|elseif|else|endif|call|jump|detour|return|declare|once|endonce|enum|endenum|case|local)'"))
                {
                    return DiagnosticDescriptor.UnclosedCommand.Code;
                }
            }

            // YS0007: Unclosed scope (missing endif, endonce, etc)
            if (msg.Contains("missing") && (msg.Contains("endif") || msg.Contains("endonce") || msg.Contains("end")))
            {
                return DiagnosticDescriptor.UnclosedScope.Code;
            }

            // YS0005: Malformed dialogue / syntax error
            if (msg.Contains("extraneous input") || msg.Contains("mismatched input"))
            {
                return DiagnosticDescriptor.SyntaxError.Code;
            }

            // Default: no specific code for other ANTLR errors
            return null;
        }
    }

    internal static class ErrorUtility
    {
        internal static string ReportNoViableAlternative(Parser recognizer, NoViableAltException e)
        {
            string? msg = null;

            if (ErrorUtility.IsInsideRule<YarnSpinnerParser.If_statementContext>(recognizer)
                && recognizer.RuleContext is YarnSpinnerParser.StatementContext
                && e.StartToken.Type == YarnSpinnerLexer.COMMAND_START
                && e.OffendingToken.Type == YarnSpinnerLexer.COMMAND_ELSE)
            {
                // We are inside an if statement, we're attempting to parse a
                // statement, and we got an '<<', 'else', and we weren't able
                // to match that. The programmer included an extra '<<else>>'.
                _ = ErrorUtility.GetEnclosingRule<YarnSpinnerParser.If_statementContext>(recognizer);

                msg = $"More than one <<else>> statement in an <<if>> statement isn't allowed";
            }
            else if (e.StartToken.Type == YarnSpinnerLexer.COMMAND_START
                && e.OffendingToken.Type == YarnSpinnerLexer.COMMAND_END)
            {
                // We saw a << immediately followed by a >>. The programmer
                // forgot to include command text.
                msg = $"Command text expected";
            }
            else if (recognizer.RuleContext is YarnSpinnerParser.Declare_statementContext
                && e.OffendingToken.Type == YarnSpinnerLexer.FUNC_ID
                && recognizer.TokenStream.Get(e.OffendingToken.TokenIndex - 1).Type == YarnSpinnerLexer.COMMAND_DECLARE)
            {
                // We're in a <<declare>> statement, and we saw a FUNC_ID
                // immediately after the 'declare' keyword. The user forgot to
                // include a '$' before the variable name (which is why the lexer
                // matched a function ID, rather than a variable ID).
                msg = "Variable names need to start with a $";
            }

            msg ??= $"Unexpected \"{e.OffendingToken.Text}\" while reading {ErrorUtility.GetFriendlyNameForRuleContext(recognizer.RuleContext, true)}";

            return msg;
        }

        internal static string ReportInputMismatch(Parser recognizer, InputMismatchException e)
        {
            string? msg = null;

            switch (recognizer.RuleContext)
            {
                case YarnSpinnerParser.If_statementContext ifStatement:
                    if (e.OffendingToken.Type == YarnSpinnerLexer.BODY_END)
                    {
                        // We have exited a body in the middle of an if
                        // statement. The programmer forgot to include an
                        // <<endif>>.
                        msg = $"Expected an <<endif>> to match the <<if>> statement on line {ifStatement.Start.Line}";
                    }
                    else if (e.OffendingToken.Type == YarnSpinnerLexer.COMMAND_ELSE && recognizer.GetExpectedTokens().Contains(YarnSpinnerLexer.COMMAND_ENDIF))
                    {
                        // We saw an else, but we expected to see an endif. The
                        // programmer wrote an additional <<else>>.
                        msg = $"More than one <<else>> statement in an <<if>> statement isn't allowed";
                    }

                    break;
                case YarnSpinnerParser.VariableContext _:
                    if (e.OffendingToken.Type == YarnSpinnerLexer.FUNC_ID)
                    {
                        // We're parsing a variable (which starts with a '$'),
                        // but we encountered a FUNC_ID (which doesn't). The
                        // programmer forgot to include the '$'.
                        msg = "Variable names need to start with a $";
                    }

                    break;
            }

            msg ??= $"Unexpected \"{e.OffendingToken.Text}\" while reading {ErrorUtility.GetFriendlyNameForRuleContext(recognizer.RuleContext, true)}";

            return msg;
        }

        internal static bool IsInsideRule<TRuleType>(Parser recognizer) where TRuleType : RuleContext
        {
            RuleContext currentContext = recognizer.RuleContext;

            while (currentContext != null)
            {
                if (currentContext.GetType() == typeof(TRuleType))
                {
                    return true;
                }

                currentContext = currentContext.Parent;
            }

            return false;
        }
        internal static TRuleType? GetEnclosingRule<TRuleType>(Parser recognizer) where TRuleType : RuleContext
        {
            RuleContext currentContext = recognizer.RuleContext;

            while (currentContext != null)
            {
                if (currentContext.GetType() == typeof(TRuleType))
                {
                    return currentContext as TRuleType;
                }

                currentContext = currentContext.Parent;
            }

            return null;
        }
        internal static string GetFriendlyNameForRuleContext(RuleContext context, bool withArticle = false)
        {
            string ruleName = YarnSpinnerParser.ruleNames[context.RuleIndex];

            string friendlyName = ruleName.Replace("_", " ");

            if (withArticle)
            {
                // If the friendly name's first character is a vowel, the
                // article is 'an'; otherwise, 'a'.
                char firstLetter = System.Linq.Enumerable.First(friendlyName);

                string article;

                char[] englishVowels = new[] { 'a', 'e', 'i', 'o', 'u' };

                article = System.Linq.Enumerable.Contains(englishVowels, firstLetter) ? "an" : "a";

                return $"{article} {friendlyName}";
            }
            else
            {
                return friendlyName;
            }
        }

        // walks backwards along the token stream until we hit one of the terminators in the provided set
        // then it returns the token immediately before hitting the terminator
        // if we walk backwards to depth and still haven't hit a terminator token we give up
        internal static IToken? WalkBackwardsUntilTerminator(ITokenStream tokens, HashSet<int> terminators, bool inclusive = false, int depth = 10)
        {
            int rollingTokenIndex = -1;
            bool foundToken = false;
            for (int i = 0; i < depth; i++)
            {
                var token = tokens.LA(-1 -i);
                if (terminators.Contains(token))
                {
                    foundToken = true;
                    if (inclusive)
                    {
                        rollingTokenIndex = i;
                    }
                    break;
                }
                rollingTokenIndex = i;
            }

            if (rollingTokenIndex == -1 || !foundToken)
            {
                return null;
            }

            return tokens.LT(-1 - rollingTokenIndex);
        }
        internal static IToken? WalkForwardsUntilTerminator(ITokenStream tokens, HashSet<int> terminators, bool inclusive = false, int depth = 10)
        {
            int rollingTokenIndex = -1;
            bool foundToken = false;
            for (int i = 0; i < depth; i++)
            {
                var token = tokens.LA(i + 2);
                if (terminators.Contains(token))
                {
                    foundToken = true;
                    if (inclusive)
                    {
                        rollingTokenIndex = i;
                    }
                    break;
                }
                rollingTokenIndex = i;
            }

            if (rollingTokenIndex == -1 || !foundToken)
            {
                return null;
            }
            
            return tokens.LT(rollingTokenIndex + 2);
        }
        internal static (Range range, string tokenText) DiagnosticInfo(Parser parser, HashSet<int> endTerminal, int line, int charPositionInLine)
        {
            var stopLine = parser.RuleContext.Stop?.Line - 1 ?? line - 1;
            var stopColumn = charPositionInLine;
            var text = parser.RuleContext.GetText();

            // we don't really have an end position
            if (parser.RuleContext.Stop == null)
            {
                // we don't have a valid stop token
                // so we don't know the range of the statement
                // which means we need to find it ourselves
                // this also means the text is invalid and we'll have to get that ourselves
                var token = ErrorUtility.WalkForwardsUntilTerminator(parser.TokenStream, endTerminal, true);
                if (token == null)
                {
                    // this means we couldn't walk to the end of the token
                    // basically have to just give up at this point
                    text = "<unknown>";
                    stopColumn = charPositionInLine;
                }
                else
                {
                    var interval = new Antlr4.Runtime.Misc.Interval(parser.RuleContext.Start.StartIndex, token.StopIndex);
                    text = parser.RuleContext.Start.InputStream.GetText(interval);
                    stopColumn = parser.RuleContext.Start.Column + text.Length;
                }
            }

            var range = new Range(line -1, parser.RuleContext.Start.Column, stopLine, stopColumn);
            return (range, text);
        }
        internal static string LineOfCurrentToken(Parser parser)
        {
            return LineOfCurrentToken(parser.TokenStream, parser.CurrentToken.InputStream);
        }
        internal static string LineOfCurrentToken(ITokenStream tokenStream, ICharStream inputStream)
        {
            var line = string.Empty;

            var leftMostTerminators = new HashSet<int>
            {
                YarnSpinnerLexer.NEWLINE,
                YarnSpinnerLexer.BODY_START,
            };
            var rightMostTerminators = new HashSet<int>
            {
                YarnSpinnerLexer.NEWLINE,
                YarnSpinnerLexer.BODY_END,
            };

            int leftMostNewLine = ErrorUtility.WalkBackwardsUntilTerminator(tokenStream, leftMostTerminators)?.StartIndex ?? -1;
            int rightMostNewLine = ErrorUtility.WalkForwardsUntilTerminator(tokenStream, rightMostTerminators)?.StopIndex ?? -1;

            if (leftMostNewLine > -1 && rightMostNewLine > -1)
            {
                var interval = new Antlr4.Runtime.Misc.Interval(leftMostNewLine, rightMostNewLine);
                line = inputStream.GetText(interval);
            }

            return line;
        }

        internal static string? GenerateContextMessage(string line, int start, int stop)
        {
            // this generates strings like the following:
            // this is the line before a command <<after command>>
            //                                   ^^^^^^^^^^^^^^^^^

            // just quickly doing some bounds checks
            // we need the start to be <= stop
            // and we need the start and stop to be within the range of the string
            if (start > stop)
            {
                return null;
            }
            if (start < 0)
            {
                return null;
            }
            if (stop > line.Length)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(line);
            for (int i = 0; i < line.Length; i++)
            {
                if (i >= start && i <= stop)
                {
                    builder.Append('^');
                }
                else
                {
                    builder.Append(' ');
                }
            }
            return builder.ToString();
        }
    }
}
