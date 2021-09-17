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
        public IEnumerable<Diagnostic> Diagnostics { get; protected set; }
        public ImmutableArray<int> LineStarts { get; protected set; }
        public List<IToken> Commands { get; protected set; }
        public List<YarnFunctionCall> CommandInfos { get; protected set; }
        public List<IToken> Functions { get; protected set; }
        public List<YarnFunctionCall> FunctionInfos { get; protected set; }
        public CodeCompletionCore CodeCompletionCore { get; protected set; }

        public Uri Uri { get; set; }

        public YarnFileData(string text, Uri uri, Workspace workspace)
        {
            this.Uri = uri;
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
            try
            {
                ParseTree = Parser.dialogue(); // Dialogue is the root node of the syntax tree
            }
            catch (ParseException)
            {
                ParseTree = null;
            }

            // should probably just set these directly inside the visit function, or refactor all these into a references object
            (NodeTitles, NodeJumps, Commands, CommandInfos, Functions, FunctionInfos,
                Variables, DeclaredVariables) = ReferencesVisitor.Visit(this, tokenStream);
            DocumentSymbols = DocumentSymbolsVisitor.Visit(this);

            CodeCompletionCore = new CodeCompletionCore(Parser, Handlers.CompletionHandler.PreferedRules, Handlers.CompletionHandler.IgnoredTokens);

            // Should probably get lexer errors too, but not sure how to handle duplicates
            Diagnostics = parserDiagnosticErrorListener.Errors.Concat(lexerDiagnosticErrorListener.Errors);
            Diagnostics = Diagnostics.Concat(Warnings.GetWarnings(this, workspace));
            Diagnostics = Diagnostics.Concat(SemanticErrors.GetErrors(this, workspace));
            PublishDiagnostics(Uri, null, Diagnostics, workspace);
        }

        public int? GetRawToken(Position position)
        {
            return TokenPositionVisitor.Visit(this, position);
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

        private void PublishDiagnostics(DocumentUri uri, int? version, IEnumerable<Diagnostic> diagnostics, Workspace workspace)
        {
            workspace.LanguageServer.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = uri,
                    Version = version,
                    Diagnostics = new Container<Diagnostic>(diagnostics),
                });
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