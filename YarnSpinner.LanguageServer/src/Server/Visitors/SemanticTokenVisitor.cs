using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Yarn.Compiler;
using Position = Yarn.Compiler.Position;

namespace YarnLanguageServer
{
    internal class SemanticTokenVisitor : YarnSpinnerParserBaseVisitor<bool>
    {
        /// <summary>
        /// The regular expression that matches a character name at the start of
        /// a line.
        /// </summary>
        internal static readonly System.Text.RegularExpressions.Regex NameRegex = new (@"^\s*([^\/#]*?):");

        public static void BuildSemanticTokens(SemanticTokensBuilder builder, YarnFileData yarnFile)
        {
            var visitor = new SemanticTokenVisitor();
            foreach (var commenttoken in yarnFile.CommentTokens)
            {
                visitor.AddTokenType(commenttoken, commenttoken, SemanticTokenType.Comment);
            }

            visitor.Visit(yarnFile.ParseTree);

            foreach (var (start, length, tokenType, tokenModifiers) in visitor.positions.OrderBy(t => t.start.Line).ThenBy(t => t.start.Character))
            {
                builder.Push(start.Line, start.Character, length, tokenType as SemanticTokenType?, tokenModifiers);
            }
        }

        private List<(Position start, int length, SemanticTokenType tokenType, SemanticTokenModifier[] tokenModifiers)> positions;

        private SemanticTokenVisitor()
        {
            this.positions = new List<(Position start, int length, SemanticTokenType tokenType, SemanticTokenModifier[] tokenModifiers)>();
        }

        #region Utility Functions
        private void AddTokenType(IParseTree start, SemanticTokenType tokenType, params SemanticTokenModifier[] tokenModifier) {
            AddTokenType(start, start, tokenType, tokenModifier);
        }

        private void AddTokenType(IParseTree start, IParseTree stop, SemanticTokenType tokenType, params SemanticTokenModifier[] tokenModifier)
        {
            // Note only works for terminal nodes
            AddTokenType(start?.Payload as IToken, stop?.Payload as IToken, tokenType, tokenModifier);
        }

        private void AddTokenType(IToken start, SemanticTokenType tokenType, params SemanticTokenModifier[] tokenModifier)
        {
            AddTokenType(start, start, tokenType, tokenModifier);
        }

        private void AddTokenType(IToken start, IToken stop, SemanticTokenType tokenType, params SemanticTokenModifier[] tokenModifier)
        {
            if (start is not null && stop is not null)
            {
                int length = stop.StopIndex - start.StartIndex + 1;

                positions.Add(
                    (start.ToPosition(), length, tokenType, tokenModifier)
                );
            }
        }

        private void AddTokenType(Position start, int length, SemanticTokenType tokenType, params SemanticTokenModifier[] tokenModifier) {
            positions.Add(
                (start, length, tokenType, tokenModifier)
            );
        }

        #endregion Utility Functions

        #region Visitor Method Overrides

        public override bool VisitHeader([NotNull] YarnSpinnerParser.HeaderContext context)
        {
            AddTokenType(context.header_key, context.header_key, SemanticTokenType.Property);

            if (context.header_value?.Text != null)
            {
                if (context.header_key?.Text == "title")
                {
                    AddTokenType(context.header_value, context.header_value, SemanticTokenType.Class);
                }
                else
                {
                    AddTokenType(context.header_value, context.header_value, SemanticTokenType.String);
                }
            }

            return base.VisitHeader(context);
        }

        public override bool VisitShortcut_option([NotNull] YarnSpinnerParser.Shortcut_optionContext context)
        {
            AddTokenType(context.Start, SemanticTokenType.Keyword);

            return base.VisitShortcut_option(context);
        }

        public override bool VisitFunction_call([Antlr4.Runtime.Misc.NotNull] YarnSpinnerParser.Function_callContext context)
        {
            AddTokenType(context.FUNC_ID(), SemanticTokenType.Function); // function name
            return base.VisitFunction_call(context);
        }

        public override bool VisitLine_condition([NotNull] YarnSpinnerParser.Line_conditionContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.COMMAND_IF(), SemanticTokenType.Keyword); // if
            AddTokenType(context.COMMAND_END(), SemanticTokenType.Keyword); // >>
            return base.VisitLine_condition(context);
        }

        public override bool VisitDeclare_statement([NotNull] YarnSpinnerParser.Declare_statementContext context)
        {
            AddTokenType(context.Start, SemanticTokenType.Keyword);
            AddTokenType(context.Stop, SemanticTokenType.Keyword);
            AddTokenType(context.COMMAND_DECLARE(), SemanticTokenType.Keyword); // declare
            AddTokenType(context.OPERATOR_ASSIGNMENT(), SemanticTokenType.Operator); // =

            return base.VisitDeclare_statement(context);
        }

        public override bool VisitValueFalse([NotNull] YarnSpinnerParser.ValueFalseContext context)
        {
            AddTokenType(context.Stop, SemanticTokenType.Keyword);
            return base.VisitValueFalse(context);
        }

        public override bool VisitValueTrue([NotNull] YarnSpinnerParser.ValueTrueContext context)
        {
            AddTokenType(context.Stop, SemanticTokenType.Keyword);
            return base.VisitValueTrue(context);
        }

        public override bool VisitValueNumber([NotNull] YarnSpinnerParser.ValueNumberContext context)
        {
            AddTokenType(context.Stop, SemanticTokenType.Number);
            return base.VisitValueNumber(context);
        }

        public override bool VisitValueVar([NotNull] YarnSpinnerParser.ValueVarContext context)
        {
            AddTokenType(context.Stop, SemanticTokenType.Variable);
            return base.VisitValueVar(context);
        }

