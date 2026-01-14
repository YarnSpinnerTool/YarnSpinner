// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime.Misc;

    /// <summary>
    /// extracts node metadata during compilation for language server features
    /// this visitor walks the parse tree and gathers information about nodes including
    /// jumps, function calls, commands, variables, character names, and structural information
    /// </summary>
    internal class NodeMetadataVisitor : YarnSpinnerParserBaseVisitor<object>
    {
        private readonly List<NodeMetadata> nodes = new List<NodeMetadata>();
        private NodeMetadata? currentNode = null;
        private readonly string fileUri;
        private int previewLineCount = 0;
        private const int MaxPreviewLines = 3;

        public NodeMetadataVisitor(string fileUri)
        {
            this.fileUri = fileUri;
        }

        /// <summary>
        /// extracts metadata from a parse tree
        /// </summary>
        public static List<NodeMetadata> Extract(string fileUri, YarnSpinnerParser.DialogueContext dialogueContext)
        {
            var visitor = new NodeMetadataVisitor(fileUri);
            visitor.Visit(dialogueContext);
            return visitor.nodes;
        }

        public override object VisitNode([NotNull] YarnSpinnerParser.NodeContext context)
        {
            // start a new node metadata object
            currentNode = new NodeMetadata
            {
                Uri = fileUri,
                // get complexity score from the compiler (set by nodetrackingvisitor or nodegroupvisitor)
                NodeGroupComplexity = context.ComplexityScore,
                // header starts at the first delimiter, convert from 1 based to 0 based
                HeaderStartLine = context.Start.Line - 1
            };

            previewLineCount = 0;

            // visit children to extract all the metadata
            base.VisitNode(context);

            // only add nodes that have a title
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
                // capture the line where title is declared, convert from 1 based to 0 based
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
                // Get the range of the entire jump command (from << to >>)
                // Use COMMAND_START as the start to ensure we get the actual line of the statement
                var commandStart = context.COMMAND_START()?.Symbol;
                var commandEnd = context.COMMAND_END()?.Symbol;

                currentNode.Jumps.Add(new JumpInfo
                {
                    DestinationTitle = destinationName,
                    Type = JumpType.Jump,
                    Range = commandStart != null && commandEnd != null
                        ? Utility.GetRange(commandStart, commandEnd)
                        : Utility.GetRange(context)
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

            // get command text from the formatted text
            var commandFormattedText = context.command_formatted_text();
            if (commandFormattedText != null)
            {
                var commandText = commandFormattedText.GetText();
                if (!string.IsNullOrWhiteSpace(commandText))
                {
                    // extract command name which is the first word
                    var parts = commandText.Split(new[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var commandName = parts[0];

                        // skip flow control keywords (these are not commands from the game perspective)
                        if (commandName != "if" && commandName != "elseif" && commandName != "else" && commandName != "endif")
                        {
                            if (!currentNode.CommandCalls.Contains(commandName))
                            {
                                currentNode.CommandCalls.Add(commandName);
                            }
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

                // extract tags from tags header
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
                // body starts after the second delimiter, convert from 1 based to 0 based
                currentNode.BodyStartLine = context.Start.Line - 1;
                // body ends at the stop token line, convert from 1 based to 0 based
                // note: the stop token might be null if the node is unclosed but we handle that
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

            // extract character name from lines that match the pattern charactername: dialogue
            var lineText = context.line_formatted_text()?.GetText();
            if (!string.IsNullOrWhiteSpace(lineText))
            {
                // look for character name before colon
                var colonIndex = lineText.IndexOf(':');
                if (colonIndex > 0 && colonIndex < lineText.Length - 1)
                {
                    var potentialCharacter = lineText.Substring(0, colonIndex).Trim();
                    // basic heuristic: character names are usually short and dont contain newlines
                    if (potentialCharacter.Length < 30 && !potentialCharacter.Contains('\n'))
                    {
                        if (!currentNode.CharacterNames.Contains(potentialCharacter))
                        {
                            currentNode.CharacterNames.Add(potentialCharacter);
                        }
                    }
                }

                // build preview text from first few lines
                if (previewLineCount < MaxPreviewLines)
                {
                    if (previewLineCount > 0)
                    {
                        currentNode.PreviewText += "\n";
                    }
                    // limit preview line length to avoid huge strings
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
            }

            return base.VisitShortcut_option(context);
        }
    }
}
