using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Antlr4.Runtime;
using Antlr4CodeCompletion.Core.CodeCompletion;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn.Compiler;
using YarnLanguageServer.Diagnostics;

// Disambiguate between
// OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic and
// Yarn.Compiler.Diagnostic
using Diagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace YarnLanguageServer
{
    internal class YarnFileData
    {
        public YarnSpinnerLexer Lexer { get; protected set; }
        public YarnSpinnerParser Parser { get; protected set; }
        public YarnSpinnerParser.DialogueContext ParseTree { get; protected set; }
        public IList<IToken> Tokens { get; protected set; }
        public IList<IToken> CommentTokens { get; protected set; }
        public IList<YarnVariableDeclaration> DeclaredVariables { get; protected set; }
        public IEnumerable<DocumentSymbol> DocumentSymbols { get; protected set; }

        public IList<IToken> NodeTitles { get; protected set; }
        public IList<IToken> NodeJumps { get; protected set; }
        public IList<IToken> Variables { get; protected set; }
        public IEnumerable<Diagnostic> CompilerDiagnostics { get; protected set; }
        public bool HasSemanticDiagnostics { get; protected set;  }
        public ImmutableArray<int> LineStarts { get; protected set; }
        public List<IToken> Commands { get; protected set; }
        public List<YarnFunctionCall> CommandInfos { get; protected set; }
        public List<IToken> Functions { get; protected set; }
        public List<YarnFunctionCall> FunctionInfos { get; protected set; }
        public CodeCompletionCore CodeCompletionCore { get; protected set; }

        public Uri Uri { get; set; }
        public Workspace Workspace { get; protected set; }

        public string Text { get; set; }

        public YarnFileData(string text, Uri uri, Workspace workspace)
        {
            Uri = uri;
            Workspace = workspace;
            Text = text;

            Update(text, workspace);

            // maybe we do the initial parsing, but don't do diagnostics / symbolic tokens until it's actually opened?
            // for now maybe let's just do it all in one go and get lazy if things are slow
        }

        public void Open(string text, Workspace workspace)
        {
            Update(text, workspace);
        }

        public void Update(string text, Workspace workspace)
        {
            LineStarts = TextCoordinateConverter.GetLineStarts(text);

            // Lex tokens and comments
            var commentLexer = new YarnSpinnerLexer(CharStreams.fromstring(text));
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
            Lexer = new YarnSpinnerLexer(CharStreams.fromstring(text));
            var tokenStream = new CommonTokenStream(Lexer);
            Parser = new YarnSpinnerParser(tokenStream);

            // Turn off compiler error listeners, and replace with our friendly / error tolerant ones
            Lexer.RemoveErrorListeners();
            var lexerDiagnosticErrorListener = new LexerDiagnosticErrorListener();
            Lexer.AddErrorListener(lexerDiagnosticErrorListener);
            Parser.RemoveErrorListeners();
            var parserDiagnosticErrorListener = new ParserDiagnosticErrorListener();
            Parser.AddErrorListener(parserDiagnosticErrorListener);

            // Attempt actual parse
            ParseTree = Parser.dialogue(); // Dialogue is the root node of the syntax tree

            // should probably just set these directly inside the visit function, or refactor all these into a references object
            (NodeTitles, NodeJumps, Commands, CommandInfos, Functions, FunctionInfos,
                Variables, DeclaredVariables) = ReferencesVisitor.Visit(this, tokenStream);
            DocumentSymbols = DocumentSymbolsVisitor.Visit(this);

            CodeCompletionCore = new CodeCompletionCore(Parser, Handlers.CompletionHandler.PreferedRules, Handlers.CompletionHandler.IgnoredTokens);

            // Can save parsing/lexing errors here, becuase they should only change when the file needs to be reparsed
            CompilerDiagnostics = parserDiagnosticErrorListener.Errors.Concat(lexerDiagnosticErrorListener.Errors);
            PublishDiagnostics();
        }

        internal void ApplyContentChange(TextDocumentContentChangeEvent contentChange)
        {
            if (contentChange.Range == null) {
                this.Text = contentChange.Text;
                return;
            } else {
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

        public void PublishDiagnostics()
        {
            // Here are the diagnostics that might change depending on other things in the workspace
            var diagnostics = Warnings.GetWarnings(this, Workspace);
            diagnostics = diagnostics.Concat(SemanticErrors.GetErrors(this, Workspace));
            this.HasSemanticDiagnostics = diagnostics.Any();

            diagnostics = diagnostics.Concat(CompilerDiagnostics);

            Workspace.LanguageServer.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = Uri,
                    Version = null,
                    Diagnostics = new Container<Diagnostic>(diagnostics),
                });
        }
        public int? GetRawToken(Position position)
        {
            // TODO: Not sure if it's even worth using a visitor vs just iterating through the token list.
            var result = TokenPositionVisitor.Visit(this, position);
            if (result != null) { return result; }

            // The parse tree doesn't have whitespace tokens so need to manually search sometimes
            var match = this.Tokens.FirstOrDefault(t => PositionHelper.DoesPositionContainToken(position, t));
            result = match?.TokenIndex;
            return result;
        }

        public (YarnSymbolType yarnSymbolType, IToken token) GetTokenAndType(Position position)
        {
            IToken resultToken = null;

            Func<IToken, bool> isTokenMatch = (IToken t) => PositionHelper.DoesPositionContainToken(position, t);

            // TODO Speed these searches up using binary search on the token positions
            // see getTokenFromList() in PositionHelper.cs
            resultToken = Commands.FirstOrDefault(isTokenMatch);
            if (resultToken != null)
            {
                return (YarnSymbolType.Command, resultToken);
            }

            resultToken = NodeTitles.Concat(NodeJumps).FirstOrDefault(isTokenMatch);
            if (resultToken != null)
            {
                return (YarnSymbolType.Node, resultToken);
            }

            resultToken = Variables.FirstOrDefault(isTokenMatch);
            if (resultToken != null)
            {
                return (YarnSymbolType.Variable, resultToken);
            }

            resultToken = Functions.FirstOrDefault(isTokenMatch);
            if (resultToken != null)
            {
                return (YarnSymbolType.Function, resultToken);
            }

            return (YarnSymbolType.Unknown, null);
        }

        public (YarnFunctionCall?, int? activeParameterIndex) GetParameterInfo(Position position)
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
                if (parameter.Contains(position)) {
                    return (info, parameterIndex);
                }

                parameterIndex++;
            }

            return (info, null);
        }

        public void ClearDiagnostics(Workspace workspace)
        {
            workspace.LanguageServer.TextDocument.PublishDiagnostics(new PublishDiagnosticsParams { Uri = this.Uri });
        }

        private YarnFunctionCall? GetFunctionInfo(Position position)
        {
            // Strategy is to look for rightmost start function parameter, and if there are none, check command parameters
            var functionMatches = FunctionInfos.Where(fi => fi.ExpressionRange.Contains(position)).OrderByDescending(fi => fi.ExpressionRange.Start);
            if (functionMatches.Any())
            {
                return functionMatches.FirstOrDefault();
            }

            var commandMatches = CommandInfos.Where(fi => fi.ExpressionRange.Contains(position));
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