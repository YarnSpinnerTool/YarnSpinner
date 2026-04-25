using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Yarn.Compiler;

#nullable enable

namespace Yarn.Compiler
{
    public class SyntaxTree
    {
        public string Name { get; internal set; }
        public SyntaxNode RootNode { get; internal set; }
    }

    [DebuggerDisplay("{GetDebuggerDisplay()}")]
    public class SyntaxNode
    {
        public SyntaxNode? Parent { get; }

        public string Type { get; set; } = string.Empty;
        public ParserRuleContext? Context { get; private set; }
        public IToken? Token { get; private set; }

        private readonly List<SyntaxNode> _children = new();

        public IReadOnlyList<SyntaxNode> Children => _children;

        public SyntaxTree SyntaxTree { get; internal set; }

        private int? _cachedLength = null;

        public virtual string Text
        {
            get
            {
                if (this.Token != null)
                {
                    return LeadingTrivia?.Text + this.Token.Text + TrailingTrivia?.Text;
                }
                else
                {
                    return LeadingTrivia?.Text + string.Join("", Children.Select(c => c.Text)) + TrailingTrivia?.Text;
                }
            }
        }

        public virtual int Length
        {
            get
            {
                if (_cachedLength.HasValue)
                {
                    return _cachedLength.Value;
                }

                if (this.Token != null)
                {
                    _cachedLength = (LeadingTrivia?.Length ?? 0)
                        + this.Token.Text.Length
                        + (TrailingTrivia?.Length ?? 0);
                }
                else
                {
                    _cachedLength = (this.LeadingTrivia?.Length ?? 0)
                        + Children.Aggregate(0, (curr, node) => curr + node.Length)
                        + (this.TrailingTrivia?.Length ?? 0);
                }
                return _cachedLength.Value;
            }
        }

        private void InvalidateCachedLength()
        {
            if (_cachedLength.HasValue)
            {
                return;
            }
            _cachedLength = null;
            Parent?.InvalidateCachedLength();
        }

        public string TextWithoutTrivia
        {
            get
            {
                if (this.Token != null)
                {
                    return this.Token.Text;
                }
                else
                {
                    return string.Join("", Children.Select(c => c.TextWithoutTrivia));
                }
            }
        }

        public TriviaSyntaxNode? TrailingTrivia { get; private set; } = null;
        public TriviaSyntaxNode? LeadingTrivia { get; private set; } = null;

        public int StartOffset
        {
            get
            {
                if (Parent == null) { return 0; }
                return Parent.GetOffsetOfChild(this);
            }
        }

        public int EndOffset
        {
            get
            {
                return StartOffset + Length;
            }
        }

        private int GetOffsetOfChild(SyntaxNode syntaxNode)
        {
            var offset = this.StartOffset;

            foreach (var child in this.Children)
            {
                if (child == syntaxNode)
                {
                    return offset;
                }
                offset += child.Length;
            }

            throw new ArgumentException($"Child {syntaxNode} is not a child of {this}");
        }

        internal IEnumerable<SyntaxNode> GetChildren(string? type = null)
        {
            return this._children.Where(c => type == null || c.Type == type);
        }

        public IEnumerable<T> GetChildren<T>() where T : SyntaxNode
        {
            return this._children.OfType<T>();
        }

        public IEnumerable<T> GetDescendants<T>() where T : SyntaxNode
        {
            static IEnumerable<T> Visit(SyntaxNode node)
            {
                var descendants = node._children.SelectMany(c => Visit(c));
                if (node is T item)
                {
                    return descendants.Prepend(item);
                }
                else
                {
                    return descendants;
                }
            }
            return Visit(this);
        }

        internal IEnumerable<SyntaxNode> GetDescendants(string? type = null)
        {
            IEnumerable<SyntaxNode> Visit(SyntaxNode node)
            {
                var descendants = node._children.SelectMany(c => Visit(c));
                if (type == null || node.Type == type)
                {
                    return descendants.Prepend(node);
                }
                else
                {
                    return descendants;
                }
            }
            return Visit(this);
        }

        internal SyntaxNode? GetAncestorOfType(string type)
        {
            if (this.Type == type) { return this; }
            if (this.Parent == null) { return null; }
            return Parent.GetAncestorOfType(type);
        }

        public T? GetAncestor<T>() where T : SyntaxNode
        {
            if (this is T item) { return item; }
            if (this.Parent == null) { return null; }
            return Parent.GetAncestor<T>();
        }

        internal string GetDebuggerDisplay() => $"{Type} ({Children.Count}) " +
            $"'{(Text.Length > 32 ? Text.Substring(0, 32) + "..." : Text)}'";

        public SyntaxNode(SyntaxNode? parent)
        {
            this.Parent = parent;
        }

