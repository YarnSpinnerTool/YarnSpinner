// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using static Yarn.Instruction.Types;

    // the visitor for the body of the node does not really return ints, just
    // has to return something might be worth later investigating returning
    // Instructions
    internal partial class CodeGenerationVisitor : YarnSpinnerParserBaseVisitor<int>
    {
        private Compiler compiler;

        internal string trackingEnabled = null;

        public CodeGenerationVisitor(Compiler compiler, string trackingEnabled)
        {
            this.compiler = compiler;
            this.trackingEnabled = trackingEnabled;
        }

        private int GenerateCodeForExpressionsInFormattedText(IList<IParseTree> nodes)
        {
            int expressionCount = 0;

            // First, visit all of the nodes, which are either terminal text
            // nodes or expressions. if they're expressions, we evaluate them,
            // and inject a positional reference into the final string.
            foreach (var child in nodes)
            {
                if (child is ITerminalNode)
                {
                    // nothing to do; string assembly will have been done by the
                    // StringTableGeneratorVisitor
                }
                else if (child is ParserRuleContext)
                {
                    // assume that this is an expression (the parser only
                    // permits them to be expressions, but we can't specify that
                    // here) - visit it, and we will emit code that pushes the
                    // final value of this expression onto the stack. running
                    // the line will pop these expressions off the stack.
                    this.Visit(child);
                    expressionCount += 1;
                }
            }

            return expressionCount;
        }

        // a regular ol' line of text
        public override int VisitLine_statement(YarnSpinnerParser.Line_statementContext context)
        {
            // TODO: add support for line conditions:
            //
            // Mae: here's a line <<if true>>
            //
            // is identical to
            //
            // <<if true>> Mae: here's a line <<endif>>

            // Evaluate the inline expressions and push the results onto the
            // stack.
            var expressionCount = this.GenerateCodeForExpressionsInFormattedText(context.line_formatted_text().children);

            // Get the lineID for this string from the hashtags
            var lineIDTag = Compiler.GetLineIDTag(context.hashtag());

            if (lineIDTag == null)
            {
                throw new InvalidOperationException("Internal error: line should have an implicit or explicit line ID tag, but none was found");
            }

            var lineID = lineIDTag.text.Text;

            this.compiler.Emit(OpCode.RunLine, context.Start, new Operand(lineID), new Operand(expressionCount));

            return 0;
        }

        // A set command: explicitly setting a value to an expression <<set $foo
        // to 1>>
        public override int VisitSet_statement([NotNull] YarnSpinnerParser.Set_statementContext context)
        {
            // Ensure that the correct result is on the stack by evaluating the
            // expression. If this assignment includes an operation (e.g. +=),
            // do that work here too.
            switch (context.op.Type)
            {
                case YarnSpinnerLexer.OPERATOR_ASSIGNMENT:
                    this.Visit(context.expression());
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_ADDITION_EQUALS:
                    this.GenerateCodeForOperation(Operator.Add, context.op, context.expression().Type, context.variable(), context.expression());
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION_EQUALS:
                    this.GenerateCodeForOperation(Operator.Minus, context.op, context.expression().Type, context.variable(), context.expression());
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION_EQUALS:
                    this.GenerateCodeForOperation(Operator.Multiply, context.op, context.expression().Type, context.variable(), context.expression());
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_DIVISION_EQUALS:
                    this.GenerateCodeForOperation(Operator.Divide, context.op, context.expression().Type, context.variable(), context.expression());
                    break;
                case YarnSpinnerLexer.OPERATOR_MATHS_MODULUS_EQUALS:
                    this.GenerateCodeForOperation(Operator.Modulo, context.op, context.expression().Type, context.variable(), context.expression());
                    break;
            }

            // now store the variable and clean up the stack
            string variableName = context.variable().GetText();
            this.compiler.Emit(OpCode.StoreVariable, context.Start, new Operand(variableName));
            this.compiler.Emit(OpCode.Pop, context.Start);
            return 0;
        }

        public override int VisitCall_statement(YarnSpinnerParser.Call_statementContext context)
        {
            // Visit our function call, which will invoke the function
            this.Visit(context.function_call());

            // TODO: if this function returns a value, it will be pushed onto
            // the stack, but there's no way for the compiler to know that, so
            // the stack will not be tidied up. is there a way for that to work?
            return 0;
        }

        // semi-free form text that gets passed along to the game for things
        // like <<turn fred left>> or <<unlockAchievement FacePlant>>
        public override int VisitCommand_statement(YarnSpinnerParser.Command_statementContext context)
        {
            var expressionCount = 0;
            var sb = new StringBuilder();
            foreach (var node in context.command_formatted_text().children)
            {
                if (node is ITerminalNode)
                {
                    sb.Append(node.GetText());
                }
                else if (node is ParserRuleContext)
                {
                    // Generate code for evaluating the expression at runtime
                    this.Visit(node);

                    // Don't include the '{' and '}', because it will have been
                    // added as a terminal node already
                    sb.Append(expressionCount);
                    expressionCount += 1;
                }
            }

            var composedString = sb.ToString();

            // TODO: look into replacing this as it seems a bit odd
            switch (composedString)
            {
                case "stop":
                    // "stop" is a special command that immediately stops
                    // execution
                    this.compiler.Emit(OpCode.Stop, context.command_formatted_text().Start);
                    break;
                default:
                    this.compiler.Emit(OpCode.RunCommand, context.command_formatted_text().Start, new Operand(composedString), new Operand(expressionCount));
                    break;
            }

            return 0;
        }

        // emits the required bytecode for the function call
        private void GenerateCodeForFunctionCall(string functionName, YarnSpinnerParser.Function_callContext functionContext, YarnSpinnerParser.ExpressionContext[] parameters)
        {
            // generate the instructions for all of the parameters
            foreach (var parameter in parameters)
            {
                this.Visit(parameter);
            }

            // push the number of parameters onto the stack
            this.compiler.Emit(OpCode.PushFloat, functionContext.Start, new Operand(parameters.Length));

            // then call the function itself
            this.compiler.Emit(OpCode.CallFunc, functionContext.Start, new Operand(functionName));
        }

        // handles emiting the correct instructions for the function
        public override int VisitFunction_call(YarnSpinnerParser.Function_callContext context)
        {
            string functionName = context.FUNC_ID().GetText();

            this.GenerateCodeForFunctionCall(functionName, context, context.expression());

            return 0;
        }

        // if statement ifclause (elseifclause)* (elseclause)? <<endif>>
        public override int VisitIf_statement(YarnSpinnerParser.If_statementContext context)
        {
            context.AddErrorNode(null);

            // label to give us a jump point for when the if finishes
            string endOfIfStatementLabel = this.compiler.RegisterLabel("endif");

            // handle the if
            var ifClause = context.if_clause();
            this.GenerateClause(endOfIfStatementLabel, ifClause, ifClause.statement(), ifClause.expression());

            // all elseifs
            foreach (var elseIfClause in context.else_if_clause())
            {
                this.GenerateClause(endOfIfStatementLabel, elseIfClause, elseIfClause.statement(), elseIfClause.expression());
            }

            // the else, if there is one
            var elseClause = context.else_clause();
            if (elseClause != null)
            {
                this.GenerateClause(endOfIfStatementLabel, elseClause, elseClause.statement(), null);
            }

            this.compiler.CurrentNode.Labels.Add(endOfIfStatementLabel, this.compiler.CurrentNode.Instructions.Count);

            return 0;
        }

        private void GenerateClause(string jumpLabel, ParserRuleContext clauseContext, YarnSpinnerParser.StatementContext[] children, YarnSpinnerParser.ExpressionContext expression)
        {
            string endOfClauseLabel = this.compiler.RegisterLabel("skipclause");

            // handling the expression (if it has one) will only be called on
            // ifs and elseifs
            if (expression != null)
            {
                // Code-generate the expression
                this.Visit(expression);

                this.compiler.Emit(OpCode.JumpIfFalse, expression.Start, new Operand(endOfClauseLabel));
            }

            // running through all of the children statements
            foreach (var child in children)
            {
                this.Visit(child);
            }

            this.compiler.Emit(OpCode.JumpTo, clauseContext.Stop, new Operand(jumpLabel));

            if (expression != null)
            {
                this.compiler.CurrentNode.Labels.Add(endOfClauseLabel, this.compiler.CurrentNode.Instructions.Count);
                this.compiler.Emit(OpCode.Pop, clauseContext.Stop);
            }
        }

        // for the shortcut options (-> line of text <<if expression>> indent
        // statements dedent)+
        public override int VisitShortcut_option_statement(YarnSpinnerParser.Shortcut_option_statementContext context)
        {
            string endOfGroupLabel = this.compiler.RegisterLabel("group_end");

            var labels = new List<string>();

            int optionCount = 0;

            // For each option, create an internal destination label that, if
            // the user selects the option, control flow jumps to. Then,
            // evaluate its associated line_statement, and use that as the
            // option text. Finally, add this option to the list of upcoming
            // options.
            foreach (var shortcut in context.shortcut_option())
            {
                // Generate the name of internal label that we'll jump to if
                // this option is selected. We'll emit the label itself later.
                string optionDestinationLabel = this.compiler.RegisterLabel($"shortcutoption_{this.compiler.CurrentNode.Name ?? "node"}_{optionCount + 1}");
                labels.Add(optionDestinationLabel);

                // This line statement may have a condition on it. If it does,
                // emit code that evaluates the condition, and add a flag on the
                // 'Add Option' instruction that indicates that a condition
                // exists.
                bool hasLineCondition = false;
                if (shortcut.line_statement()?.line_condition()?.expression() != null)
                {
                    // Evaluate the condition, and leave it on the stack
                    this.Visit(shortcut.line_statement().line_condition().expression());

                    hasLineCondition = true;
                }

                // We can now prepare and add the option.

                // Start by figuring out the text that we want to add. This will
                // involve evaluating any inline expressions.
                var expressionCount = this.GenerateCodeForExpressionsInFormattedText(shortcut.line_statement().line_formatted_text().children);

                // Get the line ID from the hashtags if it has one
                var lineIDTag = Compiler.GetLineIDTag(shortcut.line_statement().hashtag());
                string lineID = lineIDTag.text.Text;

                if (lineIDTag == null)
                {
                    throw new InvalidOperationException("Internal error: no line ID provided");
                }

                // And add this option to the list.
                this.compiler.Emit(
                    OpCode.AddOption,
                    shortcut.line_statement().Start,
                    new Operand(lineID),
                    new Operand(optionDestinationLabel),
                    new Operand(expressionCount),
                    new Operand(hasLineCondition));

                optionCount++;
            }

            // All of the options that we intend to show are now ready to go.
            this.compiler.Emit(OpCode.ShowOptions, context.Stop);

            // The top of the stack now contains the name of the label we want
            // to jump to. Jump to it now.
            this.compiler.Emit(OpCode.Jump, context.Stop);

            // We'll now emit the labels and code associated with each option.
            optionCount = 0;
            foreach (var shortcut in context.shortcut_option())
            {
                // Emit the label for this option's code
                this.compiler.CurrentNode.Labels.Add(labels[optionCount], this.compiler.CurrentNode.Instructions.Count);

                // Run through all the children statements of the shortcut
                // option.
                foreach (var child in shortcut.statement())
                {
                    this.Visit(child);
                }

                // Jump to the end of this shortcut option group.
                this.compiler.Emit(OpCode.JumpTo, shortcut.Stop, new Operand(endOfGroupLabel));

                optionCount++;
            }

            // We made it to the end! Mark the end of the group, so we can jump
            // to it.
            this.compiler.CurrentNode.Labels.Add(endOfGroupLabel, this.compiler.CurrentNode.Instructions.Count);
            this.compiler.Emit(OpCode.Pop, context.Stop);

            return 0;
        }

        // the calls for the various operations and expressions first the
        // special cases (), unary -, !, and if it is just a value by itself
        #region specialCaseCalls

        // (expression)
        public override int VisitExpParens(YarnSpinnerParser.ExpParensContext context)
        {
            return this.Visit(context.expression());
        }

        // -expression
        public override int VisitExpNegative(YarnSpinnerParser.ExpNegativeContext context)
        {
            this.GenerateCodeForOperation(Operator.UnaryMinus, context.op, context.Type, context.expression());

            return 0;
        }

        // (not NOT !)expression
        public override int VisitExpNot(YarnSpinnerParser.ExpNotContext context)
        {
            this.GenerateCodeForOperation(Operator.Not, context.op, context.Type, context.expression());

            return 0;
        }

        // variable
        public override int VisitExpValue(YarnSpinnerParser.ExpValueContext context)
        {
            return this.Visit(context.value());
        }
        #endregion

        #region lValueOperatorrValueCalls

        /// <summary>
        /// Emits code that calls a method appropriate for the operator
        /// <paramref name="op"/> on the type <paramref name="type"/>, given the operands <paramref name="operands"/>.
        /// </summary>
        /// <param name="op">The operation to perform on <paramref name="operands"/>.</param>
        /// <param name="operatorToken">The first token in the statement that is responsible for this operation.</param>
        /// <param name="type">The type of the expression.</param>
        /// <param name="operands">The operands to perform the operation <paramref name="op"/> on.</param>
        /// <exception cref="InvalidOperationException">Thrown when there is no matching instructions for the <paramref name="op"/></exception>
        private void GenerateCodeForOperation(Operator op, IToken operatorToken, Yarn.IType type, params ParserRuleContext[] operands)
        {
            // Generate code for each of the operands, so that their value is
            // now on the stack.
            foreach (var operand in operands)
            {
                this.Visit(operand);
            }

            // Indicate that we are pushing this many items for comparison
            this.compiler.Emit(OpCode.PushFloat, operatorToken, new Operand(operands.Length));

            // Figure out the canonical name for the method that the VM should
            // invoke in order to perform this work
            Yarn.IType implementingType = TypeUtil.FindImplementingTypeForMethod(type, op.ToString());

            // Couldn't find an implementation method? That's an error! The type
            // checker should have caught this.
            if (implementingType == null)
            {
                throw new InvalidOperationException($"Internal error: Codegen failed to get implementation type for {op} given input type {type.Name}.");
            }

            string functionName = TypeUtil.GetCanonicalNameForMethod(implementingType, op.ToString());

            // Call that function.
            this.compiler.Emit(OpCode.CallFunc, operatorToken, new Operand(functionName));
        }
        
        private void GenerateTrackingCode(string variableName)
        {
            GenerateTrackingCode(this.compiler, variableName);
        }

        // really ought to make this emit like a list of opcodes actually
        public static void GenerateTrackingCode(Compiler compiler, string variableName)
        {
            // pushing the var and the increment onto the stack
            compiler.Emit(OpCode.PushVariable, new Operand(variableName));
            compiler.Emit(OpCode.PushFloat, new Operand(1));

            // Indicate that we are pushing this many items for comparison
            compiler.Emit(OpCode.PushFloat, new Operand(2));

            // calling the function
            compiler.Emit(OpCode.CallFunc, new Operand("Number.Add"));

            // now store the variable and clean up the stack
            compiler.Emit(OpCode.StoreVariable, new Operand(variableName));
            compiler.Emit(OpCode.Pop);
        }

        // * / %
        public override int VisitExpMultDivMod(YarnSpinnerParser.ExpMultDivModContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.Type, context.expression(0), context.expression(1));

            return 0;
        }

        // + -
        public override int VisitExpAddSub(YarnSpinnerParser.ExpAddSubContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.Type, context.expression(0), context.expression(1));

            return 0;
        }

        // < <= > >=
        public override int VisitExpComparison(YarnSpinnerParser.ExpComparisonContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.Type, context.expression(0), context.expression(1));

            return 0;
        }

        // == !=
        public override int VisitExpEquality(YarnSpinnerParser.ExpEqualityContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.Type, context.expression(0), context.expression(1));

            return 0;
        }

        // and && or || xor ^
        public override int VisitExpAndOrXor(YarnSpinnerParser.ExpAndOrXorContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.Type, context.expression(0), context.expression(1));

            return 0;
        }
        #endregion

        // the calls for the various value types this is a wee bit messy but is
        // easy to extend, easy to read and requires minimal checking as ANTLR
        // has already done all that does have code duplication though
        #region valueCalls
        public override int VisitValueVar(YarnSpinnerParser.ValueVarContext context)
        {
            return this.Visit(context.variable());
        }

        public override int VisitValueNumber(YarnSpinnerParser.ValueNumberContext context)
        {
            float number = float.Parse(context.NUMBER().GetText(), CultureInfo.InvariantCulture);
            this.compiler.Emit(OpCode.PushFloat, context.Start, new Operand(number));

            return 0;
        }

        public override int VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            this.compiler.Emit(OpCode.PushBool, context.Start, new Operand(true));

            return 0;
        }

        public override int VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            this.compiler.Emit(OpCode.PushBool, context.Start, new Operand(false));
            return 0;
        }

        public override int VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            string variableName = context.VAR_ID().GetText();
            this.compiler.Emit(OpCode.PushVariable, context.Start, new Operand(variableName));

            return 0;
        }

        public override int VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            // stripping the " off the front and back actually is this what we
            // want?
            string stringVal = context.STRING().GetText().Trim('"');

            this.compiler.Emit(OpCode.PushString, context.Start, new Operand(stringVal));

            return 0;
        }

        // all we need do is visit the function itself, it will handle
        // everything
        public override int VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            this.Visit(context.function_call());

            return 0;
        }

        // null value
        public override int VisitValueNull(YarnSpinnerParser.ValueNullContext context)
        {
            this.compiler.Emit(OpCode.PushNull, context.Start);
            return 0;
        }
        #endregion

        public override int VisitDeclare_statement(YarnSpinnerParser.Declare_statementContext context)
        {
            // Declare statements do not participate in code generation
            return 0;
        }

        // A <<jump>> command, which immediately jumps to another node, given
        // its name.
        public override int VisitJumpToNodeName([NotNull] YarnSpinnerParser.JumpToNodeNameContext context)
        {
            if (trackingEnabled != null)
            {
                GenerateTrackingCode(trackingEnabled);
            }

            compiler.Emit(OpCode.PushString, context.destination, new Operand(context.destination.Text));
            compiler.Emit(OpCode.RunNode, context.Start);

            return 0;
        }

        // A <<jump>> command, which immediately jumps to another node, given an
        // expression that resolves to a node's name.
        public override int VisitJumpToExpression([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            if (trackingEnabled != null)
            {
                GenerateTrackingCode(trackingEnabled);
            }

            // Evaluate the expression, and jump to the result on the stack.
            this.Visit(context.expression());
            this.compiler.Emit(OpCode.RunNode, context.Start);

            return 0;
        }

        // TODO: figure out a better way to do operators
        internal static readonly Dictionary<int, Operator> TokensToOperators = new Dictionary<int, Operator>
        {
            // operators for the standard expressions
            { YarnSpinnerLexer.OPERATOR_LOGICAL_LESS_THAN_EQUALS, Operator.LessThanOrEqualTo },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER_THAN_EQUALS, Operator.GreaterThanOrEqualTo },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_LESS, Operator.LessThan },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_GREATER, Operator.GreaterThan },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_EQUALS, Operator.EqualTo },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_NOT_EQUALS, Operator.NotEqualTo },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_AND, Operator.And },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_OR, Operator.Or },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_XOR, Operator.Xor },
            { YarnSpinnerLexer.OPERATOR_LOGICAL_NOT, Operator.Not },
            { YarnSpinnerLexer.OPERATOR_MATHS_ADDITION, Operator.Add },
            { YarnSpinnerLexer.OPERATOR_MATHS_SUBTRACTION, Operator.Minus },
            { YarnSpinnerLexer.OPERATOR_MATHS_MULTIPLICATION, Operator.Multiply },
            { YarnSpinnerLexer.OPERATOR_MATHS_DIVISION, Operator.Divide },
            { YarnSpinnerLexer.OPERATOR_MATHS_MODULUS, Operator.Modulo },
        };
    }
}
