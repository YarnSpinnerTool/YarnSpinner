// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Antlr4.Runtime;

    using static Instruction.Types;

    public class NodeGroupCompiler : ICodeEmitter
    {
        /// <summary>
        /// Initializes a new instance of the NodeGroupCompiler class.
        /// </summary>
        /// <param name="nodeGroupName"></param>
        /// <param name="variableDeclarations"></param>
        /// <param name="nodes"></param>
        public NodeGroupCompiler(string nodeGroupName, IDictionary<string, Declaration> variableDeclarations, IEnumerable<YarnSpinnerParser.NodeContext> nodes)
        {
            this.VariableDeclarations = variableDeclarations;
            this.Nodes = nodes;
            this.CurrentNode = new Node
            {
                Name = nodeGroupName
            };
            this.CurrentNodeDebugInfo = new NodeDebugInfo("<generated>", nodeGroupName);
        }

        public Node CurrentNode { get; private set; }

        public NodeDebugInfo CurrentNodeDebugInfo { get; private set; }

        public IDictionary<string, Declaration> VariableDeclarations { get; set; }

        private IEnumerable<YarnSpinnerParser.NodeContext> Nodes { get; }


        public (Node,NodeDebugInfo) GetResult()
        {
            var codeGenerator = new CodeGenerationVisitor(this);


            var jumpToNodeInstructions = new Dictionary<YarnSpinnerParser.NodeContext, Instruction>();
            Instruction noContentAvailableJump;



            foreach (var node in this.Nodes)
            {
                var conditionCount = 0;
                if (node.NodeTitle == null) {
                    // The node doesn't have a title. We can't use it.
                    continue;
                }

                var whenHeaders = node.GetHeaders(SpecialHeaderNames.WhenHeader);

                var whenConditions = whenHeaders
                    .Select(h => h.header_when_expression())
                    .Where(e => e != null && (e.expression() != null || e.once != null));

                for (int i = 0; i < whenConditions.Count(); i++)
                {
                    var condition = whenConditions.ElementAt(i);

                    var expression = condition.expression();

                    var once = condition.once != null;

                    if (expression != null)
                    {
                        conditionCount += CodeGenerationVisitor.GetValueCountInExpression(expression);

                        // Evaluate the expression
                        codeGenerator.Visit(expression);
                    }

                    if (once)
                    {
                        conditionCount += 1;

                        // Emit code that checks that the 'once' variable has
                        // not yet been set.
                        var onceVariable = Compiler.GetContentViewedVariableName(node.NodeTitle);

                        this.Emit(
                            condition.once,
                            new Instruction
                            {
                                PushVariable = new PushVariableInstruction
                                {
                                    VariableName = onceVariable
                                },
                            },
                            new Instruction
                            {
                                PushFloat = new PushFloatInstruction { Value = 1 }
                            },
                            new Instruction
                            {
                                CallFunc = new CallFunctionInstruction
                                {
                                    FunctionName = CodeGenerationVisitor.GetFunctionName(
                                        Types.Boolean,
                                         Operator.Not
                                    )
                                }
                            }
                        );
                    }

                    if (expression != null && once)
                    {
                        // 'and' the two together
                        this.Emit(
                            condition.once,
                            new Instruction
                            {
                                PushFloat = new PushFloatInstruction { Value = 2 },
                            },
                            new Instruction
                            {
                                CallFunc = new CallFunctionInstruction
                                {
                                    FunctionName = CodeGenerationVisitor.GetFunctionName(Types.Boolean, Operator.And)
                                }
                            });
                    }

                    if (i != 0)
                    {
                        // This isn't 'and' it with the previous result
                        this.Emit(
                            condition.Start,
                            new Instruction
                            {
                                PushFloat = new PushFloatInstruction { Value = 2 },
                            },
                            new Instruction
                            {
                                CallFunc = new CallFunctionInstruction
                                {
                                    FunctionName = CodeGenerationVisitor.GetFunctionName(Types.Boolean, Operator.And)
                                }
                            });
                    }
                }

                if (whenConditions.Count() == 0) {
                    // This node is part of a node group, but it didn't have any
                    // condition expressions. Push 'true' to ensure that it
                    // runs.
                    this.Emit(
                        node.GetHeader(SpecialHeaderNames.TitleHeader)?.Start,
                        new Instruction
                        {
                            PushBool = new PushBoolInstruction { Value = true }
                        }
                    );
                }

                Instruction skipRegistration;

                // If it's false, jump over the call to add this as a candidate
                this.Emit(
                    node.GetHeader(SpecialHeaderNames.TitleHeader)?.header_key,
                    skipRegistration = new Instruction
                    {
                        JumpIfFalse = new Instruction.Types.JumpIfFalseInstruction
                        {
                            Destination = -1,
                        }
                    }
                );

                Instruction runThisNodeInstruction;

                // Otherwise, add it as a candidate
                this.Emit(
                    node.GetHeader(SpecialHeaderNames.TitleHeader)?.header_value,
                    // line ID for this option (arg 3)
                    new Instruction { PushString = new PushStringInstruction { Value = node.NodeTitle } },
                    // condition count (arg 2)
                    new Instruction { PushFloat = new PushFloatInstruction { Value = conditionCount } },
                    // destination if selected (arg 1)
                    runThisNodeInstruction = new Instruction { PushFloat = new PushFloatInstruction { Value = -1 } },
                    // instruction count
                    new Instruction { PushFloat = new PushFloatInstruction { Value = 3 } },
                    new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = VirtualMachine.AddLineGroupCandidateFunctionName } }
                );

                jumpToNodeInstructions[node] = runThisNodeInstruction;

                // Mark the point where we'd skip to if we were not adding this
                // as a candidate
                skipRegistration.Destination = this.CurrentInstructionNumber;
                CurrentNodeDebugInfo.AddLabel("nodegroup_skip_registering_" + node.NodeTitle, this.CurrentInstructionNumber);
            }


            // Consult the saliency strategy
            // We've added all of our candidates; now query which one to jump to
            this.Emit(null,
                // Where to jump to if there are no candidates
                noContentAvailableJump = new Instruction { PushFloat = new PushFloatInstruction { Value = -1 } },
                // The number of parameters (1)
                new Instruction { PushFloat = new PushFloatInstruction { Value = 1 } },
                // Call the function
                new Instruction { CallFunc = new CallFunctionInstruction { FunctionName = VirtualMachine.SelectLineGroupCandidateFunctionName } },
                // After this call, the appropriate label to jump to will be on the
                // stack.
                new Instruction { PeekAndJump = new PeekAndJumpInstruction { } }
            );

            // Generate code that jumps to each node
            foreach (var node in Nodes)
            {
                jumpToNodeInstructions[node].Destination = CurrentInstructionNumber;

                // If any of the 'when' conditions had a 'once' in them, set the
                // variable for it now.
                var whenHeaders = node.GetHeaders(SpecialHeaderNames.WhenHeader);
                var onceHeader = whenHeaders.FirstOrDefault(h => h.header_when_expression().once != null);
                if (onceHeader != null && node.NodeTitle != null) {
                    var onceVariable = Compiler.GetContentViewedVariableName(node.NodeTitle);

                    this.Emit(
                        onceHeader.header_when_expression().once,
                        new Instruction
                        {
                            PushBool = new PushBoolInstruction { Value = true }
                        },
                        new Instruction
                        {
                            StoreVariable = new StoreVariableInstruction
                            {
                                VariableName = onceVariable
                            }
                        },
                        new Instruction
                        {
                            Pop = new PopInstruction { }
                        }
                    );
                }

                this.CurrentNodeDebugInfo.AddLabel($"nodegroup_run_{node.NodeTitle}", CurrentInstructionNumber);

                // Detour into to the node, and then return when we're done.
                this.Emit(
                    node.GetHeader(SpecialHeaderNames.TitleHeader)?.header_value,
                    new Instruction
                    {
                        DetourToNode = new DetourToNodeInstruction
                        {
                            NodeName = node.NodeTitle
                        }
                    },
                    new Instruction {
                        Return = new ReturnInstruction { }
                    }
                );
            }

            // If we made it here, then no content was available. Return to
            // where we were called from.
            noContentAvailableJump.Destination = CurrentInstructionNumber;
            this.CurrentNodeDebugInfo.AddLabel("nodegroup_none_viable", CurrentInstructionNumber);
            this.Emit(
                null,
                new Instruction
                {
                    Return = new ReturnInstruction { }
                }
            );

            return (CurrentNode, CurrentNodeDebugInfo);
        }

        /// <inheritdoc/>
        public void Emit(IToken? startToken, Instruction instruction)
        {
            Compiler.Emit(this.CurrentNode ?? throw new InvalidOperationException(),
                          this.CurrentNodeDebugInfo ?? throw new InvalidOperationException(),
                          startToken?.Line - 1 ?? -1,
                          startToken?.Column ?? -1,
                          instruction);
        }

        /// <inheritdoc/>
        public void Emit(IToken? startToken, params Instruction[] instructions)
        {
            foreach (var instruction in instructions) {
                this.Emit(startToken, instruction);
            }
        }

        /// <inheritdoc/>
        public void Emit(Instruction instruction)
        {
            this.Emit(null, instruction);
        }

        private int CurrentInstructionNumber
        {
            get
            {
                Node currentNode = this.CurrentNode ?? throw new InvalidOperationException($"Can't get current instruction number: {nameof(this.CurrentNode)} is null");
                return currentNode.Instructions.Count;
            }
        }
    }
}
