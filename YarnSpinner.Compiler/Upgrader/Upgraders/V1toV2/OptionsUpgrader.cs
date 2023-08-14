// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler.Upgrader
{
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;

    internal class OptionsUpgrader : ILanguageUpgrader
    {
        public UpgradeResult Upgrade(UpgradeJob upgradeJob)
        {
            var outputFiles = new List<UpgradeResult.OutputFile>();

            foreach (var file in upgradeJob.Files)
            {
                var replacements = new List<TextReplacement>();

                ICharStream input = CharStreams.fromstring(file.Source);
                YarnSpinnerV1Lexer lexer = new YarnSpinnerV1Lexer(input);
                CommonTokenStream tokens = new CommonTokenStream(lexer);
                YarnSpinnerV1Parser parser = new YarnSpinnerV1Parser(tokens);

                var tree = parser.dialogue();

                var formatFunctionVisitor = new OptionsVisitor(replacements);

                formatFunctionVisitor.Visit(tree);

                outputFiles.Add(new UpgradeResult.OutputFile(file.FileName, replacements, file.Source));
            }

            return new UpgradeResult
            {
                Files = outputFiles,
            };
        }

        private class OptionsVisitor : YarnSpinnerV1ParserBaseVisitor<int>
        {
            private const string NodesWereRenamedDescription = "Node names containing a period were renamed to have underscores.";
            private const string OptionSyntaxChangedDescription = "Options using deprecated syntax were moved to the end of the node.";
            private const string OptionDestinationsWereRenamedDescription = "Option destinations containing a period were renamed to have underscores.";
            private const string OptionsWereMovedDescription = "An option using deprecated syntax was moved to the end of the node.";
            private const string JumpSyntaxWasUpgradedDescription = "A jump was upgraded to use updated syntax.";
            private readonly ICollection<TextReplacement> replacements;

            private readonly List<OptionLink> currentNodeOptionLinks = new List<OptionLink>();

            public OptionsVisitor(ICollection<TextReplacement> replacements)
            {
                this.replacements = replacements;
            }

            public static string GetContextTextWithWhitespace(ParserRuleContext context)
            {
                // Get the original text of expressionContext. We can't
                // use "expressionContext.GetText()" here, because that
                // just concatenates the text of all captured tokens,
                // and doesn't include text on hidden channels (e.g.
                // whitespace and comments).
                if (context == null)
                {
                    return string.Empty;
                }

                var interval = new Interval(context.Start.StartIndex, context.Stop.StopIndex);
                return context.Start.InputStream.GetText(interval);
            }

            public override int VisitNode([NotNull] YarnSpinnerV1Parser.NodeContext context)
            {
                this.currentNodeOptionLinks.Clear();

                this.VisitChildren(context);

                if (this.currentNodeOptionLinks.Count == 0)
                {
                    // No options in this node. Early out.
                    return 0;
                }

                // CurrentNodeOptionLinks now contains all options
                // that were encountered; create amendments to delete them
                // and add shortcut options
                var newShortcutOptionEntries = new List<string>();

                bool optionsWereRenamed = false;

                foreach (var optionLink in this.currentNodeOptionLinks)
                {
                    // If this option link has any hashtags, the newline at
                    // the end of the line is captured, so we'll need to
                    // generate a new one in our replacement text. If not,
                    // then the replacement text is empty.
                    var needsNewline = optionLink.Context.hashtag().Length > 0;

                    string replacementText = $@"// Option ""{GetContextTextWithWhitespace(optionLink.Context.option_formatted_text())}"" moved to the end of this node";
                    replacementText += needsNewline ? "\n" : string.Empty;

                    // Create a replacement to remove it
                    var replacement = new TextReplacement
                    {
                        Start = optionLink.Context.Start.StartIndex,
                        StartLine = optionLink.Context.Start.Line,
                        OriginalText = GetContextTextWithWhitespace(optionLink.Context),
                        ReplacementText = replacementText,
                        Comment = OptionsWereMovedDescription,
                    };

                    this.replacements.Add(replacement);

                    // And create a replacement at the end to add the
                    // shortcut replacement
                    var optionLine = GetContextTextWithWhitespace(optionLink.Context.option_formatted_text());
                    var optionDestination = optionLink.Context.NodeName?.Text ?? "<ERROR: invalid destination>";

                    if (optionDestination.Contains("."))
                    {
                        optionDestination = optionDestination.Replace(".", "_");
                        optionsWereRenamed = true;
                    }

                    var hashtags = optionLink.Context.hashtag().Select(hashtag => GetContextTextWithWhitespace(hashtag));

                    var conditions = optionLink.Conditions.Select(c =>
                    {
                        if (c.requiredTruthValue == true)
                        {
                            return $"({GetContextTextWithWhitespace(c.expression)})";
                        }
                        else
                        {
                            return $"!({GetContextTextWithWhitespace(c.expression)})";
                        }
                    }).Reverse();

                    var allConditions = string.Join(" && ", conditions);

                    var sb = new System.Text.StringBuilder();

                    // Create the shortcut option
                    sb.Append("-> ");
                    sb.Append(optionLine);

                    // If this option had any conditions, emit the computed
                    // line condition
                    if (allConditions.Count() > 0)
                    {
                        sb.Append($" <<if {allConditions}>>");
                    }

                    // Emit all hashtags that the option had
                    foreach (var hashtag in hashtags)
                    {
                        sb.Append($" {hashtag}");
                    }

                    // Now start creating the jump instruction
                    sb.AppendLine();

                    // Indent one level; we know we're at the end of a node
                    // so we're at the zero indentation level
                    sb.Append("    ");

                    // Emit the jump instruction itself
                    sb.Append($"<<jump {optionDestination}>>");
                    sb.AppendLine();

                    // We're done!
                    newShortcutOptionEntries.Add(sb.ToString());
                }

                // Finally, create a replacement that injects the newly created shortcut options
                var endOfNode = context.BODY_END().Symbol;

                string replacementDescription = $"{OptionSyntaxChangedDescription}{(optionsWereRenamed ? " " + OptionDestinationsWereRenamedDescription : string.Empty)}";

                var newOptionsReplacement = new TextReplacement
                {
                    Start = endOfNode.StartIndex,
                    OriginalText = string.Empty,
                    ReplacementText = string.Join(string.Empty, newShortcutOptionEntries),
                    StartLine = endOfNode.Line,
                    Comment = replacementDescription,
                };

                this.replacements.Add(newOptionsReplacement);

                return 0;
            }

            public override int VisitOptionJump([NotNull] YarnSpinnerV1Parser.OptionJumpContext context)
            {
                var destination = context.NodeName.Text;

                var nodesWereRenamed = false;
                if (destination.Contains("."))
                {
                    destination = destination.Replace(".", "_");
                    nodesWereRenamed = true;
                }

                var comment = JumpSyntaxWasUpgradedDescription + (nodesWereRenamed ? " " + OptionDestinationsWereRenamedDescription : string.Empty);

                var replacement = new TextReplacement
                {
                    OriginalText = GetContextTextWithWhitespace(context),
                    ReplacementText = $"<<jump {destination}>>",
                    Start = context.Start.StartIndex,
                    StartLine = context.Start.Line,
                    Comment = comment,
                };

                this.replacements.Add(replacement);

                return 0;
            }

            public override int VisitOptionLink([NotNull] YarnSpinnerV1Parser.OptionLinkContext context)
            {
                var link = new OptionLink(context);

                // Walk up the tree until we hit a NodeContext, looking for
                // if-clauses, else-if clauses, and end-if clauses.
                var parent = context.Parent;

                while (parent != null && parent is YarnSpinnerV1Parser.NodeContext == false)
                {
                    if (parent is YarnSpinnerV1Parser.If_clauseContext ifClause)
                    {
                        // The option is inside an 'if' clause. The
                        // expression must evaluate to true in order for
                        // this option to run.
                        link.Conditions.Add((ifClause.expression(), true));
                    }
                    else if (parent is YarnSpinnerV1Parser.Else_if_clauseContext elseIfContext)
                    {
                        // The option is inside an 'else if' clause. The
                        // expression must evaluate to true, and all of the
                        // preceding if and else-if clauses in this if
                        // statement must evaluate to false, in order for
                        // this option to run.
                        link.Conditions.Add((elseIfContext.expression(), true));

                        var parentIfClause = elseIfContext.Parent as YarnSpinnerV1Parser.If_statementContext;

                        foreach (var siblingClause in parentIfClause.children)
                        {
                            // Stop if we've reached ourself
                            if (siblingClause == elseIfContext)
                            {
                                break;
                            }

                            switch (siblingClause)
                            {
                                case YarnSpinnerV1Parser.If_clauseContext siblingIfClause:
                                    link.Conditions.Add((siblingIfClause.expression(), false));
                                    break;
                                case YarnSpinnerV1Parser.Else_if_clauseContext siblingElseIfClause:
                                    link.Conditions.Add((siblingElseIfClause.expression(), false));
                                    break;
                            }
                        }
                    }
                    else if (parent is YarnSpinnerV1Parser.Else_clauseContext elseContext)
                    {
                        // The option is inside an 'else' clause. All of the
                        // preceding if and else-if clauses in this if
                        // statement must evaluate to false, in order for
                        // this option to run.
                        var parentIfClause = elseContext.Parent as YarnSpinnerV1Parser.If_statementContext;

                        foreach (var siblingClause in parentIfClause.children)
                        {
                            // Stop if we've hit ourself (probably not an
                            // issue since an else statement occurs at the
                            // end anyway, but good to check imo)
                            if (siblingClause == elseContext)
                            {
                                break;
                            }

                            switch (siblingClause)
                            {
                                case YarnSpinnerV1Parser.If_clauseContext siblingIfClause:
                                    link.Conditions.Add((siblingIfClause.expression(), false));
                                    break;
                                case YarnSpinnerV1Parser.Else_if_clauseContext siblingElseIfClause:
                                    link.Conditions.Add((siblingElseIfClause.expression(), false));
                                    break;
                            }
                        }
                    }

                    // Step up the tree
                    parent = parent.Parent;
                }

                this.currentNodeOptionLinks.Add(link);

                return base.VisitOptionLink(context);
            }

            public override int VisitHeader([NotNull] YarnSpinnerV1Parser.HeaderContext context)
            {
                // When we encounter a "title:" header, replace any periods in
                // it with underscores.
                if (context.header_key.Text != "title")
                {
                    return base.VisitHeader(context);
                }

                var nodeName = context.header_value.Text;

                if (nodeName.Contains("."))
                {
                    var newNodeName = nodeName.Replace(".", "_");

                    var replacement = new TextReplacement
                    {
                        Start = context.header_value.StartIndex,
                        StartLine = context.header_value.Line,
                        OriginalText = nodeName,
                        ReplacementText = newNodeName,
                        Comment = NodesWereRenamedDescription,
                    };

                    this.replacements.Add(replacement);
                }

                return base.VisitHeader(context);
            }

            private struct OptionLink
            {
                public YarnSpinnerV1Parser.OptionLinkContext Context;

                // The collection of conditions for this option to appear
                public List<(YarnSpinnerV1Parser.ExpressionContext expression, bool requiredTruthValue)> Conditions;

                public OptionLink(YarnSpinnerV1Parser.OptionLinkContext context)
                    : this()
                {
                    this.Context = context;
                    this.Conditions = new List<(YarnSpinnerV1Parser.ExpressionContext expression, bool requiredTruthValue)>();
                }
            }
        }
    }
}
