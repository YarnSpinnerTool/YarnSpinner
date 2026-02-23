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

    internal sealed class LexerErrorListener : IAntlrErrorListener<int>
    {
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        private readonly string fileName;

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        public LexerErrorListener(string fileName) : base()
        {
            this.fileName = fileName;
        }

        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
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
                }
                else
                {
                    diagnostic.Code = DiagnosticDescriptor.SyntaxError.Code;
                }
            }

            this.diagnostics.Add(diagnostic);
        }
    }

    internal sealed class ParserErrorListener : BaseErrorListener
    {
        private readonly List<Diagnostic> diagnostics = new List<Diagnostic>();
        private readonly string fileName;

        public IEnumerable<Diagnostic> Diagnostics => this.diagnostics;

        public ParserErrorListener(string fileName) : base()
        {
            this.fileName = fileName;
        }

        public override void SyntaxError(System.IO.TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            Range range = new Range(line - 1, charPositionInLine, line - 1, charPositionInLine + 1);

            var diagnostic = new Diagnostic(this.fileName, range, msg);

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
}
