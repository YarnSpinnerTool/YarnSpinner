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
    [DebuggerDisplay("{GetDebuggerDisplay()}")]
    public class SyntaxNode
    {
        public SyntaxNode? Parent { get; }

        public string Type { get; set; } = string.Empty;
        public ParserRuleContext? Context { get; private set; }
        public IToken? Token { get; private set; }

        private readonly List<SyntaxNode> _children = new();

        public IReadOnlyList<SyntaxNode> Children => _children;

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

        public IEnumerable<SyntaxNode> GetChildrenOfType(string type)
        {
            return this._children.Where(c => c.Type == type);
        }

        public IEnumerable<SyntaxNode> GetDescendantsOfType(string type)
        {
            IEnumerable<SyntaxNode> Visit(SyntaxNode node)
            {
                var descendants = node._children.SelectMany(c => Visit(c));
                if (node.Type == type)
                {
                    return descendants.Prepend(node);
                }
                else
                {
                    return descendants;
                }
            }
            return this.Children.SelectMany(c => Visit(c));
        }

        public SyntaxNode? GetAncestorOfType(string type)
        {
            if (this.Type == type) { return this; }
            if (this.Parent == null) { return null; }
            return Parent.GetAncestorOfType(type);
        }

        internal string GetDebuggerDisplay() => $"{Type} ({Children.Count}) " +
            $"'{(Text.Length > 32 ? Text.Substring(0, 32) + "..." : Text)}'";

        public SyntaxNode(SyntaxNode? parent)
        {
            this.Parent = parent;
        }

        public static SyntaxNode VisitTree(CommonTokenStream tokenStream, IParseTree tree)
        {
            Dictionary<int, SyntaxNode> hiddenTokenOwnership = new();

            SyntaxNode Visit(SyntaxNode? parent, IParseTree tree)
            {
                var node = new SyntaxNode(parent);

                IToken startToken, endToken;

                if (tree is ParserRuleContext context)
                {
                    node.Type = YarnSpinnerParser.ruleNames[context.RuleIndex];
                    node.Context = context;
                    startToken = context.Start;
                    endToken = context.Stop;

                }
                else if (tree is TerminalNodeImpl token)
                {
                    node.Type = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Symbol.Type);
                    node.Token = token.Symbol;
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

                return node;
            }

            return Visit(null, tree);

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
}

