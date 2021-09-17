using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Yarn.Compiler;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    internal class ReferencesVisitor : YarnSpinnerParserBaseVisitor<bool>
    {
        private readonly List<IToken> titles = new List<IToken>();
        private readonly List<IToken> jumps = new List<IToken>();
        private readonly List<IToken> commands = new List<IToken>();
        private readonly List<YarnFunctionCall> commandInfos = new List<YarnFunctionCall>();
        private readonly List<IToken> functions = new List<IToken>();
        private readonly List<YarnFunctionCall> functionInfos = new List<YarnFunctionCall>();
        private readonly List<IToken> variables = new List<IToken>();
        private readonly List<YarnVariableDeclaration> declarations = new List<YarnVariableDeclaration>();
        private YarnFileData yarnFileData;

        /// <summary>
        /// The CommonTokenStream derived from the file we're parsing. This
        /// is used to find documentation comments for declarations.
        /// </summary>
        private CommonTokenStream tokens;

        public static (
            List<IToken> titles, List<IToken> jumps, List<IToken> commands, List<YarnFunctionCall> commandInfos,
            List<IToken> functions, List<YarnFunctionCall> functionInfos, List<IToken> variables, List<YarnVariableDeclaration> declarations)
            Visit(YarnFileData yarnFileData, CommonTokenStream tokens)
        {
            var visitor = new ReferencesVisitor
            {
                yarnFileData = yarnFileData,
                tokens = tokens,
            };
            if (yarnFileData.ParseTree != null)
            {
                visitor.Visit(yarnFileData.ParseTree);
            }

            return (visitor.titles, visitor.jumps, visitor.commands, visitor.commandInfos,
                visitor.functions, visitor.functionInfos, visitor.variables, visitor.declarations);
        }

        public override bool VisitVariable([NotNull] YarnSpinnerParser.VariableContext context)
        {
            variables.Add(context.Stop);
            return base.VisitVariable(context);
        }

        public override bool VisitHeader([NotNull] YarnSpinnerParser.HeaderContext context)
        {
            if (context.header_key != null && context.header_key.Text == "title" && context.header_value != null)
            {
                titles.Add(context.header_value);
            }

            return base.VisitHeader(context);
        }

        public override bool VisitJump_destination([NotNull] YarnSpinnerParser.Jump_destinationContext context)
        {
            jumps.Add(context.Stop);
            return base.VisitJump_destination(context);
        }

        public override bool VisitCommand_name_rule([NotNull] YarnSpinnerParser.Command_name_ruleContext context)
        {
            commands.Add(context.Stop);

            return base.VisitCommand_name_rule(context);
        }

        public override bool VisitFunction_name([NotNull] YarnSpinnerParser.Function_nameContext context)
        {
            functions.Add(context.Stop);
            return base.VisitFunction_name(context);
        }

        public override bool VisitFunction_call([NotNull] YarnSpinnerParser.Function_callContext context)
        {
            Range parametersRange;
            if (context.lparens == null)
            {
                parametersRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.function_name().Stop).CollapseToEnd();
            }
            else if (context.rparens == null)
            {
                parametersRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.lparens, context.Stop);
                parametersRange = new Range(parametersRange.Start.Delta(0, 1), parametersRange.End.Delta(0, -1));
            }
            else
            {
                parametersRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.lparens, context.rparens);
                parametersRange = new Range(parametersRange.Start.Delta(0, 1), parametersRange.End.Delta(0, -1));
            }

            var parameterCount = context.function_parameter().Count();
            var commas = context.COMMA();
            var left = parametersRange.Start;
            var parameterRanges = new List<Range>(commas.Count() + 1);
            foreach (var right in commas.Select(c => PositionHelper.GetRange(yarnFileData.LineStarts, c.Symbol).End))
            {
                parameterRanges.Add(new Range(left, right.Delta(0, -1)));
                left = right;
            }

            parameterRanges.Add(new Range(left, parametersRange.End));



            functionInfos.Add(new YarnFunctionCall
            {
                NameToken = context.function_name().Start,
                Name = context.function_name().Start.Text,
                ExpressionRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.Start, context.Stop),
                ParameterRanges = parameterRanges,
                ParameterCount = parameterCount,
                IsCommand = false,
                ParametersRange = parametersRange,
            });

            return base.VisitFunction_call(context);
        }

        public override bool VisitDeclare_statement([NotNull] YarnSpinnerParser.Declare_statementContext context)
        {
            var token = context.declared_variable().Stop;
            var documentation = GetDocumentComments(context, true).OrDefault($"(variable) {token.Text}");

            var declaration = new YarnVariableDeclaration
            {
                DefinitionFile = yarnFileData.Uri,
                DefinitionRange = PositionHelper.GetRange(yarnFileData.LineStarts, context.Start, context.Stop),
                Documentation = documentation,
                Name = token.Text,
            };
            declarations.Add(declaration);

            variables.Add(token);
            return base.VisitDeclare_statement(context);
        }

        public override bool VisitCommand_statement([NotNull] YarnSpinnerParser.Command_statementContext context)
        {
            var commandtext = context.command_formatted_text();
            if (commandtext != null)
            {
                var parameterRangeStart = PositionHelper.GetRange(yarnFileData.LineStarts, commandtext.Start).End
                    .Delta(0, 1); // need at least one white space character after the command name before any parameters
                var parameterRangeEnd = PositionHelper.GetRange(yarnFileData.LineStarts, context.COMMAND_TEXT_END().Symbol).Start;

                var parameters = commandtext.command_parameter();
                var parameterCount = parameters.Count();
                var parameterRanges = new List<Range>(Math.Max(1, parameterCount));
                if (parameterCount == 0)
                {
                    parameterRanges.Add(new Range(parameterRangeStart, parameterRangeEnd));
                } else
                {
                    var left = parameterRangeStart;
                    foreach (var right in parameters.Select(p => PositionHelper.GetRange(yarnFileData.LineStarts, p.Stop).End))
                    {
                        parameterRanges.Add(new Range(left, right));
                        left = right;
                    }

                    parameterRanges[parameterCount - 1] = new Range(parameterRanges[parameterCount - 1].Start, parameterRangeEnd);
                }

                var result = new YarnFunctionCall
                {
                    NameToken = commandtext.command_name_rule().Start,
                    Name = commandtext.command_name_rule().Start.Text,
                    ExpressionRange = PositionHelper.GetRange(yarnFileData.LineStarts, commandtext.Start, context.COMMAND_TEXT_END().Symbol),
                    ParametersRange = new Range(parameterRangeStart, parameterRangeEnd),
                    ParameterRanges = parameterRanges,
                    ParameterCount = parameterCount,
                    IsCommand = true,
                };

                result.ExpressionRange = new Range(result.ExpressionRange.Start, result.ExpressionRange.End.Delta(0, -2)); // should get right up to the left of >>

                commandInfos.Add(result);
            }

            return base.VisitCommand_statement(context);
        }

        public string GetDocumentComments(ParserRuleContext context, bool allowCommentsAfter = true)
        {
            string description = null;

            var precedingComments = tokens.GetHiddenTokensToLeft(context.Start.TokenIndex, YarnSpinnerLexer.COMMENTS);

            if (precedingComments != null)
            {
                var precedingDocComments = precedingComments

                    // There are no tokens on the main channel with this
                    // one on the same line
                    .Where(t => tokens.GetTokens()
                        .Where(ot => ot.Line == t.Line)
                        .Where(ot => ot.Type != YarnSpinnerLexer.INDENT && ot.Type != YarnSpinnerLexer.DEDENT)
                        .Where(ot => ot.Channel == YarnSpinnerLexer.DefaultTokenChannel)
                        .Count() == 0)
                    .Where(t => t.Text.StartsWith("///")) // The comment starts with a triple-slash
                    .Select(t => t.Text.Replace("///", string.Empty).Trim()); // Get its text

                if (precedingDocComments.Count() > 0)
                {
                    description = string.Join(" ", precedingDocComments);
                }
            }

            if (allowCommentsAfter)
            {
                var subsequentComments = tokens.GetHiddenTokensToRight(context.Stop.TokenIndex, YarnSpinnerLexer.COMMENTS);
                if (subsequentComments != null)
                {
                    var subsequentDocComment = subsequentComments
                        .Where(t => t.Line == context.Stop.Line) // This comment is on the same line as the end of the declaration
                        .Where(t => t.Text.StartsWith("///")) // The comment starts with a triple-slash
                        .Select(t => t.Text.Replace("///", string.Empty).Trim()) // Get its text
                        .FirstOrDefault(); // Get the first one, or null

                    if (subsequentDocComment != null)
                    {
                        description = subsequentDocComment;
                    }
                }
            }

            return description;
        }
    }
}