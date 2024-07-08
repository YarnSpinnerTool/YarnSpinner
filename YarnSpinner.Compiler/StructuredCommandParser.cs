using System.Collections.Generic;
using System.Linq;

namespace Yarn.Compiler
{
    /// <summary>
    /// Contains methods for parsing structured commands.
    /// </summary>
    public static class StructuredCommandParser
    {
        /// <summary>
        /// Represents the result of a parse operation containing both the
        /// parsed context and diagnostics.
        /// </summary>
        public struct ParseResult
        {
            /// <summary>
            /// The parsed structured command context.
            /// </summary>
            public YarnSpinnerParser.Structured_commandContext context;

            /// <summary>
            /// A collection of diagnostics produced during parsing.
            /// </summary>
            public IEnumerable<Diagnostic> diagnostics;

            /// <summary>
            /// Gets a value indicating whether the parse result is valid (that
            /// is, it contains no parse errors.)
            /// </summary>
            public readonly bool IsValid => diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);
        }

        /// <summary>
        /// Parses the given source string into a structured command.
        /// </summary>
        /// <param name="source">The string containing the possible structured
        /// command text to be parsed.</param>
        /// <returns>A <see cref="ParseResult"/> object encapsulating the parsed
        /// context and any diagnostics.</returns>
        public static ParseResult ParseStructuredCommand(string source)
        {
            // Create an input character stream from the provided source string.
            Antlr4.Runtime.ICharStream input = Antlr4.Runtime.CharStreams.fromString(source);

            // Initialize a lexer, and set it to ExpressionMode.
            var lexer = new YarnSpinnerLexer(input);
            lexer.PushMode(YarnSpinnerLexer.ExpressionMode);

            // Create a token stream from the lexer output.
            var tokens = new Antlr4.Runtime.CommonTokenStream(lexer);

            // Initialize a parser for structured commands, with the token
            // stream as its input.
            var parser = new YarnSpinnerParser(tokens);

            // Define custom error listeners to capture lexical and parsing
            // errors during these phases.
            LexerErrorListener lexerErrorListener = new LexerErrorListener("<generated>");
            ParserErrorListener parseErrorListener = new ParserErrorListener("<generated>");

            // Remove default error listeners from lexer and parser to avoid
            // automatic error reporting, then add our custom listeners.
            lexer.RemoveErrorListeners();
            parser.RemoveErrorListeners();
            lexer.AddErrorListener(lexerErrorListener);
            parser.AddErrorListener(parseErrorListener);

            // Parse the source string into a structured command context. During
            // this parse, the lexer and parser error listeners will collect any
            // diagnostics.
            var command = parser.structured_command();

            // Return the parse result encapsulating both the parsed context and
            // all collected diagnostics from errors listeners.
            return new ParseResult
            {
                context = command,
                diagnostics = lexerErrorListener.Diagnostics.Concat(parseErrorListener.Diagnostics),
            };
        }
    }
}

