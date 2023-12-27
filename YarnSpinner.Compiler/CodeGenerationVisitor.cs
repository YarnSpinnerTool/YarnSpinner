// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using static Yarn.Instruction.Types;

    // the visitor for the body of the node does not really return ints, just
    // has to return something might be worth later investigating returning
    // Instructions
    internal partial class CodeGenerationVisitor : YarnSpinnerParserBaseVisitor<int>
    {
        private ICodeEmitter compiler;

        internal string? trackingVariableName = null;

        public CodeGenerationVisitor(ICodeEmitter compiler, string? trackingVariableName)
        {
            this.compiler = compiler;
            this.trackingVariableName = trackingVariableName;
        }

        private int CurrentInstructionNumber
        {
            get
            {
                Node currentNode = this.compiler.CurrentNode ?? throw new InvalidOperationException($"Can't get current instruction number: {nameof(this.compiler.CurrentNode)} is null");
                return currentNode.Instructions.Count;
            }
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

            if (compiler.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(compiler.CurrentNode)} was null when generating code for a line expression");
            }

            // Get the lineID for this string from the hashtags
            var lineID = Compiler.GetLineID(context);

            // Evaluate the inline expressions and push the results onto the
            // stack.
            var expressionCount = this.GenerateCodeForExpressionsInFormattedText(context.line_formatted_text().children);

            // Run the line.
            this.compiler.Emit(
                context.Start,
                new Instruction
                {
                    RunLine = new RunLineInstruction { LineID = lineID, SubstitutionCount = expressionCount }
                }
            );

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

            this.compiler.Emit(
                context.Start,
                new Instruction { StoreVariable = new StoreVariableInstruction { VariableName = variableName } },
                new Instruction { Pop = new PopInstruction { } }
            );
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
                    this.compiler.Emit(
                        context.command_formatted_text().Start,
                        new Instruction { Stop = new StopInstruction { } }
                    );
                    
                    break;
                default:
                    this.compiler.Emit(
                        context.command_formatted_text().Start,
                        new Instruction
                        {
                            RunCommand = new RunCommandInstruction {
                                CommandText = composedString,
                                SubstitutionCount = expressionCount,
                            }
                        }
                    );

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

            this.compiler.Emit(
                functionContext.Start,
                // push the number of parameters onto the stack
                new Instruction { PushFloat = new PushFloatInstruction { Value = parameters.Length } },
                // then call the function itself
                new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = functionName } }
            );
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
            var jumpsToEndOfIfStatement = new List<Instruction>();
            
            // handle the if
            var ifClause = context.if_clause();
            {
                this.GenerateClause(out var jumpFromEndOfPrimaryClause, ifClause, ifClause.statement(), ifClause.expression());
                jumpsToEndOfIfStatement.Add(jumpFromEndOfPrimaryClause);
            }

            // all elseifs
            foreach (var elseIfClause in context.else_if_clause())
            {
                this.GenerateClause(out var jumpFromEndOfClause, elseIfClause, elseIfClause.statement(), elseIfClause.expression());
                jumpsToEndOfIfStatement.Add(jumpFromEndOfClause);
            }

            // the else, if there is one
            var elseClause = context.else_clause();
            if (elseClause != null)
            {
                this.GenerateClause(out var jumpFromEndOfClause, elseClause, elseClause.statement(), null);
                jumpsToEndOfIfStatement.Add(jumpFromEndOfClause);
            }

            if (this.compiler.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: can't add a new label, because CurrentNode is null");
            }

            this.compiler.CurrentNodeDebugInfo?.AddLabel("endif", CurrentInstructionNumber);
            foreach (var jump in jumpsToEndOfIfStatement) {
                jump.Destination = CurrentInstructionNumber;
            }

            return 0;
        }

        private void GenerateClause(out Instruction jumpToEndOfIfStatement, ParserRuleContext clauseContext, YarnSpinnerParser.StatementContext[] children, YarnSpinnerParser.ExpressionContext? expression)
        {
            Instruction? jumpToEndOfClause = null;
            // handling the expression (if it has one) will only be called on
            // ifs and elseifs
            if (expression != null)
            {
                // Code-generate the expression
                this.Visit(expression);

                this.compiler.Emit(
                    expression.Start,
                    jumpToEndOfClause = new Instruction { JumpIfFalse =  new JumpIfFalseInstruction { Destination = -1 } }
                );
            }

            // running through all of the children statements
            foreach (var child in children)
            {
                this.Visit(child);
            }

            this.compiler.Emit(
                clauseContext.Stop,
                jumpToEndOfIfStatement = new Instruction { JumpTo = new JumpToInstruction { Destination = -1 } }
            );

            if (expression != null)
            {
                if (this.compiler.CurrentNode == null)
                {
                    throw new InvalidOperationException($"Internal error: can't add a new label, because CurrentNode is null");
                }

                if (jumpToEndOfClause != null)
                {
                    jumpToEndOfClause.Destination = CurrentInstructionNumber;
                    this.compiler.CurrentNodeDebugInfo?.AddLabel("end_clause", CurrentInstructionNumber);
                }

                this.compiler.Emit(
                    clauseContext.Stop,
                    new Instruction { Pop = new PopInstruction { } }
                );
            }
        }

        // for the shortcut options (-> line of text <<if expression>> indent
        // statements dedent)+
        public override int VisitShortcut_option_statement(YarnSpinnerParser.Shortcut_option_statementContext context)
        {
            if (this.compiler.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: can't codegen an option group because CurrentNode is null");
            }

            var addOptionInstructions = new List<Instruction>();
            var jumpToEndOfGroupInstructions = new List<Instruction>();

            int optionCount = 0;

            // For each option, create an internal destination label that, if
            // the user selects the option, control flow jumps to. Then,
            // evaluate its associated line_statement, and use that as the
            // option text. Finally, add this option to the list of upcoming
            // options.
            foreach (var shortcut in context.shortcut_option())
            {
                // Get the line ID for the shortcut's line
                var lineID = Compiler.GetLineID(shortcut.line_statement());

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

                Instruction addOptionInstruction;


                // And add this option to the list.
                this.compiler.Emit(
                    shortcut.line_statement().Start,
                    addOptionInstruction = new Instruction {
                        AddOption = new AddOptionInstruction {
                            LineID = lineID,
                            Destination = -1,
                            SubstitutionCount = expressionCount,
                            HasCondition = hasLineCondition,
                        }
                    });

                addOptionInstructions.Add(addOptionInstruction);

                optionCount++;
            }

            this.compiler.Emit(
                context.Stop,
                // All of the options that we intend to show are now ready to
                // go. Show them.
                new Instruction { ShowOptions = new ShowOptionsInstruction { } },
                // The top of the stack now contains the name of the label we
                // want to jump to. Jump to it now.
                new Instruction { PeekAndJump = new PeekAndJumpInstruction { } }
            );

            // We'll now emit the labels and code associated with each option.
            optionCount = 0;
            foreach (var shortcut in context.shortcut_option())
            {
                // Create the label for this option's code
                this.compiler.CurrentNodeDebugInfo?.AddLabel($"option_{optionCount}", CurrentInstructionNumber);

                // Make this option's AddOption instruction point at where we
                // are now.
                addOptionInstructions[optionCount].Destination = CurrentInstructionNumber;

                // Run through all the children statements of the  option.
                foreach (var child in shortcut.statement())
                {
                    this.Visit(child);
                }

                Instruction jumpToEnd;

                // Jump to the end of this shortcut option group.
                this.compiler.Emit(
                    shortcut.Stop,
                    jumpToEnd = new Instruction { JumpTo = new JumpToInstruction { Destination = -1 } }
                );

                jumpToEndOfGroupInstructions.Add(jumpToEnd);

                optionCount++;
            }

            // We made it to the end! Update all jump-to-end instructions to
            // point to where we are now.
            this.compiler.CurrentNodeDebugInfo?.AddLabel("group_end", CurrentInstructionNumber);

            foreach (var jump in jumpToEndOfGroupInstructions) {
                jump.Destination = CurrentInstructionNumber;
            }

            this.compiler.Emit(
                context.Stop,
                new Instruction { Pop = new PopInstruction { } }
            );

            return 0;
        }

        public override int VisitLine_group_statement(YarnSpinnerParser.Line_group_statementContext context)
        {
            if (this.compiler.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: can't codegen a line group, because CurrentNode is null");
            }

            var labels = new Dictionary<YarnSpinnerParser.Line_group_itemContext, string>();

            int optionCount = 0;

            // Each line group item works like this:
            // - If present, the condition is evaluated.
            //   - If the condition is false, this item is skipped.
            // - If the condition is true or not present, a function call is
            //   made:
            //   - 'Yarn.Internal.add_line_group_candidate(label,
            //     condition_count, line_id)'
            //     - label: The point in the program to jump to if this
            //       candidate is selected
            //     - condition_count: The number of values present in the line's
            //       condition (0 if not present)
            //     - line_id: The line ID of the line (which also serves as a
            //       unique ID for the line condition.)
            // - Each call to add_line_candidate adds an entry to an internal
            //   list in the VM.
            // - Finally, after all line group items have been checked in this
            //   way, a call to 'Yarn.Internal.select_line_candidate' is made.
            //   This causes the VM to query its saliency strategy to find the
            //   specific item to run, and returns the appropriate label to jump
            //   to.

            bool anyItemHadEmptyCondition = false;

            List<Instruction> jumpsToEndOfLineGroup = new List<Instruction>();

            var jumpsToLineGroupItems = new Dictionary<YarnSpinnerParser.Line_group_itemContext, Instruction>();

            foreach (var lineGroupItem in context.line_group_item())
            {
                var lineStatement = lineGroupItem.line_statement();
                var expression = lineStatement.line_condition()?.expression();

                var lineID = Compiler.GetLineID(lineGroupItem.line_statement());

                // This line group item has an expression. Evaluate it.
                if (expression != null)
                {
                    Instruction skipThisItemInstruction;
                    IToken? conditionToken = null;
                    int conditionCount = 0;

                    this.Visit(expression);
                    conditionToken = expression.Start;

                    // Get the number of values that this expression
                    // examines
                    conditionCount += GetValuesInExpression(expression);

                    // If this evaluates to false, skip to the end of the
                    // expression and do not register this line group item
                    this.compiler.Emit(
                        conditionToken,
                        skipThisItemInstruction = new Instruction { 
                            JumpIfFalse = new JumpIfFalseInstruction { Destination = -1 } 
                        }
                    );

                    // Call the 'add candidate' function
                    EmitCodeForRegisteringLineGroupItem(lineGroupItem, conditionCount);

                    // Update our jump to point at where we are now
                    this.compiler.CurrentNodeDebugInfo?.AddLabel("line_group_eval_end", CurrentInstructionNumber);
                    skipThisItemInstruction.Destination = CurrentInstructionNumber;
                }
                else
                {
                    // There is no expression; call the add candidate function
                    // unconditionally
                    EmitCodeForRegisteringLineGroupItem(lineGroupItem, 0);

                    // Remember that at least one line group item had no
                    // condition (and therefore there will always be an option
                    // that can be selected, which means we don't need to handle
                    // the case of "nothing is selectable")
                    anyItemHadEmptyCondition = true;
                }

                optionCount += 1;
            }

            void EmitCodeForRegisteringLineGroupItem(YarnSpinnerParser.Line_group_itemContext lineGroupItem, int conditionCount)
            {
                var lineStatement = lineGroupItem.line_statement();
                var lineIDTag = Compiler.GetLineIDTag(lineStatement.hashtag());
                var lineID = lineIDTag?.text?.Text ?? throw new InvalidOperationException("Internal compiler error: line ID for line group item was not present");

                Instruction runThisLineInstruction;

                // Push the parameters (destination, condition count (= 0), line id)
                // in reverse order
                this.compiler.Emit(
                    lineStatement.Start,
                    // line ID for this option (arg 3)
                    new Instruction { PushString = new PushStringInstruction { Value = lineID } },
                    // condition count (arg 2)
                    new Instruction { PushFloat = new PushFloatInstruction { Value = conditionCount } },
                    // destination if selected (arg 1)
                    runThisLineInstruction = new Instruction { PushFloat = new PushFloatInstruction { Value = -1 } },
                    // instruction count
                    new Instruction { PushFloat = new PushFloatInstruction { Value = 3 } },
                    new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = VirtualMachine.AddLineGroupCandidateFunctionName } }
                );

                jumpsToLineGroupItems[lineGroupItem] = runThisLineInstruction;
            }

            if (anyItemHadEmptyCondition == false)
            {
                // All items had a condition. We need to handle the event where
                // all conditions fail, so we'll register an item that jumps
                // straight to the end of the line group.

                Instruction jumpToEnd;

                this.compiler.Emit(
                    context.Stop,
                    new Instruction { PushString = new PushStringInstruction { Value = VirtualMachine.LineGroupCandidate.NoneContentID } },
                    new Instruction { PushFloat = new PushFloatInstruction { Value = 0 } },
                    jumpToEnd = new Instruction { PushFloat = new PushFloatInstruction { Value = -1 } },
                    new Instruction { PushFloat = new PushFloatInstruction { Value = 3 } },
                    new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = VirtualMachine.AddLineGroupCandidateFunctionName } }
                );

                jumpsToEndOfLineGroup.Add(jumpToEnd);
            }

            // We've added all of our candidates; now query which one to jump to
            this.compiler.Emit(context.Start,
                new Instruction { PushFloat = new PushFloatInstruction { Value = 0 } },
                new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = VirtualMachine.SelectLineGroupCandidateFunctionName } },
                // After this call, the appropriate label to jump to will be on the
                // stack.
                new Instruction { PeekAndJump = new PeekAndJumpInstruction { } }
            );

            // Now generate the code for each of the lines in the group.
            foreach (var lineGroupItem in context.line_group_item())
            {
                // Mark that the instruction to jump to a specific 
                jumpsToLineGroupItems[lineGroupItem].Destination = CurrentInstructionNumber;

                // Mark that this instruction, which we jump to, should have a
                // label
                this.compiler.CurrentNodeDebugInfo?.AddLabel("run_line_group_item", CurrentInstructionNumber);

                // Evaluate this line. 
                this.Visit(lineGroupItem.line_statement());

                // For each child, evaluate that too
                foreach (var childStatement in lineGroupItem.statement())
                {
                    this.Visit(childStatement);
                }
                Instruction jumpToEnd;

                this.compiler.Emit(
                    lineGroupItem.Stop,
                    jumpToEnd = new Instruction { JumpTo = new JumpToInstruction { Destination = -1 } }
                );
                jumpsToEndOfLineGroup.Add(jumpToEnd);
            }

            // Mark all jumps to the end of the group as being here
            this.compiler.CurrentNodeDebugInfo?.AddLabel("line_group_end", CurrentInstructionNumber);
            foreach (var i in jumpsToEndOfLineGroup)
            {
                i.Destination = CurrentInstructionNumber;
            }

            // Pop the instruction number that represents the selected item
            // destination
            this.compiler.Emit(context.Stop, new Instruction { Pop = new PopInstruction { } });

            return 0;
        }

        private static string GetFunctionName(IType type, Operator op)
        {
            TypeBase implementingType = TypeUtil.FindImplementingTypeForMethod(type, op.ToString());
            if (implementingType == null)
            {
                throw new InvalidOperationException($"Internal error: Codegen failed to get implementation type for {op} given input type {type}.");
            }

            string functionName = TypeUtil.GetCanonicalNameForMethod(implementingType, op.ToString());
            return functionName;
        }

        /// <summary>
        /// Gets the total number of values - functions calls, variables, and
        /// constant values - present in an expression and its sub-expressions.
        /// </summary>
        /// <remarks>Function call arguments and their sub-expressions are not
        /// included in the count.</remarks>
        /// <param name="context">An expression.</param>
        /// <returns>The total number of values in the expression.</returns>
        private static int GetValuesInExpression(YarnSpinnerParser.ExpressionContext context)
        {
            if (context is YarnSpinnerParser.ExpValueContext)
            {
                return 1;
            }
            else
            {
                var accum = 0;
                foreach (var child in context.children)
                {
                    if (child is YarnSpinnerParser.ExpressionContext exp)
                    {
                        accum += GetValuesInExpression(exp);
                    }
                }
                return accum;
            }
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
        /// <paramref name="op"/> on the type <paramref name="type"/>, given the
        /// operands <paramref name="operands"/>.
        /// </summary>
        /// <param name="op">The operation to perform on <paramref
        /// name="operands"/>.</param>
        /// <param name="operatorToken">The first token in the statement that is
        /// responsible for this operation.</param>
        /// <param name="type">The type of the expression.</param>
        /// <param name="operands">The operands to perform the operation
        /// <paramref name="op"/> on.</param>
        /// <exception cref="InvalidOperationException">Thrown when there is no
        /// matching instructions for the <paramref name="op"/></exception>
        private void GenerateCodeForOperation(Operator op, IToken operatorToken, Yarn.IType type, params ParserRuleContext[] operands)
        {
            // Generate code for each of the operands, so that their value is
            // now on the stack.
            foreach (var operand in operands)
            {
                this.Visit(operand);
            }

            // Figure out the canonical name for the method that the VM should
            // invoke in order to perform this work
            TypeBase implementingType = TypeUtil.FindImplementingTypeForMethod(type, op.ToString());

            // Couldn't find an implementation method? That's an error! The type
            // checker should have caught this.
            if (implementingType == null)
            {
                throw new InvalidOperationException($"Internal error: Codegen failed to get implementation type for {op} given input type {type.Name}.");
            }

            string functionName = TypeUtil.GetCanonicalNameForMethod(implementingType, op.ToString());

            this.compiler.Emit(
                operatorToken,
                // Indicate that we are pushing this many items for comparison
                new Instruction { PushFloat = new PushFloatInstruction { Value = operands.Length } },
                // Call that function.
                new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = functionName } }
            );
        }

        private void GenerateTrackingCode(string variableName, IToken sourceToken)
        {
            GenerateTrackingCode(this.compiler, variableName, sourceToken);
        }

        public static void GenerateTrackingCode(ICodeEmitter compiler, string variableName, IToken sourceToken)
        {
            compiler.Emit(
                sourceToken,
                // pushing the var and the increment onto the stack
                new Instruction { PushVariable = new PushVariableInstruction { VariableName = variableName } },
                new Instruction { PushFloat = new PushFloatInstruction { Value = 1 } },
                // Indicate that we are pushing this many items for comparison
                new Instruction { PushFloat = new PushFloatInstruction { Value = 2 } },
                // calling the function to add them together
                new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = GetFunctionName(Types.Number, Operator.Add) } },
                // now store the variable and clean up the stack
                new Instruction { StoreVariable = new StoreVariableInstruction { VariableName = variableName } },
                new Instruction { Pop = new PopInstruction { } }
            );
        }

        // * / %
        public override int VisitExpMultDivMod(YarnSpinnerParser.ExpMultDivModContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.expression(0).Type, context.expression(0), context.expression(1));

            return 0;
        }

        // + -
        public override int VisitExpAddSub(YarnSpinnerParser.ExpAddSubContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.expression(0).Type, context.expression(0), context.expression(1));

            return 0;
        }

        // < <= > >=
        public override int VisitExpComparison(YarnSpinnerParser.ExpComparisonContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.expression(0).Type, context.expression(0), context.expression(1));

            return 0;
        }

        // == !=
        public override int VisitExpEquality(YarnSpinnerParser.ExpEqualityContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.expression(0).Type, context.expression(0), context.expression(1));

            return 0;
        }

        // and && or || xor ^
        public override int VisitExpAndOrXor(YarnSpinnerParser.ExpAndOrXorContext context)
        {
            this.GenerateCodeForOperation(TokensToOperators[context.op.Type], context.op, context.expression(0).Type, context.expression(0), context.expression(1));

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

            this.compiler.Emit(
                context.Start, 
                new Instruction { PushFloat = new PushFloatInstruction { Value = number } }
            );

            return 0;
        }

        public override int VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            this.compiler.Emit(
                context.Start, 
                new Instruction { PushBool = new PushBoolInstruction { Value = true } }
            );

            return 0;
        }

        public override int VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            this.compiler.Emit(
                context.Start, 
                new Instruction { PushBool = new PushBoolInstruction { Value = false } }
            );
            return 0;
        }

        public override int VisitVariable(YarnSpinnerParser.VariableContext context)
        {
            string variableName = context.VAR_ID().GetText();

            // Get the declaration for this variable
            if (this.compiler.VariableDeclarations.TryGetValue(variableName, out var declaration) == false)
            {
                throw new System.InvalidOperationException($"Internal error during code generation: variable {variableName} has no declaration");
            }

            // Is this variable a 'smart variable'?
            if (declaration.IsInlineExpansion)
            {
                // Then code-generate its parse tree. (We won't hit an infinite
                // loop, because we already checked that during type-checking.)
                this.Visit(declaration.InitialValueParserContext);
            }
            else
            {
                // Otherwise, generate the code that fetches the variable from
                // storage.
                this.compiler.Emit(
                    context.Start, 
                    new Instruction { PushVariable = new PushVariableInstruction { VariableName = variableName} }
                );
            }

            return 0;
        }

        public override int VisitValueString(YarnSpinnerParser.ValueStringContext context)
        {
            // stripping the " off the front and back actually is this what we
            // want?
            string stringVal = context.STRING().GetText().Trim('"');
            this.compiler.Emit(
                context.Start, 
                new Instruction { PushString = new PushStringInstruction { Value =  stringVal } }
            );

            return 0;
        }

        // all we need do is visit the function itself, it will handle
        // everything
        public override int VisitValueFunc(YarnSpinnerParser.ValueFuncContext context)
        {
            this.Visit(context.function_call());

            return 0;
        }

        // A reference to a constant property member of a type (eg Food.Apple).
        // We need to find the underlying value of that type and push that.
        public override int VisitValueTypeMemberReference([NotNull] YarnSpinnerParser.ValueTypeMemberReferenceContext context)
        {
            var type = context.Type;

            var memberName = context.typeMemberReference().memberName.Text;

            if (type.TypeMembers.TryGetValue(memberName, out var member) == false)
            {
                throw new System.InvalidOperationException($"Internal error during code generation: type {type} has no member {memberName}");
            }

            if (!(member is ConstantTypeProperty property))
            {
                throw new System.InvalidOperationException($"Internal error during code generation: {type.Name}.{memberName} is not a {nameof(ConstantTypeProperty)}");
            }

            var value = property.Value;

            var propertyType = property.Type;
            if (propertyType is EnumType @enum)
            {
                propertyType = @enum.RawType;
            }

            // Raw values are permitted to be a string, or a number
            if (propertyType == Types.String)
            {
                this.compiler.Emit(
                    context.Start, 
                    new Instruction { PushString = new PushStringInstruction { Value = value.ToString() } }
                );
            }
            else if (propertyType == Types.Number)
            {
                this.compiler.Emit(
                    context.Start, 
                    new Instruction { PushFloat = new PushFloatInstruction { Value = value.ToSingle(CultureInfo.InvariantCulture) } }
                );
            }
            else
            {
                throw new InvalidOperationException($"Internal error: {type.Name}.{memberName} has type {property.Type.Name}, which is not allowed.");
            }

            return 0;
        }
        #endregion

        public override int VisitDeclare_statement(YarnSpinnerParser.Declare_statementContext context)
        {
            // Declare statements for variables do not participate in code
            // generation. (Declarations for smart variables are code-generated
            // at a different stage.)
            return 0;
        }

        // A <<jump>> command, which immediately jumps to another node, given
        // its name.
        public override int VisitJumpToNodeName([NotNull] YarnSpinnerParser.JumpToNodeNameContext context)
        {
            if (trackingVariableName != null)
            {
                GenerateTrackingCode(trackingVariableName, context.Start);
            }

            this.compiler.Emit(context.Start, 
                new Instruction { RunNode = new RunNodeInstruction { NodeName = context.destination.Text }}
            );

            return 0;
        }

        // A <<jump>> command, which immediately jumps to another node, given an
        // expression that resolves to a node's name.
        public override int VisitJumpToExpression([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            if (trackingVariableName != null)
            {
                GenerateTrackingCode(trackingVariableName, context.Start);
            }

            // Evaluate the expression, and jump to the result on the stack.
            this.Visit(context.expression());
            this.compiler.Emit(context.Start, new Instruction { PeekAndRunNode = new PeekAndRunNodeInstruction { } });

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
