using Antlr4.Runtime;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Yarn.Compiler;
// Disambiguate between
// OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic and
// Yarn.Compiler.Diagnostic
using Position = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    internal interface INotificationSender
    {
        void SendNotification<T>(string method, T @params);
    }

    internal class YarnFileData
    {
        public YarnSpinnerLexer Lexer { get; protected set; }
        public YarnSpinnerParser Parser { get; protected set; }
        public YarnSpinnerParser.DialogueContext ParseTree { get; protected set; }
        public IList<IToken> Tokens { get; protected set; }
        public IList<IToken> CommentTokens { get; protected set; }
        public IEnumerable<DocumentSymbol> DocumentSymbols { get; protected set; }

        public ImmutableArray<int> LineStarts { get; protected set; }

        public List<NodeInfo> NodeInfos { get; protected set; }

        public List<string> NodeGroupNames { get; protected set; }

        public Uri Uri { get; set; }
        public INotificationSender? NotificationSender { get; protected set; }

        public string Text { get; set; }

        public YarnFileData(string text, Uri uri, INotificationSender? notificationSender)
        {
            Uri = uri;
            NotificationSender = notificationSender;
            Text = text;

            Update(text);
        }

        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Lexer))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Parser))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(LineStarts))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(ParseTree))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(Tokens))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(CommentTokens))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(DocumentSymbols))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(NodeInfos))]
        [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(NodeGroupNames))]
        public void Update(string text)
        {
            LineStarts = TextCoordinateConverter.GetLineStarts(text);

            // Lex tokens and comments
            var commentLexer = new YarnSpinnerLexer(CharStreams.fromString(text));
            var commentTokenStream = new CommonTokenStream(commentLexer);
            CommentTokens = new List<IToken>();
            commentTokenStream.Fill();
            CommentTokens = commentTokenStream.GetTokens()
                .Where(token =>
                    token.Channel == 2 &&
                    token.Type != YarnSpinnerLexer.Eof)
                .ToList();
            Tokens = commentTokenStream.GetTokens()
                .Where(token =>
                    token.Type != YarnSpinnerLexer.Eof)
                .ToList();

            // Now onto the real parsing
            Lexer = new YarnSpinnerLexer(CharStreams.fromString(text));
            var tokenStream = new CommonTokenStream(Lexer);
            Parser = new YarnSpinnerParser(tokenStream);

            // Turn off compiler error listeners
            Parser.RemoveErrorListeners();
            Lexer.RemoveErrorListeners();

            // Attempt actual parse
            ParseTree = Parser.dialogue(); // Dialogue is the root node of the syntax tree

            // should probably just set these directly inside the visit
            // function, or refactor all these into a references object

            ReferencesVisitor.Visit(this, tokenStream, out var nodeInfos, out var nodeGroupNames);
            this.NodeInfos = nodeInfos.ToList();
            this.NodeGroupNames = nodeGroupNames.ToList();

            DocumentSymbols = DocumentSymbolsVisitor.Visit(this);
        }

        internal void ApplyContentChange(TextDocumentContentChangeEvent contentChange)
        {
            if (contentChange.Range == null)
            {
                this.Text = contentChange.Text;
                return;
            }
            else
            {
                var range = contentChange.Range;

                var startIndex = LineStarts[range.Start.Line] + range.Start.Character;
                var endIndex = LineStarts[range.End.Line] + range.End.Character;

                var stringBuilder = new System.Text.StringBuilder();

                stringBuilder.Append(this.Text, 0, startIndex)
                    .Append(contentChange.Text)
                    .Append(this.Text, endIndex, this.Text.Length - endIndex);

                this.Text = stringBuilder.ToString();
            }
        }

        /// <summary>
        /// Gets the collection of all references to commands in this file.
        /// </summary>
        public IEnumerable<YarnActionReference> CommandReferences => NodeInfos
            .SelectMany(n => n.CommandCalls);

        /// <summary>
        /// Gets the collection of all jumps to nodes in this file.
        /// </summary>
        public IEnumerable<NodeJump> NodeJumps => NodeInfos
            .SelectMany(n => n.Jumps);

        /// <summary>
        /// Gets the collection of all tokens in this file that represent the
        /// title in a node definition.
        /// </summary>
        public IEnumerable<IToken> NodeDefinitions => NodeInfos.Where(n => n.HasTitle).Select(n => n.TitleToken).NonNull();

        /// <summary>
        /// Gets the collection of all function references in this file.
        /// </summary>
        public IEnumerable<YarnActionReference> FunctionReferences => NodeInfos
            .SelectMany(n => n.FunctionCalls);

        /// <summary>
        /// Gets the collection of all tokens in this file that represent
        /// variables.
        /// </summary>
        public IEnumerable<IToken> VariableReferences => NodeInfos
            .SelectMany(n => n.VariableReferences)
            .Select(variableToken => variableToken);

        /// <summary>
        /// Gets the number of lines in this file.
        /// </summary>
        public int LineCount => LineStarts.Length;

        /// <summary>
        /// Gets or sets the <see cref="Project"/> objects that owns this file.
        /// </summary>
        /// <remarks>
        /// A .yarn file may be a part of multiple Yarn projects. However, a
        /// <see cref="YarnFileData"/> represents a Yarn file in the context of
        /// a <i>single</i> Yarn project. Multiple <see cref="YarnFileData"/>
        /// objects may exist for one file on disk.
        /// </remarks>
        public Project? Project { get; internal set; }

        /// <summary>
        /// Gets the length of the line at the specified index, optionally
        /// including the line terminator.
        /// </summary>
        /// <param name="lineIndex">The zero-based index of the line to get the
        /// length of.</param>
        /// <param name="includeLineTerminator">If <see langword="true"/>, the
        /// resulting value will include the line terminator.</param>
        /// <returns>The length of the line.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref
        /// name="lineIndex"/> is less than zero, or equal to or greater than
        /// <see cref="LineCount"/>.</exception>
        public int GetLineLength(int lineIndex, bool includeLineTerminator = false)
        {
            if (lineIndex < 0 || lineIndex >= LineCount)
            {
                throw new ArgumentOutOfRangeException(nameof(lineIndex), $"Must be between zero and {nameof(LineCount)}");
            }


            if (Text.Length == 0)
            {
                return 0;
            }

            var chars = Text.ToCharArray();

            var start = LineStarts[lineIndex];
            int end;
            if ((lineIndex + 1) < LineStarts.Length)
            {
                end = LineStarts[lineIndex + 1];
            }
            else
            {
                end = chars.Length;
            }

            var offset = 0;


            while ((start + offset) < end)
            {
                if (!includeLineTerminator && (chars[start + offset] == '\r' || chars[start + offset] == '\n'))
                {
                    break;
                }

                offset += 1;
            }

            return offset;
        }

        /// <summary>
        /// Given a position in the file, returns the type of symbol it
        /// represents (if any), and the token at that position (if any).
        /// </summary>
        /// <param name="position">The position to query for.</param>
        /// <returns>A tuple containing the type and token of the symbol at
        /// <paramref name="position"/>.</returns>
        public (YarnSymbolType yarnSymbolType, IToken? token) GetTokenAndType(Position position)
        {
            Func<IToken, bool> isTokenMatch = (IToken t) => PositionHelper.DoesPositionContainToken(position, t);

            var allSymbolTokens = new IEnumerable<(IToken token, YarnSymbolType type)>[] {
                // Jumps and Detours
                NodeInfos.SelectMany(n => n.Jumps).Select(j => (j.DestinationToken, YarnSymbolType.Node)),

                // Commands
                NodeInfos.SelectMany(n => n.CommandCalls).Select(c => (c.NameToken, YarnSymbolType.Command)),

                // Variables
                NodeInfos.SelectMany(n => n.VariableReferences).Select(v => (v, YarnSymbolType.Variable)),

                // Functions
                NodeInfos.SelectMany(n => n.FunctionCalls).Select(f => (f.NameToken, YarnSymbolType.Function)),
            };

            foreach (var tokenInfo in allSymbolTokens.SelectMany(g => g))
            {
                if (isTokenMatch(tokenInfo.token))
                {
                    return (tokenInfo.type, tokenInfo.token);
                }
            }

            // TODO Speed these searches up using binary search on the token positions
            // see getTokenFromList() in PositionHelper.cs
            return (YarnSymbolType.Unknown, null);
        }

        public (YarnActionReference? actionReference, int? activeParameterIndex) GetParameterInfo(Position position)
        {
            var info = GetFunctionInfo(position);
            if (info == null)
            {
                return (null, null);
            }

            if (!info.Value.ParameterRanges.Any()
                || position < info.Value.ParameterRanges.First().Start
                || position > info.Value.ParametersRange.End)
            {
                return (info, info.Value.ParametersRange.Contains(position) ? 0 : null);
            }

            int parameterIndex = 0;
            foreach (var parameter in info.Value.ParameterRanges)
            {
                if (parameter.Contains(position))
                {
                    return (info, parameterIndex);
                }

                parameterIndex++;
            }

            return (info, null);
        }

        /// <summary>
        /// Indicates whether the text specified by the given range is null,
        /// empty, or consists only of white-space characters.
        /// </summary>
        /// <param name="range">The range to check.</param>
        /// <returns><see langword="true"/> if the specified range is null,
        /// empty, or consists only of white-space characters.</returns>
        public bool IsNullOrWhitespace(Range range)
        {
            if (range.IsEmpty())
            {
                return true;
            }

            var rangeStartIndex = LineStarts[range.Start.Line] + range.Start.Character;
            var rangeEndIndex = LineStarts[range.End.Line] + range.End.Character;

            var slice = this.Text.Substring(rangeStartIndex, rangeEndIndex - rangeStartIndex);

            return string.IsNullOrWhiteSpace(slice);
        }

        /// <inheritdoc cref="IsNullOrWhitespace(Range)"/>
        /// <param name="start">The start of the range to check.</param>
        /// <param name="end">The end of the range to check.</param>
        public bool IsNullOrWhitespace(Position start, Position end)
        {
            if (start > end)
            {
                // Invalid range.
                return false;
            }

            return IsNullOrWhitespace(new Range(start, end));
        }

        /// <summary>
        /// Gets a substring of this file's text, indicated by the given range.
        /// </summary>
        /// <param name="range">The range of this file to get.</param>
        /// <returns>A substring of this file's text.</returns>
        public string GetRange(Range range)
        {
            var startOffset = PositionHelper.GetOffset(this.LineStarts, range.Start);
            var endOffset = PositionHelper.GetOffset(this.LineStarts, range.End);

            return this.Text.Substring(startOffset, endOffset - startOffset);
        }

        public bool TryGetRawToken(Position position, out int rawToken)
        {
            // TODO: Not sure if it's even worth using a visitor vs just iterating through the token list.
            var result = TokenPositionVisitor.Visit(this, position);
            if (result.HasValue)
            {
                rawToken = result.Value;
                return true;
            }

            // The parse tree doesn't have whitespace tokens so need to manually search sometimes
            var match = this.Tokens.FirstOrDefault(t => PositionHelper.DoesPositionContainToken(position, t));
            result = match?.TokenIndex;
            if (result.HasValue)
            {
                rawToken = result.Value;
                return true;
            }
            rawToken = default;
            return false;
        }

        [Obsolete("Use " + nameof(TryGetRawToken))]
        public int? GetRawToken(Position position)
        {
            if (TryGetRawToken(position, out var token))
            {
                return token;
            }
            else
            {
                return null;
            }
        }

        private YarnActionReference? GetFunctionInfo(Position position)
        {
            // Strategy is to look for rightmost start function parameter, and if there are none, check command parameters
            var functionMatches = NodeInfos.SelectMany(n => n.FunctionCalls).Where(fi => fi.ExpressionRange.Contains(position)).OrderByDescending(fi => fi.ExpressionRange.Start);
            if (functionMatches.Any())
            {
                return functionMatches.FirstOrDefault();
            }

            var commandMatches = NodeInfos.SelectMany(n => n.CommandCalls).Where(fi => fi.ExpressionRange.Contains(position));
            if (commandMatches.Any())
            {
                return commandMatches.FirstOrDefault();
            }

            return null;
        }
    }

    public enum YarnSymbolType
    {
        Node,
        Command,
        Variable,
        Function,
        Unknown,
    }
}
