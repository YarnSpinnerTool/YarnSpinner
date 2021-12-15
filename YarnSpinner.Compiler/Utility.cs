namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Utility methods for working with line tags.
    /// </summary>
    public static class Utility
    {
        private static readonly Random Random = new Random();

        /// <summary>
        /// Generates a Yarn script that contains a node that declares
        /// variables.
        /// </summary>
        /// <remarks>This method is intended to be called by tools that let
        /// the user manage variable declarations. Such tools can read the
        /// existing variable declarations in from a script (by compiling
        /// the script with the `DeclarationsOnly` <see
        /// cref="CompilationJob.CompilationType"/>), allow the user to
        /// make changes, and then write the changes to disk by calling
        /// this method and saving the results.</remarks>
        /// <param name="declarations">The collection of <see
        /// cref="Declaration"/> objects to include in the output.</param>
        /// <param name="title">The title of the node that should be
        /// generated.</param>
        /// <param name="tags">The collection of tags that should be
        /// generated for the node. If this is <see langword="null"/>, no
        /// tags will be generated.</param>
        /// <param name="headers">The collection of additional headers that
        /// should be generated for the node. If this is <see
        /// langword="null"/>, no additional headers will be
        /// generated.</param>
        /// <returns>A string containing a Yarn script that declares the
        /// specified variables.</returns>
        /// <throws cref="ArgumentOutOfRangeException">Thrown when any of
        /// the <see cref="Declaration"/> objects in <paramref
        /// name="declarations"/> is not a variable declaration, or if the
        /// <see cref="Declaration.ReturnType"/> of any of the declarations
        /// is an invalid value.</throws>
        public static string GenerateYarnFileWithDeclarations(
            IEnumerable<Yarn.Compiler.Declaration> declarations,
            string title = "Program",
            IEnumerable<string> tags = null,
            IDictionary<string, string> headers = null)
        {
            var stringBuilder = new StringBuilder();

            stringBuilder.AppendLine($"title: {title}");

            if (tags != null)
            {
                stringBuilder.AppendLine($"tags: {string.Join(" ", tags)}");
            }

            if (headers != null)
            {
                foreach (var kvp in headers)
                {
                    stringBuilder.AppendLine($"{kvp.Key}: {kvp.Value}");
                }
            }

            stringBuilder.AppendLine("---");

            int count = 0;

            foreach (var decl in declarations)
            {
                if (decl.Type is FunctionType)
                {
                    throw new ArgumentOutOfRangeException($"Declaration {decl.Name} is a {decl.Type.Name}; it must be a variable.");
                }

                if (string.IsNullOrEmpty(decl.Description) == false)
                {
                    if (count > 0) {
                        // Insert a blank line above this comment, for readibility
                        stringBuilder.AppendLine();
                    }
                    stringBuilder.AppendLine($"/// {decl.Description}");
                }

                stringBuilder.Append($"<<declare {decl.Name} = ");

                if (decl.Type == BuiltinTypes.Number) {
                    stringBuilder.Append(decl.DefaultValue);
                } else if (decl.Type == BuiltinTypes.String) {
                    stringBuilder.Append('"' + (string)decl.DefaultValue + '"');
                } else if (decl.Type == BuiltinTypes.Boolean) {
                    stringBuilder.Append((bool)decl.DefaultValue ? "true" : "false");
                } else {
                    throw new ArgumentOutOfRangeException($"Declaration {decl.Name}'s type must not be {decl.Type.Name}.");
                }

                stringBuilder.AppendLine(">>");

                count += 1;
            }

            stringBuilder.AppendLine("===");

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Given Yarn source code, adds line tags to the ends of all lines
        /// that need one and do not already have one.
        /// </summary>
        /// <remarks>This method ensures that it does not generate line
        /// tags that are already present in the file, or present in the
        /// <paramref name="existingLineTags"/> collection.
        ///
        /// Line tags are added to any line of source code that contains
        /// user-visible text: lines, options, and shortcut options.
        /// </remarks>
        /// <param name="contents">The source code to add line tags
        /// to.</param>
        /// <param name="existingLineTags">The collection of line tags
        /// already exist elsewhere in the source code; the newly added
        /// line tags will not be duplicates of any in this
        /// collection.</param>
        /// <returns>The modified source code, with line tags
        /// added.</returns>
        public static string AddTagsToLines(string contents, ICollection<string> existingLineTags = null)
        {
            // First, get the parse tree for this source code.
            var (parseSource, diagnostics) = ParseSource(contents);

            // Were there any error-level diagnostics?
            if (diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error)) {
                // We encountered a parse error. Bail here; we aren't confident
                // in our ability to correctly insert a line tag.
                return null;
            }

            // Make sure we have a list of line tags to work with.
            if (existingLineTags == null) {
                existingLineTags = new List<string>();
            }

            // Create the line listener, which will produce TextReplacements for
            // each new line tag.
            var untaggedLineListener = new UntaggedLineListener(new List<string>(existingLineTags), parseSource.Tokens);

            // Walk the tree with this listener, and generate text replacements
            // containing line tags.
            var walker = new ParseTreeWalker();
            walker.Walk(untaggedLineListener, parseSource.Tree);

            // Apply these text replacements to the original source and return
            // it.
            return Upgrader.LanguageUpgrader.ApplyReplacements(
                contents, 
                untaggedLineListener.Replacements
            );
        }

        /// <summary>
        /// </summary>
        /// <param name="source">The source code to parse.</param>
        /// <returns></returns>
        public static (FileParseResult, IEnumerable<Diagnostic>) ParseSource(string source)
        {
            var diagnostics = new List<Diagnostic>();
            var result = Compiler.ParseSyntaxTree("<input>", source, ref diagnostics);

            return (result, diagnostics);
        }

        /// <summary>
        /// Generates a new unique line tag that is not present in
        /// `existingKeys`.
        /// </summary>
        /// <param name="existingKeys">The collection of keys that should
        /// be considered when generating a new, unique line tag.</param>
        /// <returns>A unique line tag that is not already present in
        /// `existingKeys`.</returns>
        private static string GenerateString(ICollection<string> existingKeys)
        {
            string tag;
            do
            {
                tag = string.Format(CultureInfo.InvariantCulture, "line:{0:x7}", Random.Next(0x1000000));
            }
            while (existingKeys.Contains(tag));

            return tag;
        }

        /// <summary>
        /// An <see cref="IYarnSpinnerParserListener"/> that produces line tags.
        /// </summary>
        private class UntaggedLineListener : YarnSpinnerParserBaseListener
        {
            /// <summary>
            /// A collection of <see cref="Upgrader.TextReplacement"/> objects,
            /// each containing a line tag to add.
            /// </summary>
            public List<Upgrader.TextReplacement> Replacements { get; private set; }
            
            private readonly IList<string> existingStrings;

            private readonly CommonTokenStream TokenStream;

            /// <summary>
            /// Creates a new instance of <see cref="UntaggedLineListener"/>.
            /// </summary>
            /// <param name="existingStrings">A collection of line IDs that
            /// should not be used. This list will be added to as this instance
            /// works.</param>
            /// <param name="tokenStream">The token stream used to generate the
            /// <see cref="IParseTree"/> this instance is operating on.</param>
            public UntaggedLineListener(IList<string> existingStrings, CommonTokenStream tokenStream) {
                this.Replacements = new List<Upgrader.TextReplacement>();
                this.existingStrings = existingStrings;
                this.TokenStream = tokenStream;
            }

            /// <inheritdoc/>
            public override void ExitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
            {
                // We're looking at a complete line statement.

                // First, figure out if this line statement already has a line
                // tag. Start by taking the hashtags...
                var hashtags = context.hashtag();

                // Get the text for all of these hashtags...
                var texts = StringTableGeneratorVisitor.GetHashtagTexts(hashtags);

                // And then look for a line ID hashtag.
                foreach (var text in texts) {
                    if (text.StartsWith("line:")) {
                        // This line contains a line code. Nothing left to do.
                        return;
                    }
                }
                
                // Find the index of the first token on the default channel to
                // the left of the newline.
                var previousTokenIndex = IndexOfPreviousTokenOnChannel(
                    TokenStream, 
                    context.NEWLINE().Symbol.TokenIndex, 
                    YarnSpinnerLexer.DefaultTokenChannel
                );

                // Did we find one?
                if (previousTokenIndex == -1) {
                    // No token was found before this newline. This is an
                    // internal error - there must be at least one symbol
                    // besides the terminating newline.
                    throw new InvalidOperationException($"Internal error: failed to find any tokens before the newline in line statement on line {context.Start.Line}");
                }

                // Get the token at this index. We'll put our tag after it.
                var previousToken = TokenStream.Get(previousTokenIndex);

                // Generate a new, unique line ID.
                string newLineID = Utility.GenerateString(existingStrings);

                // Record that we've used this new line ID, so that we don't
                // accidentally use it twice.
                existingStrings.Add(newLineID);
                
                // Create a text replacement that inserts a space followed by
                // the line tag at the end of the line.
                var replacement = new Upgrader.TextReplacement() {
                    Start = previousToken.StopIndex + 1,
                    StartLine = previousToken.Line,
                    OriginalText = "",
                    ReplacementText = $" #{newLineID} ",
                    Comment = "Added line tag"
                };

                // Add this replacement to the list.
                this.Replacements.Add(replacement);
            }

            /// <summary>
            /// Gets the index of the first token to the left of the token at
            /// <paramref name="index"/> that's on <paramref name="channel"/>.
            /// If there are no tokens that match, return -1.
            /// </summary>
            /// <param name="tokenStream">The token stream to search
            /// within.</param>
            /// <param name="index">The index of the token to start searching
            /// from.</param>
            /// <param name="channel">The channel to find tokens on.</param>
            /// <returns>The index of the first token before the token at
            /// <paramref name="index"/> that is on the channel <paramref
            /// name="channel"/>. If none is found, returns -1. If <paramref
            /// name="index"/> is beyond the size of <paramref
            /// name="tokenStream"/>, returns the index of the last token in the
            /// stream.</returns>
            private static int IndexOfPreviousTokenOnChannel(CommonTokenStream tokenStream, int index, int channel)
            {

                // Are we beyond the list of tokens?
                if (index >= tokenStream.Size)
                {
                    // Return the final token in the channel, which will be an
                    // EOF.
                    return tokenStream.Size - 1;
                }

                // 'index' is the token we want to start searching from. We want
                // to find items before it, so start looking from the token
                // before it.
                var currentIndex = index -= 1;

                // Walk backwards through the tokens list.
                while (currentIndex >= 0)
                {
                    IToken token = tokenStream.Get(currentIndex);

                    // Is this token on the channel we're looking for?
                    if (token.Channel == channel)
                    {
                        // We're done - we found one! Return it.
                        return currentIndex;
                    }
                    currentIndex -= 1;
                }

                // We found nothing. Return the 'not found' value.
                return -1;
            }
        }
    }
}
