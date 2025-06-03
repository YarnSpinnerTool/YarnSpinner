// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Antlr4.Runtime;
    using Antlr4.Runtime.Misc;
    using Antlr4.Runtime.Tree;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text;
    using static Yarn.Instruction.Types;

    // the visitor for the body of the node does not really return ints, just
    // has to return something might be worth later investigating returning
    // Instructions
    internal partial class CodeGenerationVisitor : YarnSpinnerParserBaseVisitor<int>
    {
        private readonly ICodeEmitter compiler;

        public CodeGenerationVisitor(ICodeEmitter compiler)
        {
            this.compiler = compiler;
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
            if (compiler.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: {nameof(compiler.CurrentNode)} was null when generating code for a line expression");
            }

            // Get the lineID for this string from the hashtags
            var lineID = Compiler.GetContentID(ContentIdentifierType.Line, context);

            // Evaluate any condition on the line statement.
            EvaluateLineCondition(context, out _);

            Instruction? jumpIfConditionFalseInstruction = null;

            bool hasAnyCondition = context.GetConditionType() != ParserContextExtensions.ContentConditionType.NoCondition;

            if (hasAnyCondition)
            {
                // Jump over this line if the line's condition is false.
                this.compiler.Emit(
                    context.line_condition().Start,
                    context.line_condition().Stop,
                    jumpIfConditionFalseInstruction = new Instruction
                    {
                        JumpIfFalse = new JumpIfFalseInstruction { Destination = -1 }
                    }
                );
            }

            if (context.line_condition() is YarnSpinnerParser.LineOnceConditionContext once)
            {
                // The line has a 'once' condition that has evaluated to true.
                // Set the corresponding variable for it so that it doesn't
                // appear again.
                this.compiler.Emit(
                    once.COMMAND_ONCE().Symbol,
                    once.COMMAND_ONCE().Symbol,
                    new Instruction { PushBool = new PushBoolInstruction { Value = true } },
                    new Instruction { StoreVariable = new StoreVariableInstruction { VariableName = Compiler.GetContentViewedVariableName(lineID) } },
                    new Instruction { Pop = new PopInstruction { } }
                );
            }

            // Evaluate the inline expressions and push the results onto the
            // stack.
            var expressionCount = this.GenerateCodeForExpressionsInFormattedText(context.line_formatted_text().children);

            // Run the line.
            this.compiler.Emit(
                context.line_formatted_text().Start,
                context.line_formatted_text().Stop,
                new Instruction
                {
                    RunLine = new RunLineInstruction { LineID = lineID, SubstitutionCount = expressionCount }
                }
            );

            if (jumpIfConditionFalseInstruction != null)
            {
                // We generated a jump instruction to jump over the line. Update
                // its destination to immediately after the line.
                jumpIfConditionFalseInstruction.Destination = this.CurrentInstructionNumber;
                this.compiler.CurrentNodeDebugInfo?.AddLabel("skip_line", this.CurrentInstructionNumber);
            }

            if (hasAnyCondition)
            {
                // We evaluated a condition, and the value is still on the
                // stack. Pop the bool off the stack.
                this.compiler.Emit(
                    context.line_condition().Start,
                    context.line_condition().Stop,
                    new Instruction { Pop = new PopInstruction { } }
                );
            }

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
                context.Stop,
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

            // If a compiler-defined effect is present for this command, emit
            // the necessary code for it instead of emitting a RunCommand
            // instruction like we would normally
            if (context.commandEffect != null)
            {
                switch (context.commandEffect)
                {
                    case Statements.SetBoolVariableCommandEffect setBool:
                        this.compiler.Emit(
                            context.command_formatted_text().Start,
                            context.command_formatted_text().Stop,
                            new Instruction { PushBool = new PushBoolInstruction { Value = setBool.Value } },
                            new Instruction { StoreVariable = new StoreVariableInstruction { VariableName = setBool.VariableName } },
                            new Instruction { Pop = new PopInstruction { } }
                        );
                        break;
                    case Statements.NoOpCommandEffect _:
                        // no-op
                        break;
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown command effect type {context.commandEffect.GetType()}");
                }
                return 0;
            }

            foreach (var node in context.command_formatted_text()?.children ?? Array.Empty<IParseTree>())
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
                        context.command_formatted_text().Stop,
                        new Instruction { Stop = new StopInstruction { } }
                    );

                    break;
                default:
                    this.compiler.Emit(
                        context.command_formatted_text().Start,
                        context.command_formatted_text().Stop,
                        new Instruction
                        {
                            RunCommand = new RunCommandInstruction
                            {
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
                functionContext.Stop,
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
            foreach (var jump in jumpsToEndOfIfStatement)
            {
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
                    expression.Stop,
                    jumpToEndOfClause = new Instruction { JumpIfFalse = new JumpIfFalseInstruction { Destination = -1 } }
                );
            }

            // running through all of the children statements
            foreach (var child in children)
            {
                this.Visit(child);
            }

            this.compiler.Emit(
                clauseContext.Stop,
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

            var onceVariables = new Dictionary<YarnSpinnerParser.Shortcut_optionContext, string?>();

            // For each option, create an internal destination label that, if
            // the user selects the option, control flow jumps to. Then,
            // evaluate its associated line_statement, and use that as the
            // option text. Finally, add this option to the list of upcoming
            // options.
            foreach (var shortcut in context.shortcut_option())
            {
                // Get the line ID for the shortcut's line
                var lineID = Compiler.GetContentID(ContentIdentifierType.Line, shortcut.line_statement());

                // This line statement may have a condition on it. If it does,
                // emit code that evaluates the condition, and add a flag on the
                // 'Add Option' instruction that indicates that a condition
                // exists.
                EvaluateLineCondition(shortcut.line_statement(), out var onceVariable);
                onceVariables[shortcut] = onceVariable;

                bool hasAnyCondition = shortcut.line_statement().GetConditionType() != ParserContextExtensions.ContentConditionType.NoCondition;

                // We can now prepare and add the option.

                // Start by figuring out the text that we want to add. This will
                // involve evaluating any inline expressions.
                var expressionCount = this.GenerateCodeForExpressionsInFormattedText(shortcut.line_statement().line_formatted_text().children);

                Instruction addOptionInstruction;

                // And add this option to the list.
                this.compiler.Emit(
                    shortcut.line_statement().Start,
                    shortcut.line_statement().Stop,
                    addOptionInstruction = new Instruction
                    {
                        AddOption = new AddOptionInstruction
                        {
                            LineID = lineID,
                            Destination = -1,
                            SubstitutionCount = expressionCount,
                            HasCondition = hasAnyCondition,
                        }
                    });

                addOptionInstructions.Add(addOptionInstruction);

                optionCount++;
            }

            this.compiler.Emit(
                context.Start,
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

                if (shortcut.line_statement().line_condition() is YarnSpinnerParser.LineOnceConditionContext once)
                {
                    // This option has a 'once' condition on it. Generate code
                    // that sets the corresponding 'once' variable for this
                    // option to true, so that we don't see it again.
                    this.compiler.Emit(
                        once.COMMAND_ONCE().Symbol,
                        once.COMMAND_ONCE().Symbol,
                        new Instruction
                        {
                            PushBool = new PushBoolInstruction { Value = true }
                        },
                        new Instruction
                        {
                            StoreVariable = new StoreVariableInstruction { VariableName = onceVariables[shortcut] }
                        },
                        new Instruction { Pop = new PopInstruction { } }
                    );
                }

                // Run through all the children statements of the  option.
                foreach (var child in shortcut.statement())
                {
                    this.Visit(child);
                }

                Instruction jumpToEnd;

                // Jump to the end of this shortcut option group.
                this.compiler.Emit(
                    shortcut.Stop,
                    shortcut.Stop,
                    jumpToEnd = new Instruction { JumpTo = new JumpToInstruction { Destination = -1 } }
                );

                jumpToEndOfGroupInstructions.Add(jumpToEnd);

                optionCount++;
            }

            // We made it to the end! Update all jump-to-end instructions to
            // point to where we are now.
            this.compiler.CurrentNodeDebugInfo?.AddLabel("group_end", CurrentInstructionNumber);

            foreach (var jump in jumpToEndOfGroupInstructions)
            {
                jump.Destination = CurrentInstructionNumber;
            }

            this.compiler.Emit(
                context.Stop,
                context.Stop,
                new Instruction { Pop = new PopInstruction { } }
            );

            return 0;
        }

        /// <summary>
        /// Generates code that evaluates any conditions present on the line and
        /// places the result on the stack.
        /// </summary>
        /// <param name="lineStatement">The line statement containing
        /// potential condition information.</param>
        /// <param name="onceVariable">An output parameter to store the name of
        /// the relevant 'once' variable for this statement if present, or <see
        /// langword="null"/> if not.</param>
        private void EvaluateLineCondition(YarnSpinnerParser.Line_statementContext lineStatement, out string? onceVariable)
        {
            onceVariable = null;

            switch (lineStatement?.GetConditionType())
            {
                case ParserContextExtensions.ContentConditionType.OnceCondition:
                    var condition = (lineStatement.line_condition() as YarnSpinnerParser.LineOnceConditionContext)!;

                    var lineID = Compiler.GetContentID(ContentIdentifierType.Line, lineStatement);
                    onceVariable = Compiler.GetContentViewedVariableName(lineID);

                    // Test to see if the 'once' variable for this content is
                    // false

                    this.compiler.Emit(
                        condition.COMMAND_ONCE().Symbol,
                        condition.COMMAND_ONCE().Symbol,
                        new Instruction
                        {
                            PushVariable = new PushVariableInstruction
                            {
                                VariableName = onceVariable
                            }
                        },
                        new Instruction
                        {
                            // one argument for 'not'
                            PushFloat = new PushFloatInstruction { Value = 1 }
                        },
                        new Instruction
                        {
                            // 'not' the variable
                            CallFunc = new CallFunctionInstruction
                            {
                                FunctionName = GetFunctionName(Types.Boolean, Operator.Not)
                            }
                        }
                    );

                    // If the condition has an expression, evaluate that too
                    // and 'and' it with the 'once' test we just evaluated
                    if (condition.expression() is YarnSpinnerParser.ExpressionContext expr)
                    {
                        Visit(condition.expression());

                        this.compiler.Emit(
                            expr.Start,
                            expr.Stop,
                            new Instruction
                            {
                                PushFloat = new PushFloatInstruction
                                {
                                    Value = 2
                                }
                            },
                            new Instruction
                            {
                                CallFunc = new CallFunctionInstruction
                                {
                                    FunctionName = GetFunctionName(Types.Boolean, Operator.And)
                                }
                            }
                        );
                    }

                    break;
                case ParserContextExtensions.ContentConditionType.RegularCondition:
                    // Evaluate the condition and put it on the stack
                    Visit((lineStatement.line_condition() as YarnSpinnerParser.LineConditionContext)!.expression());
                    break;
                case null:
                    // No condition; nothing to do.
                    break;
            }
        }

        public override int VisitLine_group_statement(YarnSpinnerParser.Line_group_statementContext context)
        {
            if (this.compiler.CurrentNode == null)
            {
                throw new InvalidOperationException($"Internal error: can't codegen a line group, because CurrentNode is null");
            }

            int optionCount = 0;

            // Each line group item works like this:
            // - For each item in the line group, evaluate its condition if it
            //   has one, or else push true onto the stack. Then, tell the VM to
            //   add a candidate for that item.
            // - Next, tell the VM to select the best item. If one was selected,
            //   then the stack will contain (true, destination); otherwise, it
            //   will contain (false).
            // - If we had content, jump to the appropriate destination, which
            //   contains code for displaying the line, and then jumps to the
            //   end of the line group's instructions. (If the line group's
            //   condition was a 'once' or 'once if' condition, we update the
            //   'once' flag for the item here.)
            // - If we didn't, jump straight to the end immediately.

            List<Instruction> jumpsToEndOfLineGroup = new List<Instruction>();

            var onceVariables = new Dictionary<YarnSpinnerParser.Line_group_itemContext, string?>();

            var addCandidateInstructions = new Dictionary<YarnSpinnerParser.Line_group_itemContext, Instruction>();

            this.compiler.CurrentNodeDebugInfo?.AddLabel("line_group_start", CurrentInstructionNumber);


            foreach (var lineGroupItem in context.line_group_item())
            {
                var lineStatement = lineGroupItem.line_statement();

                var lineID = Compiler.GetContentID(ContentIdentifierType.Line, lineGroupItem.line_statement());

                Instruction addCandidateInstruction;

                int conditionCount;

                if (lineStatement.line_condition() != null)
                {
                    // This line group item has an expression. Evaluate it -
                    // this will result in the expression's value being left on
                    // the stack, and possibly give us the name of any 'once'
                    // variable that may be present.

                    EvaluateLineCondition(lineStatement, out string? onceVariableName);

                    if (onceVariableName != null)
                    {
                        onceVariables[lineGroupItem] = onceVariableName;
                    }

                    // Count the number of conditions in the expression:
                    conditionCount = lineStatement.line_condition().ConditionCount;
                }
                else
                {
                    // There is no expression; push 'true' onto the stack and
                    // note that it had a complexity of zero
                    this.compiler.Emit(
                        lineStatement.Start,
                        lineStatement.Stop,
                        new Instruction { PushBool = new PushBoolInstruction { Value = true } }
                    );
                    conditionCount = 0;
                }

                // Add this line group item
                this.compiler.Emit(
                    lineStatement.Start,
                    lineStatement.Stop,
                    addCandidateInstruction = new Instruction
                    {
                        AddSaliencyCandidate = new AddSaliencyCandidateInstruction
                        {
                            ComplexityScore = conditionCount,
                            ContentID = lineID,
                        }
                    }
                );

                // Remember this add candidate instruction - we'll need to
                // update where it jumps to later
                addCandidateInstructions[lineGroupItem] = addCandidateInstruction;

                optionCount += 1;
            }

            Instruction noContentAvailableJump;

            // We've added all of our candidates; now query which one to jump to
            this.compiler.Emit(
                context.Start,
                context.Stop,
                new Instruction { SelectSaliencyCandidate = new SelectSaliencyCandidateInstruction { } }
            );

            // The top of the stack now contains 'true' if a piece of content
            // was selected, and 'false' if not. 
            this.compiler.Emit(
                context.Start,
                context.Stop,
                // If the top of the stack is false, immediately jump to the end
                // of this entire line group.
                noContentAvailableJump = new Instruction { JumpIfFalse = new JumpIfFalseInstruction { Destination = -1 } },
                // Pop the 'has condition' value off the top of the stack.
                new Instruction { Pop = new PopInstruction { } },
                // Jump to the instruction indicated by the top of the stack.
                new Instruction { PeekAndJump = new PeekAndJumpInstruction { } }
            );

            // We'll update the destination of the 'jump to the end of the line
            // group if no content was selected' instruction once we know what
            // instruction number to jump to, so add it to the list of jumps to
            // the end.
            jumpsToEndOfLineGroup.Add(noContentAvailableJump);

            // Now generate the code for each of the lines in the group.
            foreach (var lineGroupItem in context.line_group_item())
            {
                // Ensure that the 'add candidate' instruction that points us to
                // here has the correct destination
                addCandidateInstructions[lineGroupItem].Destination = CurrentInstructionNumber;

                // Mark that this instruction, which we jump to, should have a
                // label
                this.compiler.CurrentNodeDebugInfo?.AddLabel("run_line_group_item", CurrentInstructionNumber);

                // We got here via a peek-and-jump; we can discard the top of
                // the stack now.
                this.compiler.Emit(
                    lineGroupItem.line_statement().Start,
                    lineGroupItem.line_statement().Stop,
                    new Instruction { Pop = new PopInstruction { } }
                );

                if (onceVariables.TryGetValue(lineGroupItem, out var onceVariable) && onceVariable != null)
                {
                    // We have a 'once' variable for this line group item. Emit
                    // code that sets it to 'true', so that we don't see this
                    // item again.
                    IToken token = (lineGroupItem.line_statement()?.line_condition() as YarnSpinnerParser.LineOnceConditionContext)?.COMMAND_ONCE().Symbol ?? lineGroupItem.Start;
                    this.compiler.Emit(
                        token,
                        token,
                        new Instruction
                        {
                            PushBool = new PushBoolInstruction { Value = true },
                        },
                        new Instruction
                        {
                            StoreVariable = new StoreVariableInstruction { VariableName = onceVariable },
                        },
                        new Instruction { Pop = new PopInstruction { } }
                    );
                }

                // Run this line. (We don't call Visit(line_statement), because
                // that would re-evaluate the line condition, which we don't
                // need or want.)

                var lineFormattedText = lineGroupItem.line_statement().line_formatted_text();

                // Evaluate the inline expressions and push the results onto the
                // stack.
                var expressionCount = this.GenerateCodeForExpressionsInFormattedText(lineFormattedText.children);
                var lineID = lineGroupItem.line_statement().LineID;

                // Run the line.
                this.compiler.Emit(
                    lineGroupItem.line_statement().line_formatted_text()?.Start ?? lineGroupItem.Start,
                    lineGroupItem.line_statement().line_formatted_text()?.Stop ?? lineGroupItem.Stop,

                    new Instruction
                    {
                        RunLine = new RunLineInstruction { LineID = lineID, SubstitutionCount = expressionCount }
                    }
                );

                // For each child statement in this line group item, evaluate
                // that too.
                foreach (var childStatement in lineGroupItem.statement())
                {
                    this.Visit(childStatement);
                }

                // Finally, jump to the end of the group
                Instruction jumpToEnd;

                this.compiler.Emit(
                    lineGroupItem.Stop,
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

            return 0;
        }

        internal static string GetFunctionName(IType type, Operator op)
        {
            TypeBase? implementingType = TypeUtil.FindImplementingTypeForMethod(type, op.ToString());
            if (implementingType == null)
            {
                throw new InvalidOperationException($"Internal error: Codegen failed to get implementation type for {op} given input type {type}.");
            }

            string functionName = TypeUtil.GetCanonicalNameForMethod(implementingType, op.ToString());
            return functionName;
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
            TypeBase? implementingType = TypeUtil.FindImplementingTypeForMethod(type, op.ToString());

            // Couldn't find an implementation method? That's an error! The type
            // checker should have caught this.
            if (implementingType == null)
            {
                throw new InvalidOperationException($"Internal error: Codegen failed to get implementation type for {op} given input type {type.Name}.");
            }

            string functionName = TypeUtil.GetCanonicalNameForMethod(implementingType, op.ToString());

            this.compiler.Emit(
                operatorToken,
                operatorToken,
                // Indicate that we are pushing this many items for comparison
                new Instruction { PushFloat = new PushFloatInstruction { Value = operands.Length } },
                // Call that function.
                new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = functionName } }
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
                context.Stop,
                new Instruction { PushFloat = new PushFloatInstruction { Value = number } }
            );

            return 0;
        }

        public override int VisitValueTrue(YarnSpinnerParser.ValueTrueContext context)
        {
            this.compiler.Emit(
                context.Start,
                context.Stop,
                new Instruction { PushBool = new PushBoolInstruction { Value = true } }
            );

            return 0;
        }

        public override int VisitValueFalse(YarnSpinnerParser.ValueFalseContext context)
        {
            this.compiler.Emit(
                context.Start,
                context.Stop,
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
                    context.Stop,
                    new Instruction { PushVariable = new PushVariableInstruction { VariableName = variableName } }
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
                context.Stop,
                new Instruction { PushString = new PushStringInstruction { Value = stringVal } }
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
                    context.Stop,
                    new Instruction { PushString = new PushStringInstruction { Value = value.ToString() } }
                );
            }
            else if (propertyType == Types.Number)
            {
                this.compiler.Emit(
                    context.Start,
                    context.Stop,
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
            EmitJumpToNamedNode(context, context.destination.Text, detour: false);

            return 0;
        }


        // A <<jump>> command, which immediately jumps to another node, given an
        // expression that resolves to a node's name.
        public override int VisitJumpToExpression([NotNull] YarnSpinnerParser.JumpToExpressionContext context)
        {
            EmitJumpToExpression(context, context.expression(), detour: false);
            return 0;
        }


        private void EmitJumpToNamedNode(ParserRuleContext context, string nodeName, bool detour)
        {
            switch (detour)
            {
                case true:
                    this.compiler.Emit(
                        context.Start,
                        context.Stop,
                        new Instruction { DetourToNode = new DetourToNodeInstruction { NodeName = nodeName } }
                    );
                    break;
                case false:
                    this.compiler.Emit(
                        context.Start,
                        context.Stop,
                        new Instruction { RunNode = new RunNodeInstruction { NodeName = nodeName } }
                    );
                    break;
            }

        }

        private void EmitJumpToExpression(ParserRuleContext context, YarnSpinnerParser.ExpressionContext jumpExpression, bool detour)
        {
            // Evaluate the expression, and jump to the result on the stack.
            this.Visit(jumpExpression);

            switch (detour)
            {
                case true:
                    this.compiler.Emit(
                        context.Start,
                        context.Stop,
                        new Instruction { PeekAndDetourToNode = new PeekAndDetourToNode { } }
                    );
                    break;
                case false:
                    this.compiler.Emit(
                    context.Start,
                    context.Stop,
                    new Instruction { PeekAndRunNode = new PeekAndRunNodeInstruction { } });
                    break;
            }
        }

        public override int VisitDetourToExpression([NotNull] YarnSpinnerParser.DetourToExpressionContext context)
        {
            EmitJumpToExpression(context, context.expression(), detour: true);
            return 0;
        }

        public override int VisitDetourToNodeName([NotNull] YarnSpinnerParser.DetourToNodeNameContext context)
        {
            EmitJumpToNamedNode(context, context.destination.Text, detour: true);
            return 0;
        }

        public override int VisitReturn_statement([NotNull] YarnSpinnerParser.Return_statementContext context)
        {
            this.compiler.Emit(
                context.Start,
                context.Stop,
                new Instruction { Return = new ReturnInstruction { } });
            return 0;
        }

        public override int VisitOnce_statement([NotNull] YarnSpinnerParser.Once_statementContext context)
        {
            // Generate a mostly-unique, mostly-stable 'once' tracking variable
            // for this 'once' statement based on where we are in the
            // compilation - this should be mostly stable across line text
            // edits, but should change if the program changes its structure
            // (i.e. this 'once' statement appears at a different instruction
            // number)
            string onceVariable = context.once_primary_clause().OnceVariableName ?? throw new InvalidOperationException("Internal error: once statement primary clause is missing a once variable");

            // Get the token that represents the 'once' keyword in this
            // statement, so that we can associate the generated instructions
            // with it
            IToken onceToken = context.once_primary_clause().COMMAND_ONCE().Symbol;

            this.compiler.CurrentNodeDebugInfo?.AddLabel("once_start", CurrentInstructionNumber);

            // Evaluate the once variable
            this.compiler.Emit(
                onceToken,
                onceToken,
                new Instruction
                {
                    PushVariable = new PushVariableInstruction { VariableName = onceVariable },
                },
                new Instruction
                {
                    PushFloat = new PushFloatInstruction { Value = 1 },
                },
                new Instruction
                {
                    CallFunc = new CallFunctionInstruction
                    {
                        FunctionName = GetFunctionName(Types.Boolean, Operator.Not)
                    }
                }
            );

            if (context.once_primary_clause().expression() != null)
            {
                // The statement has an expression. Evaluate it and 'and' it
                // with the once variable's evaluation.
                Visit(context.once_primary_clause().expression());

                this.compiler.Emit(
                    onceToken,
                    onceToken,
                    new Instruction
                    {
                        PushFloat = new PushFloatInstruction { Value = 2 },
                    },
                    new Instruction
                    {
                        CallFunc = new CallFunctionInstruction
                        {
                            FunctionName = GetFunctionName(Types.Boolean, Operator.And)
                        }
                    }
            );
            }

            // If the resulting value is false, jump over this content (either
            // to the alternate clause, if present, or otherwise to the end of
            // the statement).
            Instruction jumpOverPrimaryClause;

            this.compiler.Emit(
                onceToken,
                onceToken,
                jumpOverPrimaryClause = new Instruction
                {
                    JumpIfFalse = new JumpIfFalseInstruction { Destination = -1 }
                }
            );

            // If we haven't jumped, we're in the 'once' content. Set the 'once'
            // variable to true so that we don't see it again.
            this.compiler.Emit(
                onceToken,
                onceToken,
                new Instruction
                {
                    PushBool = new PushBoolInstruction { Value = true }
                },
                new Instruction
                {
                    StoreVariable = new StoreVariableInstruction { VariableName = onceVariable }
                },
                new Instruction { Pop = new PopInstruction { } }
            );

            // Evaluate the primary clause
            foreach (var statement in context.once_primary_clause().statement())
            {
                Visit(statement);
            }

            if (context.once_alternate_clause() != null)
            {
                // We have an alternate clause, which we should run if the
                // primary clause did not.
                Instruction jumpOverAlternateClause;

                // Start by jumping over this clause if we DID run the primary
                // clause.
                this.compiler.Emit(
                    context.once_primary_clause().Stop,
                    context.once_primary_clause().Stop,
                    jumpOverAlternateClause = new Instruction
                    {
                        JumpTo = new JumpToInstruction { Destination = -1 }
                    }
                );

                // Make the 'jump over primary clause' jump to where we are now,
                // which is the the start of the alternate clause.
                jumpOverPrimaryClause.Destination = this.CurrentInstructionNumber;
                this.compiler.CurrentNodeDebugInfo?.AddLabel("once_skip_primary", this.CurrentInstructionNumber);

                // Evaluate the alternate clause.
                foreach (var statement in context.once_alternate_clause().statement())
                {
                    Visit(statement);
                }

                // Make the 'jump over alternate clause' instruction point to where we are now, which is the end of the alternate clause (and therefore the end of the entire statement.)
                jumpOverAlternateClause.Destination = this.CurrentInstructionNumber;
                this.compiler.CurrentNodeDebugInfo?.AddLabel("once_skip_alternate", this.CurrentInstructionNumber);

            }
            else
            {
                // Update the 'jump over primary clause' instruction to point to
                // where we are now, which is the end of the statement.
                jumpOverPrimaryClause.Destination = this.CurrentInstructionNumber;
                this.compiler.CurrentNodeDebugInfo?.AddLabel("once_skip_primary", this.CurrentInstructionNumber);
            }

            // Pop the result of evaluating the condition, which was left on the stack.
            this.compiler.Emit(
                context.Stop,
                context.Stop,
                new Instruction { Pop = new PopInstruction { } }
            );

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

    internal static class ParserContextExtensions
    {

        internal enum ContentConditionType
        {
            /// <summary>
            /// The content does not have a condition.
            /// </summary>
            NoCondition,

            /// <summary>
            /// The content has a 'once' condition (possibly with an additional expression).
            /// </summary>
            OnceCondition,

            /// <summary>
            /// The content has a regular condition with an expression.
            /// </summary>
            RegularCondition,
        }

        public static ContentConditionType GetConditionType(this YarnSpinnerParser.Line_statementContext line)
        {
            return GetConditionType(line.line_condition());
        }

        private static ContentConditionType GetConditionType(YarnSpinnerParser.Line_conditionContext? condition)
        {
            if (condition == null)
            {
                return ContentConditionType.NoCondition;
            }

            if (condition is YarnSpinnerParser.LineConditionContext)
            {
                return ContentConditionType.RegularCondition;
            }

            if (condition is YarnSpinnerParser.LineOnceConditionContext)
            {
                return ContentConditionType.OnceCondition;
            }

            throw new InvalidOperationException("Unknown content condition type " + condition.GetType());
        }
    }
}