        public static SyntaxTree VisitTree(FileParseResult fileParseResult)
        {
            var tokenStream = fileParseResult.Tokens;
            var parseTree = fileParseResult.Tree;
            var syntaxTree = new SyntaxTree()
            {
                Name = fileParseResult.Name,
            };

            Dictionary<int, SyntaxNode> hiddenTokenOwnership = new();

            SyntaxNode Visit(SyntaxNode? parent, IParseTree tree)
            {
                SyntaxNode node;

                IToken startToken, endToken;

                if (tree is ParserRuleContext context)
                {
                    switch (context)
                    {
                        case YarnSpinnerParser.NodeContext:
                            node = new NodeSyntaxNode(parent);
                            break;
                        case YarnSpinnerParser.Line_statementContext:
                            node = new LineStatementSyntaxNode(parent);
                            break;
                        case YarnSpinnerParser.Shortcut_option_statementContext:
                            node = new OptionStatementSyntaxNode(parent);
                            break;
                        case YarnSpinnerParser.Line_group_statementContext:
                            node = new LineGroupStatementSyntaxNode(parent);
                            break;
                        case YarnSpinnerParser.Command_statementContext:
                            node = new CommandStatementSyntaxNode(parent);
                            break;
                        default:
                            node = new SyntaxNode(parent);
                            break;
                    }

                    node.Type = YarnSpinnerParser.ruleNames[context.RuleIndex];
                    node.Context = context;
                    startToken = context.Start;
                    endToken = context.Stop;
                }
                else if (tree is TerminalNodeImpl token)
                {
                    node = new SyntaxNode(parent)
                    {
                        Type = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Symbol.Type),
                        Token = token.Symbol
                    };
                    startToken = endToken = token.Symbol;
                }
                else
                {
                    throw new System.InvalidOperationException("Unhandled tree type " + tree.GetType().Name);
                }


                // Trailing trivia are nodes on the same line as the last
                // token of the node that are not already owned by another
                // node. We grab before visiting children because we want
                // trivia to be associated with the highest-level statement
                // on the same line - for example, comments following a line
                // should be trailing trivia on the line statement itself,
                // not the final token that makes up the line

                {
                    var trailingHiddenTokens = (tokenStream.GetHiddenTokensToRight(endToken.TokenIndex) ?? new List<IToken>())
                        .Where(t => t.Line == endToken.Line)
                        .TakeWhile(t => hiddenTokenOwnership.ContainsKey(t.TokenIndex) == false).ToList();
                    if (trailingHiddenTokens.Count != 0)
                    {
                        foreach (var t in trailingHiddenTokens)
                        {
                            hiddenTokenOwnership[t.TokenIndex] = node;
                        }
                        var interval = new Interval(trailingHiddenTokens.First().StartIndex, trailingHiddenTokens.Last().StopIndex);
                        var trailingTrivia = startToken.InputStream.GetText(interval);
                        node.TrailingTrivia = new TriviaSyntaxNode(node, trailingTrivia);
                    }
                }

                // Find leading trivia
                if (ConsumesLeadingTrivia(node.Type))
                {
                    var precedingHiddenTokens = (tokenStream.GetHiddenTokensToLeft(startToken.TokenIndex) ?? new List<IToken>())
                        .Reverse()
                        .TakeWhile(t => hiddenTokenOwnership.ContainsKey(t.TokenIndex) == false)
                        .Reverse()
                        .ToList();
                    if (precedingHiddenTokens.Count != 0)
                    {

                        foreach (var t in precedingHiddenTokens)
                        {
                            hiddenTokenOwnership[t.TokenIndex] = node;
                        }
                        var interval = new Interval(precedingHiddenTokens.First().StartIndex, precedingHiddenTokens.Last().StopIndex);
                        var leadingTrivia = endToken.InputStream.GetText(interval);
                        node.LeadingTrivia = new TriviaSyntaxNode(node, leadingTrivia);
                    }
                }

                var children = new List<SyntaxNode>();

                // Descend into children
                for (int i = 0; i < tree.ChildCount; i++)
                {
                    var childTree = tree.GetChild(i);
                    children.Add(Visit(node, childTree));
                }
                node.AddChildren(children);

                node.SyntaxTree = syntaxTree;

                return node;
            }

            var rootNode = Visit(null, parseTree);

            syntaxTree.RootNode = rootNode;
            return syntaxTree;
        }

        public void AddChild(SyntaxNode child)
        {
            _children.Add(child);
            InvalidateCachedLength();
        }
        public void InsertChild(int index, SyntaxNode child)
        {
            _children.Insert(index, child);
            InvalidateCachedLength();
        }
        public void RemoveChild(SyntaxNode child)
        {
            _children.Remove(child);
            InvalidateCachedLength();
        }

        public void AddChildren(List<SyntaxNode> children)
        {
            _children.AddRange(children);
            InvalidateCachedLength();
        }

