using Antlr4.Runtime;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

#nullable enable

namespace Yarn.Compiler
{
    /// <summary>
    /// Custom line rules are expressions that can be parsed from simple
    /// strings, and tested against line statements to check to see if the line
    /// passes the rule.
    /// </summary>
    internal partial class CustomLineRule
    {
        private readonly Operation operation;

        private CustomLineRule(Operation operation)
        {
            this.operation = operation;
        }

        public bool CheckLine(LineStatementSyntaxNode line)
        {
            var value = this.operation.Evaluate(line);
            if (value.Type != ValueType.Bool)
            {
                throw new InvalidOperationException("Expression doesn't evaluate to a bool");
            }
            return value;
        }

        /// <summary>
        /// Converts an ANTLR syntax parse node into an <see cref="Operation"/>.
        /// </summary>
        /// <param name="expr">The ANTLR parse node to convert.</param>
        /// <returns>The parsed operation.</returns>
        /// <exception cref="InvalidOperationException">Thrown when an error
        /// occurs during processing.</exception>
        private static Operation GetOperation(ParserRuleContext? expr)
        {
            if (expr == null)
            {
                throw new InvalidOperationException("Null expression");
            }
            return (expr) switch
            {
                YarnSpinnerParser.ExpParensContext p => GetOperation(p.expression()),

                YarnSpinnerParser.ExpAndOrXorContext p => p.op.Type switch
                {
                    YarnSpinnerLexer.OPERATOR_LOGICAL_AND => new Operation(OperationType.And, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    YarnSpinnerLexer.OPERATOR_LOGICAL_OR => new Operation(OperationType.Or, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    YarnSpinnerLexer.OPERATOR_LOGICAL_XOR => new Operation(OperationType.Xor, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    _ => throw new InvalidOperationException("Unhandled logical expression type " + p.op.Text)
                },

                YarnSpinnerParser.ExpNotContext p => new Operation(OperationType.Not, GetOperation(p.expression())),

                YarnSpinnerParser.ExpComparisonContext p => p.op.Type switch
                {
                    YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER => new Operation(OperationType.GT, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER_THAN_EQUALS => new Operation(OperationType.GTE, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    YarnSpinnerLexer.OPERATOR_LOGICAL_LESS => new Operation(OperationType.LT, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    YarnSpinnerLexer.OPERATOR_LOGICAL_LESS_THAN_EQUALS => new Operation(OperationType.LTE, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    _ => throw new InvalidOperationException("Unhandled comparison type " + p.op.Text)
                },

                YarnSpinnerParser.ExpEqualityContext p => p.op.Type switch
                {
                    YarnSpinnerLexer.OPERATOR_LOGICAL_EQUALS => new Operation(OperationType.EQ, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    YarnSpinnerLexer.OPERATOR_LOGICAL_NOT_EQUALS => new Operation(OperationType.NEQ, GetOperation(p.expression(0)), GetOperation(p.expression(1))),
                    _ => throw new InvalidOperationException("Unhandled comparison type " + p.op.Text)
                },

                YarnSpinnerParser.ExpValueContext p => p.value() switch
                {
                    YarnSpinnerParser.ValueTrueContext => new Operation(true),
                    YarnSpinnerParser.ValueFalseContext => new Operation(false),
                    YarnSpinnerParser.ValueNumberContext v => new Operation(int.Parse(v.NUMBER().GetText())),
                    YarnSpinnerParser.ValueStringContext v => new Operation(v.STRING().GetText().Trim()),

                    YarnSpinnerParser.ValueFuncContext f => f.function_call().FUNC_ID().GetText() switch
                    {
                        Operation.FunctionMatches => new Operation(OperationType.FuncRegex, f.function_call().expression().Select(e => GetOperation(e)).ToArray()),
                        Operation.FunctionLength => new Operation(OperationType.FuncLength, f.function_call().expression().Select(e => GetOperation(e)).ToArray()),
                        Operation.FunctionHasCharacter => new Operation(OperationType.FuncLineHasAnyCharacter, f.function_call().expression().Select(e => GetOperation(e)).ToArray()),
                        Operation.FunctionCharacter => new Operation(OperationType.FuncGetLineCharacter, f.function_call().expression().Select(e => GetOperation(e)).ToArray()),
                        Operation.FunctionHasHashtag => new Operation(OperationType.FuncLineHasHashtag, f.function_call().expression().Select(e => GetOperation(e)).ToArray()),

                        _ => throw new InvalidOperationException("Unhandled function " + f.function_call().FUNC_ID().GetText())
                    },
                    _ => throw new InvalidOperationException("Unhandled value type " + p.GetText())

                },

                _ => throw new InvalidOperationException("Unhandled expr type " + expr?.GetType() ?? "null")
            };
        }

        /// <summary>
        /// Parses a string containing an expression into a custom line rule.
        /// </summary>
        /// <param name="source">The expression to parse.</param>
        /// <returns>The custom line rule.</returns>
        /// <exception cref="ArgumentException">Thrown when there is an
        /// error when parsing the expression.</exception>
        public static CustomLineRule Parse(string source)
        {
            ICharStream input = CharStreams.fromString(source);

            // Create a lexer that uses the Yarn Spinner grammar and put it in a
            // mode ready to lex expressions
            YarnSpinnerLexer lexer = new(input);
            lexer.Mode(YarnSpinnerLexer.ExpressionMode);
            CommonTokenStream tokens = new(lexer);

            YarnSpinnerParser parser = new(tokens)
            {
                // Throw an exception as soon as we get a parse error
                ErrorHandler = new BailErrorStrategy()
            };

            try
            {
                // Parse the string into an expression
                var expr = parser.expression();

                // Convert the parse tree into an operation
                var operation = GetOperation(expr);

                // Produce a new line rule that uses this operation
                return new CustomLineRule(operation);
            }
            catch (Antlr4.Runtime.Misc.ParseCanceledException e)
            {
                throw new ArgumentException("Syntax error when parsing rule", source, e.InnerException);
            }
        }

    }
}
