// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// Extracts node metadata during compilation for language server features.
    /// </summary>
    /// <remarks>
    /// This visitor walks the parse tree and gathers information about nodes including
    /// jumps, function calls, commands, variables, character names, and structural information.
    /// </remarks>
    internal class NodeMetadataVisitor : YarnSpinnerParserBaseVisitor<object>
    {
        private readonly List<NodeMetadata> nodes = new List<NodeMetadata>();
        private NodeMetadata? currentNode = null;
        private readonly string fileUri;
        private int previewLineCount = 0;
        private const int MaxPreviewLines = 3;

        // Track option grouping
        private int currentOptionGroup = 0;
        private Dictionary<int, int> indentToGroupId = new Dictionary<int, int>();
        private Dictionary<int, int> indentToLastLine = new Dictionary<int, int>();
        private Dictionary<int, int> indentToMinSeenSince = new Dictionary<int, int>(); // Track minimum indent seen since last option at each level
        private int lastOptionIndent = -1;
        private int lastLineNumber = -1;

        /// <summary>
        /// Regex for detecting implicit character names in dialogue lines.
        /// </summary>
        /// <remarks>
        /// Matches the pattern "characterName: " at the start of a line.
        /// This uses the same logic as LineParser for consistency.
        /// </remarks>
        private static readonly Regex implicitCharacterRegex = new Regex(@"^[^:]*:\s*");

        public NodeMetadataVisitor(string fileUri)
        {
            this.fileUri = fileUri;
        }

        /// <summary>
        /// Extracts metadata from a parse tree.
        /// </summary>
        public static List<NodeMetadata> Extract(string fileUri, YarnSpinnerParser.DialogueContext dialogueContext)
        {
            var visitor = new NodeMetadataVisitor(fileUri);
            visitor.Visit(dialogueContext);
            return visitor.nodes;
        }

        public override object VisitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            // Start a new node metadata object.
            currentNode = new NodeMetadata
            {
                Uri = fileUri,
                // Get complexity score from the compiler (set by nodeTrackingVisitor or nodeGroupVisitor).
                NodeGroupComplexity = context.ComplexityScore,
                // Header starts at the first delimiter, convert from 1-based to 0-based.
                HeaderStartLine = context.Start.Line - 1
            };

            previewLineCount = 0;

            // Reset option grouping for new node
            currentOptionGroup = 0;
            indentToGroupId = new Dictionary<int, int>();
            indentToLastLine = new Dictionary<int, int>();
            lastOptionIndent = -1;

            // Visit children to extract all the metadata.
            base.VisitNode(context);

            // Only add nodes that have a title.
            if (!string.IsNullOrWhiteSpace(currentNode.Title))
            {
                nodes.Add(currentNode);
            }

            currentNode = null;
            return false;
        }

        public override object VisitTitle_header([NotNull] YarnSpinnerParser.Title_headerContext context)
        {
            if (currentNode != null && context.title != null)
            {
                currentNode.Title = context.title.Text;
                // Capture the line where title is declared, convert from 1-based to 0-based.
                currentNode.TitleLine = context.Start.Line - 1;
            }

            return base.VisitTitle_header(context);
        }

        public override object VisitJumpToNodeName([NotNull] YarnSpinnerParser.JumpToNodeNameContext context)
        {
            if (currentNode == null || context.destination == null)
            {
                return base.VisitJumpToNodeName(context);
            }

            var destinationName = context.destination.Text;
            if (!string.IsNullOrWhiteSpace(destinationName))
            {
                // Use the destination token itself for the range
                // This ensures we highlight exactly where the problematic node name is
                var destinationToken = context.destination;

                currentNode.Jumps.Add(new JumpInfo
                {
                    DestinationTitle = destinationName,
                    Type = JumpType.Jump,
                    Range = Utility.GetRange(destinationToken, destinationToken)
                });
            }

            return base.VisitJumpToNodeName(context);
        }

        public override object VisitDetourToNodeName([NotNull] YarnSpinnerParser.DetourToNodeNameContext context)
        {
            if (currentNode == null || context.destination == null)
            {
                return base.VisitDetourToNodeName(context);
            }

            var destinationName = context.destination.Text;
            if (!string.IsNullOrWhiteSpace(destinationName))
            {
                // Get the range of the entire detour command (from << to >>)
                var commandStart = context.COMMAND_START()?.Symbol;
                var commandEnd = context.COMMAND_END()?.Symbol;

                currentNode.Jumps.Add(new JumpInfo
                {
                    DestinationTitle = destinationName,
                    Type = JumpType.Detour,
                    Range = commandStart != null && commandEnd != null
                        ? Utility.GetRange(commandStart, commandEnd)
                        : Utility.GetRange(context)
                });
            }

            return base.VisitDetourToNodeName(context);
        }

        public override object VisitFunction_call([NotNull] YarnSpinnerParser.Function_callContext context)
        {
            if (currentNode == null)
            {
                return base.VisitFunction_call(context);
            }

            var funcId = context.FUNC_ID();
            if (funcId != null)
            {
                var functionName = funcId.Symbol.Text;
                if (!string.IsNullOrWhiteSpace(functionName) && !currentNode.FunctionCalls.Contains(functionName))
                {
                    currentNode.FunctionCalls.Add(functionName);
                }
            }

            return base.VisitFunction_call(context);
        }

        public override object VisitCommand_statement([NotNull] YarnSpinnerParser.Command_statementContext context)
        {
            if (currentNode == null)
            {
                return base.VisitCommand_statement(context);
            }

            // Get command text from the formatted text.
            var commandFormattedText = context.command_formatted_text();
            if (commandFormattedText != null)
            {
                var commandText = commandFormattedText.GetText();
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    // Extract command name which is the first word.
                    var parts = commandText.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var commandName = parts[0];

                        if (!currentNode.CommandCalls.Contains(commandName))
                        {
                            currentNode.CommandCalls.Add(commandName);
                        }
                    }
                }
            }

            return base.VisitCommand_statement(context);
        }

        public override object VisitVariable([NotNull] YarnSpinnerParser.VariableContext context)
        {
            if (currentNode == null)
            {
                return base.VisitVariable(context);
            }

            var variableName = context.GetText();
            if (!string.IsNullOrWhiteSpace(variableName) && !currentNode.VariableReferences.Contains(variableName))
            {
                currentNode.VariableReferences.Add(variableName);
            }

            return base.VisitVariable(context);
        }

        public override object VisitHeader([NotNull] YarnSpinnerParser.HeaderContext context)
        {
            if (currentNode != null)
            {
                var headerKey = context.header_key?.Text?.ToLowerInvariant();
                var headerValue = context.header_value?.Text;

                // Extract tags from tags header.
                if (headerKey == "tags" && !string.IsNullOrWhiteSpace(headerValue))
                {
                    var tags = headerValue.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    foreach (var tag in tags)
                    {
                        if (!string.IsNullOrWhiteSpace(tag) && !currentNode.Tags.Contains(tag))
                        {
                            currentNode.Tags.Add(tag);
                        }
                    }
                }
            }

            return base.VisitHeader(context);
        }

        public override object VisitBody([NotNull] YarnSpinnerParser.BodyContext context)
        {
            if (currentNode != null && currentNode.BodyStartLine == -1)
            {
                // Body starts after the second delimiter, convert from 1-based to 0-based.
                currentNode.BodyStartLine = context.Start.Line - 1;
                // Body ends at the stop token line, convert from 1-based to 0-based.
                // Note: the stop token might be null if the node is unclosed but we handle that.
                currentNode.BodyEndLine = (context.Stop?.Line ?? context.Start.Line) - 1;
            }

            return base.VisitBody(context);
        }

        public override object VisitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            if (currentNode == null)
            {
                return base.VisitLine_statement(context);
            }

            // Extract character name from lines that match the pattern "characterName: dialogue".
            var lineText = context.line_formatted_text()?.GetText();
            if (!string.IsNullOrWhiteSpace(lineText))
            {
                // Use the same regex logic as LineParser for consistency.
                var match = implicitCharacterRegex.Match(lineText);
                if (match.Success)
                {
                    // Extract character name by removing the colon and trailing whitespace.
                    var characterName = match.Value.TrimEnd(':', ' ', '\t');
                    if (!string.IsNullOrWhiteSpace(characterName))
                    {
                        if (!currentNode.CharacterNames.Contains(characterName))
                        {
                            currentNode.CharacterNames.Add(characterName);
                        }
                    }
                }

                // Build preview text from first few lines.
                if (previewLineCount < MaxPreviewLines)
                {
                    if (previewLineCount > 0)
                    {
                        currentNode.PreviewText += "\n";
                    }
                    // Limit preview line length to avoid huge strings.
                    var previewLine = lineText.Length > 100 ? lineText.Substring(0, 100) + "..." : lineText;
                    currentNode.PreviewText += previewLine;
                    previewLineCount++;
                }
            }

            return base.VisitLine_statement(context);
        }

        public override object VisitShortcut_option([NotNull] YarnSpinnerParser.Shortcut_optionContext context)
        {
            if (currentNode != null)
            {
                currentNode.OptionCount++;

                // Get line number and column (indent) of this option
                int lineNumber = context.Start.Line - 1; // Convert to 0-based
                int indent = context.Start.Column;

                // Get the option text from the line_statement
                var lineStatement = context.line_statement();
                var optionText = lineStatement?.line_formatted_text()?.GetText() ?? string.Empty;

                // Determine group ID for this option
                // Rule: Options at the same indent belong to the same group UNLESS there's a large gap
                // that's NOT caused by nested options (i.e., we haven't gone deeper since last option at this indent)
                int groupId = 0;

                bool wentDeeper = lastOptionIndent > indent;

                if (indentToGroupId.TryGetValue(indent, out int existingGroupId) &&
                    indentToLastLine.TryGetValue(indent, out int lastLine))
                {
                    int lineGap = lineNumber - lastLine;

                    // If we went deeper (nested options), ignore the gap and stay in the same group
                    // Otherwise, only start a new group if gap > 5 (indicating narrator content)
                    if (wentDeeper || lineGap <= 5)
                    {
                        groupId = existingGroupId;
                    }
                    else
                    {
                        // Large gap without going deeper - narrator content between option blocks
                        groupId = currentOptionGroup;
                        indentToGroupId[indent] = groupId;
                        currentOptionGroup++;
                    }
                }
                else
                {
                    // First time seeing this indent
                    groupId = currentOptionGroup;
                    indentToGroupId[indent] = groupId;
                    currentOptionGroup++;
                }

                // Remember this line number for this indent level
                indentToLastLine[indent] = lineNumber;

                // Add option info
                currentNode.Options.Add(new OptionInfo
                {
                    Text = optionText,
                    LineNumber = lineNumber,
                    GroupId = groupId
                });

                // Track this indent as the last seen
                lastOptionIndent = indent;
            }

            return base.VisitShortcut_option(context);
        }
    }
}