        private static bool ConsumesLeadingTrivia(string type)
        {
            // Don't allow 'container' nodes to have leading trivia; the
            // actual content inside them should have it
            return type != "body" && type != "statement";
        }
    }

    public abstract class AntlrSyntaxNode<T> : SyntaxNode where T : ParserRuleContext
    {
        public AntlrSyntaxNode(SyntaxNode? parent) : base(parent)
        {
        }

        protected T AntlrContext => this.Context as T
           ?? throw new InvalidOperationException($"{nameof(LineStatementSyntaxNode)} context type is not {nameof(T)}");
    }

    public class CommandStatementSyntaxNode : AntlrSyntaxNode<YarnSpinnerParser.Command_statementContext>
    {
        public CommandStatementSyntaxNode(SyntaxNode? parent) : base(parent)
        {
        }

        public string CommandText
        {
            get
            {
                var expressionCount = 0;
                var sb = new System.Text.StringBuilder();
                foreach (var node in AntlrContext.command_formatted_text()?.children ?? Array.Empty<IParseTree>())
                {
                    if (node is ITerminalNode)
                    {
                        sb.Append(node.GetText());
                    }
                    else if (node is ParserRuleContext)
                    {


                        // Don't include the '{' and '}', because it will have been
                        // added as a terminal node already
                        sb.Append(expressionCount);
                        expressionCount += 1;
                    }
                }
                return sb.ToString();
            }
        }
    }

    public class NodeSyntaxNode : AntlrSyntaxNode<YarnSpinnerParser.NodeContext>
    {
        public NodeSyntaxNode(SyntaxNode? parent) : base(parent)
        {
        }

        public string? Title
        {
            get
            {
                if (AntlrContext.NodeTitle != null)
                {

                    return AntlrContext.NodeTitle;
                }
                else
                {
                    return AntlrContext.title_header().FirstOrDefault()?.title?.Text;
                }
            }
        }

        public IEnumerable<KeyValuePair<string, string>> Headers
        {
            get
            {
                var result = new List<KeyValuePair<string, string>>
                {
                    new("title", AntlrContext.title_header().FirstOrDefault()?.title.Text ?? string.Empty)
                };

                foreach (var whenHeader in AntlrContext.when_header() ?? Enumerable.Empty<YarnSpinnerParser.When_headerContext>())
                {
                    result.Add(new("when", whenHeader.header_when_expression()?.GetTextWithWhitespace() ?? string.Empty));
                }
                foreach (var header in AntlrContext.header() ?? Enumerable.Empty<YarnSpinnerParser.HeaderContext>())
                {
                    if (header.header_key != null)
                    {

                        result.Add(new(header.header_key.Text, header.header_value.Text ?? string.Empty));
                    }
                }
                return result;
            }
        }
    }

    public class LineGroupStatementSyntaxNode : AntlrSyntaxNode<YarnSpinnerParser.Line_group_statementContext>
    {
        public LineGroupStatementSyntaxNode(SyntaxNode? parent) : base(parent)
        {
        }

        public IEnumerable<LineStatementSyntaxNode> Lines
        {
            get
            {
                return this.Children.Where(t => t.Type == "line_group_item").SelectMany(i => i.Children.OfType<LineStatementSyntaxNode>());
            }
        }

    }

    public class OptionStatementSyntaxNode : AntlrSyntaxNode<YarnSpinnerParser.Shortcut_option_statementContext>
    {
        public OptionStatementSyntaxNode(SyntaxNode? parent) : base(parent)
        {
        }

        public IEnumerable<LineStatementSyntaxNode> Options
        {
            get
            {
                return this.Children.Where(t => t.Type == "shortcut_option").SelectMany(i => i.Children.OfType<LineStatementSyntaxNode>());
            }
        }
    }

    public class LineStatementSyntaxNode : AntlrSyntaxNode<YarnSpinnerParser.Line_statementContext>
    {
        public LineStatementSyntaxNode(SyntaxNode? parent) : base(parent)
        {
        }

        public string LineText
        {
            get
            {
                var formattedText = this.AntlrContext.line_formatted_text();
                if (formattedText == null)
                {
                    return string.Empty;
                }
                var nodes = formattedText.children;
                var composedString = new System.Text.StringBuilder();
                int expressionCount = 0;

                foreach (var child in nodes)
                {
                    if (child is ITerminalNode)
                    {
                        composedString.Append(child.GetText());
                    }
                    else if (child is ParserRuleContext)
                    {
                        // Expressions in the final string are denoted as the
                        // index of the expression, surrounded by braces { }.
                        // However, we don't need to write the braces here
                        // ourselves, because the text itself that the parser
                        // captured already has them. So, we just need to write
                        // the expression count.
                        composedString.Append(expressionCount);
                        expressionCount += 1;
                    }
                }
                return composedString.ToString().Trim();
            }
        }

        public string? LineID
        {
            get
            {
                if (AntlrContext.LineID != null)
                {
                    // Get the line ID that's been assigned to us during
                    // compilation, if any
                    return AntlrContext.LineID;
                }
                else
                {
                    // Try and get an explicitly-set line ID from the hashtags
                    foreach (var hashtag in Hashtags)
                    {
                        if (hashtag.StartsWith("line:"))
                        {
                            return hashtag;
                        }
                    }
                    return null;
                }
            }
        }

        public IEnumerable<string> Hashtags
        {
            get
            {
                return AntlrContext.hashtag()?.Select(h => h.HASHTAG_TEXT().GetText()) ?? Enumerable.Empty<string>();
            }
        }
    }
}

