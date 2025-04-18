// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

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
        /// <remarks>This method is intended to be called by tools that let the
        /// user manage variable declarations. Such tools can read the existing
        /// variable declarations in from a script (by compiling the script with
        /// the <see cref="CompilationJob.CompilationType"/> value set to  <see
        /// cref="CompilationJob.Type.TypeCheck"/>), allow the user to
        /// make changes, and then write the changes to disk by calling this
        /// method and saving the results.</remarks>
        /// <param name="declarations">The collection of <see
        /// cref="Declaration"/> objects to include in the output.</param>
        /// <param name="title">The title of the node that should be
        /// generated.</param>
        /// <param name="tags">The collection of tags that should be generated
        /// for the node. If this is <see langword="null"/>, no tags will be
        /// generated.</param>
        /// <param name="headers">The collection of additional headers that
        /// should be generated for the node. If this is <see langword="null"/>,
        /// no additional headers will be generated.</param>
        /// <returns>A string containing a Yarn script that declares the
        /// specified variables.</returns>
        /// <throws cref="ArgumentOutOfRangeException">Thrown when any of the
        /// <see cref="Declaration"/> objects in <paramref name="declarations"/>
        /// is not a variable declaration, or if the <see
        /// cref="Declaration.Type"/> of any of the declarations is an
        /// invalid value.</throws>
        public static string GenerateYarnFileWithDeclarations(
            IEnumerable<Yarn.Compiler.Declaration> declarations,
            string title = "Program",
            IEnumerable<string>? tags = null,
            IDictionary<string, string>? headers = null)
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
                    // Ignore function types; they can't be declared in Yarn
                    // script
                    continue;
                }

                if (string.IsNullOrEmpty(decl.Description) == false)
                {
                    if (count > 0)
                    {
                        // Insert a blank line above this comment, for readibility
                        stringBuilder.AppendLine();
                    }
                    stringBuilder.AppendLine($"/// {decl.Description}");
                }

                stringBuilder.Append($"<<declare {decl.Name} = ");

                if (decl.Type == Types.Number)
                {
                    stringBuilder.Append(decl.DefaultValue);
                }
                else if (decl.Type == Types.String)
                {
                    stringBuilder.Append('"' + (string)(decl.DefaultValue ?? string.Empty) + '"');
                }
                else if (decl.Type == Types.Boolean)
                {
                    stringBuilder.Append((bool)(decl.DefaultValue ?? false) ? "true" : "false");
                }
                else
                {
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
        /// <remarks><para>
        /// This method ensures that it does not generate line
        /// tags that are already present in the file, or present in the
        /// <paramref name="existingLineTags"/> collection.
        /// </para>
        /// <para>
        /// Line tags are added to any line of source code that contains
        /// user-visible text: lines, options, and shortcut options.
        /// </para>
        /// </remarks>
        /// <param name="contents">The source code to add line tags
        /// to.</param>
        /// <param name="existingLineTags">The collection of line tags
        /// already exist elsewhere in the source code; the newly added
        /// line tags will not be duplicates of any in this
        /// collection.</param>
        /// <returns>The modified source code, with line tags
        /// added.</returns>
        [Obsolete("This method doesn't return the new tags, just the modified text which can cause issues with multiple files. Please use TagLines instead")]
        public static string AddTagsToLines(string contents, ICollection<string>? existingLineTags = null)
        {
            // First, get the parse tree for this source code.
            var (parseSource, diagnostics) = ParseSource(contents);

            // Were there any error-level diagnostics?
            if (diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                // We encountered a parse error. Bail here; we aren't confident
                // in our ability to correctly insert a line tag.
                return contents;
            }

            // Make sure we have a list of line tags to work with.
            if (existingLineTags == null)
            {
                existingLineTags = new List<string>();
            }

            // Create the line listener, which will produce TextReplacements for
            // each new line tag.
            var untaggedLineListener = new UntaggedLineListener(new List<string>(existingLineTags), parseSource.Tokens);

            // Walk the tree with this listener, and generate text replacements
            // containing line tags.
            var walker = new Antlr4.Runtime.Tree.ParseTreeWalker();
            walker.Walk(untaggedLineListener, parseSource.Tree);

            // Apply these text replacements to the original source and return
            // it.
            return untaggedLineListener.RewrittenNodes().ModifiedSource;
        }

        /// <summary>
        /// Given Yarn source code, adds line tags to the ends of all lines that
        /// need one and do not already have one.
        /// </summary>
        /// <remarks><para>
        /// This method ensures that it does not generate line tags that are
        /// already present in the file, or present in the <paramref
        /// name="existingLineTags"/> collection.
        /// </para>
        /// <para>
        /// Line tags are added to any line of source code that contains
        /// user-visible text: lines, options, and shortcut options.
        /// </para>
        /// </remarks>
        /// <param name="contents">The source code to add line tags to.</param>
        /// <param name="existingLineTags">The collection of line tags already
        /// exist elsewhere in the source code; the newly added line tags will
        /// not be duplicates of any in this collection.</param>
        /// <returns>Tuple of the modified source code, with line tags added and
        /// an updated list of line IDs.
        /// </returns>
        public static (string ModifiedSource, IList<string> LineIDs) TagLines(string contents, ICollection<string>? existingLineTags = null)
        {
            // First, get the parse tree for this source code.
            var (parseSource, diagnostics) = ParseSource(contents);

            // Were there any error-level diagnostics?
            if (diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error))
            {
                // We encountered a parse error. Bail here; we aren't confident
                // in our ability to correctly insert a line tag.
                return (contents, existingLineTags.ToList() ?? new List<string>());
            }

            // Make sure we have a list of line tags to work with.
            existingLineTags ??= new List<string>();

            // Create the line listener, which will produce TextReplacements for
            // each new line tag.
            var untaggedLineListener = new UntaggedLineListener(new List<string>(existingLineTags), parseSource.Tokens);

            // Walk the tree with this listener, and generate text replacements
            // containing line tags.
            var walker = new Antlr4.Runtime.Tree.ParseTreeWalker();
            walker.Walk(untaggedLineListener, parseSource.Tree);

            // Apply these text replacements to the original source and return
            // it.
            return untaggedLineListener.RewrittenNodes();
        }

        /// <summary>
        /// Parses a string of Yarn source code, and produces a FileParseResult
        /// and (if there were any problems) a collection of diagnostics.
        /// </summary>
        /// <param name="source">The source code to parse.</param>
        /// <returns>A tuple containing a <see cref="FileParseResult"/> that
        /// stores the parse tree and tokens, and a collection of <see
        /// cref="Diagnostic"/> objects that describe problems in the source
        /// code.</returns>
        public static (FileParseResult, IEnumerable<Diagnostic>) ParseSource(string source)
        {
            var diagnostics = new List<Diagnostic>();
            var result = Compiler.ParseSyntaxTree("<input>", source, ref diagnostics);

            return (result, diagnostics);
        }

        /// <summary>
        /// Generates a new unique line tag that is not present in
        /// <c>existingKeys</c>.
        /// </summary>
        /// <param name="existingKeys">The collection of keys that should be
        /// considered when generating a new, unique line tag.</param>
        /// <returns>A unique line tag that is not already present in <paramref
        /// name="existingKeys"/>.</returns>
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
            private readonly IList<string> knownLineIDs;

            private readonly CommonTokenStream TokenStream;

            private TokenStreamRewriter rewriter;

            /// <summary>
            /// Initializes a new instance of the <see
            /// cref="UntaggedLineListener"/> class.
            /// </summary>
            /// <param name="existingStrings">A collection of line IDs that
            /// should not be used. This list will be added to as this instance
            /// works.</param>
            /// <param name="tokenStream">The token stream used to generate the
            /// <see cref="IParseTree"/> this instance is operating on.</param>
            public UntaggedLineListener(IList<string> existingStrings, CommonTokenStream tokenStream)
            {
                this.knownLineIDs = existingStrings;
                this.TokenStream = tokenStream;
                this.rewriter = new TokenStreamRewriter(TokenStream);
            }

            /// <inheritdoc/>
            public override void ExitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
            {
                // We're looking at a complete line statement.

                // First, figure out if this line statement already has a line
                // tag. Start by taking the hashtags...
                var hashtags = context.hashtag();

                // Get the text for all of these hashtags...
                var hashtagTexts = StringTableGeneratorVisitor.GetHashtagTexts(hashtags);

                // And then look for a line ID hashtag.
                foreach (var hashtag in hashtagTexts)
                {
                    if (hashtag.StartsWith("line:"))
                    {
                        // This line contains a line code. Nothing left to do.
                        return;
                    }
                    if (hashtag.StartsWith("shadow:"))
                    {
                        // This line contains a shadow line tag, which aren't
                        // allowed to have line IDs of their own.
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
                if (previousTokenIndex == -1)
                {
                    // No token was found before this newline. This is an
                    // internal error - there must be at least one symbol
                    // besides the terminating newline.
                    throw new InvalidOperationException($"Internal error: failed to find any tokens before the newline in line statement on line {context.Start.Line}");
                }

                // Get the token at this index. We'll put our tag after it.
                var previousToken = TokenStream.Get(previousTokenIndex);

                // Generate a new, unique line ID.
                string newLineID = Utility.GenerateString(knownLineIDs);

                // Record that we've used this new line ID, so that we don't
                // accidentally use it twice.
                knownLineIDs.Add(newLineID);

                this.rewriter.InsertAfter(previousToken, $" #{newLineID} ");
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

            public (string ModifiedSource, IList<string> KnownLineIDs) RewrittenNodes()
            {
                return (this.rewriter.GetText(), knownLineIDs);
            }
        }

        /// <summary>
        /// Gets the collection of contiguous runs of lines in the provided
        /// nodes. Each run of lines is guaranteed to run to completion once
        /// entered.
        /// </summary>
        /// <param name="nodes">The nodes to get string blocks for.</param>
        /// <returns>A collection of runs of lines.</returns>
        public static List<List<string>> ExtractStringBlocks(IEnumerable<Node> nodes, ProjectDebugInfo projectDebugInfo)
        {
            List<List<string>> lineBlocks = new List<List<string>>();

            foreach (var node in nodes)
            {
                var nodeDebugInfo = projectDebugInfo.Nodes.Single(n => n.NodeName == node.Name);
                var blocks = InstructionCollectionExtensions.GetBasicBlocks(node, nodeDebugInfo);
                var visited = new HashSet<string>();
                foreach (var block in blocks)
                {
                    RunBlock(block, blocks, visited);
                }
            }

            void RunBlock(BasicBlock block, IEnumerable<BasicBlock> blocks, HashSet<string> visited, string? openingLineID = null)
            {
                if (block.PlayerVisibleContent.Count() == 0)
                {
                    // skipping this block because it has no user content within
                    return;
                }

                if (visited.Contains(block.Name))
                {
                    // we have already visited this one so we can go on without it
                    return;
                }
                visited.Add(block.Name);

                var runOfLines = new List<string>();

                // if we are given an opening line ID we need to add that in at the top
                // this handles the case where we want options to open the set associated lines
                if (openingLineID != null && !string.IsNullOrEmpty(openingLineID))
                {
                    runOfLines.Add(openingLineID);
                }

                foreach (var content in block.PlayerVisibleContent)
                {
                    // I really really dislike using objects in this manner
                    // it just feels oh so very strange to me
                    if (content is BasicBlock.LineElement line)
                    {
                        // lines just get added to the current collection of content
                        runOfLines.Add(line.LineID);
                    }
                    else if (content is BasicBlock.OptionsElement options)
                    {
                        // options are special cased because of how they work
                        // an option will always be put into a block by themselves and any child content they have
                        // so this means we close off the current run of content and add it to the overall container
                        // and then make a new one for each option in the option set
                        if (runOfLines.Count() > 0)
                        {
                            lineBlocks.Add(runOfLines);
                            runOfLines = new List<string>();
                        }

                        var jumpOptions = new Dictionary<string, BasicBlock>();
                        foreach (var option in options.Options)
                        {
                            var destination = blocks.First(b => b.FirstInstructionIndex == option.Destination);
                            if (destination != null && destination.PlayerVisibleContent.Count() > 0)
                            {
                                // there is a valid jump we need to deal with
                                // we store this and will handle it later
                                jumpOptions[option.LineID] = destination;
                            }
                            else
                            {
                                // there is no jump for this option
                                // we just add it to the collection and continue
                                runOfLines.Add(option.LineID);
                                lineBlocks.Add(runOfLines);
                                runOfLines = new List<string>();
                            }
                        }

                        // now any options without a child block have been handled we need to handle those with children
                        // in that case we want to run through each of those as if they are a new block but with the option at the top
                        foreach (var pair in jumpOptions)
                        {
                            RunBlock(pair.Value, blocks, visited, pair.Key);
                        }
                    }
                    else if (content is BasicBlock.CommandElement)
                    {
                        // skipping commands as they aren't lines
                        continue;
                    }
                    else
                    {
                        // encountered an unknown type, this is an error
                        // but for now we will skip over it
                        continue;
                    }
                }

                if (runOfLines.Count() > 0)
                {
                    lineBlocks.Add(runOfLines);
                }
            }

            return lineBlocks;
        }

        /// <summary>
        /// Finds and collates every jump in every node.
        /// </summary>
        /// <param name="YarnFileContents">The collection of yarn file content to parse and walk</param>
        /// <returns>A list of lists of GraphingNode each containing a node, its jumps, and any positional info.</returns>
        public static List<List<GraphingNode>> DetermineNodeConnections(string[] YarnFileContents)
        {
            var walker = new Antlr4.Runtime.Tree.ParseTreeWalker();

            // alright so the change is instead of making it a list
            // we make it a list of lists
            List<List<GraphingNode>> cluster = new List<List<GraphingNode>>();
            foreach (var contents in YarnFileContents)
            {
                var (parseSource, diagnostics) = ParseSource(contents);

                List<GraphingNode> connections = new List<GraphingNode>();
                var jumpListener = new JumpGraphListener(connections);
                walker.Walk(jumpListener, parseSource.Tree);

                cluster.Add(connections);
            }

            return cluster;
        }

        /// <summary>
        /// Gets a string containing a representation of the compiled bytecode
        /// for a <see cref="Program"/>.
        /// </summary>
        /// <param name="program"></param>
        /// <param name="l"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        public static string GetCompiledCodeAsString(Program program, Library? l = null, CompilationResult? result = null)
        {
            return program.DumpCode(l, result);
        }

        /// <summary>
        /// Returns an <see cref="IYarnValue"/> representation of the provided
        /// value.
        /// </summary>
        /// <param name="clrValue">The value to get a Yarn representation
        /// of.</param>
        /// <returns>An <see cref="IYarnValue"/> representation of <paramref
        /// name="clrValue"/>.</returns>
        public static IYarnValue? GetYarnValue(IConvertible clrValue)
        {
            if (Types.TypeMappings.TryGetValue(clrValue.GetType(), out var yarnType))
            {
                Value yarnValue = new Value(yarnType, clrValue);

                return yarnValue;
            }
            else
            {
                return null;
            }
        }
    }

    public struct GraphingNode
    {
        /// <summary>
        /// The name of the node.
        /// </summary>
        public string node;

        /// <summary>
        /// The list of nodes that this node jumps to.
        /// </summary>
        public string[] jumps;

        /// <summary>
        /// <see langword="true"/> if this <see cref="GraphingNode"/>'s <see
        /// cref="position"/> field contains valid information.
        /// </summary>
        public bool hasPositionalInformation;

        /// <summary>
        /// The position of this <see cref="GraphingNode"/>. Only valid when
        /// <see cref="hasPositionalInformation"/> is <see langword="true"/>.
        /// </summary>
        public (int x, int y) position;
    }
}