        public override bool VisitIf_statement([NotNull] YarnSpinnerParser.If_statementContext context)
        {            
            AddTokenType(context.COMMAND_START(), SemanticTokenType.Keyword);
            AddTokenType(context.COMMAND_ENDIF(), SemanticTokenType.Keyword);
            AddTokenType(context.COMMAND_END(), SemanticTokenType.Keyword);

            return base.VisitIf_statement(context);
        }

        public override bool VisitIf_clause([NotNull] YarnSpinnerParser.If_clauseContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.COMMAND_IF(), SemanticTokenType.Keyword); // if
            AddTokenType(context.COMMAND_END(), SemanticTokenType.Keyword); // >>
            return base.VisitIf_clause(context);
        }

        public override bool VisitElse_clause([NotNull] YarnSpinnerParser.Else_clauseContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.COMMAND_ELSE(), SemanticTokenType.Keyword); // else
            AddTokenType(context.COMMAND_END(), SemanticTokenType.Keyword); // >>
            return base.VisitElse_clause(context);
        }

        public override bool VisitElse_if_clause([NotNull] YarnSpinnerParser.Else_if_clauseContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.COMMAND_ELSEIF(), SemanticTokenType.Keyword); // elseif
            AddTokenType(context.COMMAND_END(), SemanticTokenType.Keyword); // >>
            return base.VisitElse_if_clause(context);
        }
        
        public override bool VisitValueString([NotNull] YarnSpinnerParser.ValueStringContext context)
        {
            AddTokenType(context.Start, context.Stop, SemanticTokenType.String);
            return base.VisitValueString(context);
        }

        public override bool VisitSet_statement([NotNull] YarnSpinnerParser.Set_statementContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword);
            AddTokenType(context.Stop, context.Stop, SemanticTokenType.Keyword);
            AddTokenType(context.op, context.op, SemanticTokenType.Operator); // =
            AddTokenType(context.COMMAND_SET(), context.COMMAND_SET(),SemanticTokenType.Keyword);

            // AddTokenType(context.expression(), context.expression(), SemanticTokenType.Variable); // $variablename

            return base.VisitSet_statement(context);
        }

        public override bool VisitCall_statement([NotNull] YarnSpinnerParser.Call_statementContext context)
        {
            AddTokenType(context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.COMMAND_CALL(), SemanticTokenType.Keyword);

            AddTokenType(context.Stop, SemanticTokenType.Keyword); // >>
            return base.VisitCall_statement(context);
        }

        public override bool VisitCommand_statement([NotNull] YarnSpinnerParser.Command_statementContext context)
        {
            string commandText = context.command_formatted_text().GetText();

            var commandItems = CommandTextSplitter.SplitCommandText(commandText, true);

            var firstToken = context.command_formatted_text().Start;
            var firstTextToken = context.command_formatted_text().Start;

            var tokens = commandItems.Select(c =>
            {
                var token = new CommonToken(YarnSpinnerLexer.COMMAND_TEXT, c.Text)
                {
                    Line = firstTextToken.Line,
                    Column = firstTextToken.Column + c.Offset,
                    StartIndex = firstTextToken.StartIndex + c.Offset,
                    StopIndex = firstTextToken.StartIndex + c.Offset + c.Text.Length - 1,
                };
                return token;
            });

            AddTokenType(tokens.First(), SemanticTokenType.Function);
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword);
            AddTokenType(context.Stop, context.Stop, SemanticTokenType.Keyword);

            foreach (var token in tokens.Skip(1)) {
                AddTokenType(token, SemanticTokenType.Parameter);
            }

            return base.VisitCommand_statement(context);
        }

        public override bool VisitJumpToNodeName([NotNull] YarnSpinnerParser.JumpToNodeNameContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.Stop, context.Stop, SemanticTokenType.Keyword); // >>

            AddTokenType(context.COMMAND_JUMP(), SemanticTokenType.Function); // jump
            AddTokenType(context.destination, SemanticTokenType.Class); // node_name

            return base.VisitJumpToNodeName(context);
        }

        public override bool VisitJumpToExpression ([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            AddTokenType(context.Start, context.Start, SemanticTokenType.Keyword); // <<
            AddTokenType(context.Stop, context.Stop, SemanticTokenType.Keyword); // >>

            AddTokenType(context.COMMAND_JUMP(), SemanticTokenType.Function); // jump

            return base.VisitJumpToExpression(context);
        }

        public override bool VisitHashtag([NotNull] YarnSpinnerParser.HashtagContext context)
        {
            AddTokenType(context.Start, context.Stop, SemanticTokenType.Comment, SemanticTokenModifier.Declaration);
            return base.VisitHashtag(context);
        }

        public override bool VisitFile_hashtag([NotNull] YarnSpinnerParser.File_hashtagContext context)
        {
            AddTokenType(context.Start, context.Stop, SemanticTokenType.Label);
            return base.VisitFile_hashtag(context);
        }

        public override bool VisitVariable([NotNull] YarnSpinnerParser.VariableContext context)
        {
            AddTokenType(context.Start, context.Stop, SemanticTokenType.Variable); // $variablename
            return base.VisitVariable(context);
        }

        public override bool VisitLine_statement([NotNull] YarnSpinnerParser.Line_statementContext context)
        {
            // The text from the start of the line up to its first colon is considered the character's name.
            var text = context.GetTextWithWhitespace();
            var nameMatch = NameRegex.Match(text);
            if (nameMatch.Success) {
                var nameGroup = nameMatch.Groups[0];

                var startPosition = context.Start.ToPosition();
                startPosition.Character += nameGroup.Index;

                AddTokenType(startPosition, nameGroup.Length, SemanticTokenType.Label);
            }

            return base.VisitLine_statement(context);
        }

        
        #endregion Visitor Method Overrides
    }
}
