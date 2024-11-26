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
        /// <param name="nodeGroupName">The name of the node group to
        /// compile.</param>
        /// <param name="variableDeclarations">The collection of existing
        /// variable declarations found during compilation.</param>
        /// <param name="nodeContexts">The collection of node group parser
        /// contexts that all belong to this node group.</param>
        public NodeGroupCompiler(string nodeGroupName, IDictionary<string, Declaration> variableDeclarations, IEnumerable<YarnSpinnerParser.NodeContext> nodeContexts, List<Node> compiledNodes)
        {
            this.NodeGroupName = nodeGroupName;
            this.VariableDeclarations = variableDeclarations;
            this.NodeContexts = nodeContexts;
            this.CompiledNodes = compiledNodes;
        }

        public Node? CurrentNode { get; private set; }

        public NodeDebugInfo? CurrentNodeDebugInfo { get; private set; }
        public string NodeGroupName { get; }
        public IDictionary<string, Declaration> VariableDeclarations { get; set; }

        private IEnumerable<YarnSpinnerParser.NodeContext> NodeContexts { get; }
        public List<Node> CompiledNodes { get; }

        private Node? FindNode(string title)
        {
            return this.CompiledNodes.SingleOrDefault(n => n.Name == title);
        }

        internal IEnumerable<NodeCompilationResult> CompileNodeGroup()
        {
            // Create the list of nodes that we'll end up generating
            List<NodeCompilationResult> generatedNodes = new List<NodeCompilationResult>();

            foreach (var nodeContext in this.NodeContexts)
            {
                var nodeTotalConditionCount = 0;
                if (nodeContext.NodeTitle == null)
                {
                    // The node doesn't have a title. We can't use it.
                    continue;
                }

                // Find the node in the collection of compiled nodes.
                var compiledNode = FindNode(nodeContext.NodeTitle);
                if (compiledNode == null)
                {
                    // The node doesn't exist. (Possibly it was empty, and
                    // omitted as a result.)
                    continue;
                }


                List<string> whenConditionVariableNames = new List<string>();

                var whenHeaders = nodeContext.GetWhenHeaders();

                int totalComplexity = 0;

                for (int i = 0; i < whenHeaders.Count(); i++)
                {
                    var header = whenHeaders.ElementAt(i);

                    nodeTotalConditionCount += header.ComplexityScore;

                    YarnSpinnerParser.ExpressionContext? expression = header.header_expression.expression();

                    // Generate a smart variable for this node
                    var smartVariableName = $"$Yarn.Internal.{nodeContext.NodeGroup}.{nodeContext.NodeTitle}.Condition.{i}";

                    whenConditionVariableNames.Add(smartVariableName);

                    // Create a new smart variable for this header
                    var compiler = new SmartVariableCompiler(this.VariableDeclarations);
                    var result = compiler.Compile(nodeContext.SourceFileName ?? "<generated>", smartVariableName, expression);

                    this.CurrentNode = result.Node;
                    this.CurrentNodeDebugInfo = result.NodeDebugInfo;

                    totalComplexity += header.ComplexityScore;

                    if (header.IsAlways)
                    {
                        // Emit a 'true' for this 'when: always' header
                        this.Emit(header.header_expression.once, new Instruction { PushBool = new PushBoolInstruction { Value = true } });
                    }

                    if (header.IsOnce)
                    {
                        // Emit code that checks that the 'once' variable for
                        // this node has not yet been set.
                        var onceVariable = Compiler.GetContentViewedVariableName(nodeContext.NodeTitle);

                        this.Emit(
                            header.header_when_expression().once,
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

                    if (expression != null && header.IsOnce)
                    {
                        // 'and' the two together
                        this.Emit(
                            header.header_expression.once,
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

                    // The top of the stack now contains a boolean value
                    // indicating whether the condition passed or not.

                    // Add this generated node to the collection of nodes we're
                    // producing.
                    generatedNodes.Add(new NodeCompilationResult
                    {
                        Node = this.CurrentNode,
                        NodeDebugInfo = this.CurrentNodeDebugInfo,
                    });
                }

                // We need to associate this header with the list of smart
                // variables that determine its saliency.
                compiledNode.Headers.Add(new Header
                {
                    Key = Node.ContentSaliencyConditionVariablesHeader,
                    Value = string.Join(Node.ContentSaliencyVariableSeparator.ToString(), whenConditionVariableNames),
                });

                // We also need to add the header that indicates the total
                // complexity of all of its headers.
                compiledNode.Headers.Add(new Header
                {
                    Key = Node.ContentSaliencyConditionComplexityScoreHeader,
                    Value = totalComplexity.ToString(System.Globalization.CultureInfo.InvariantCulture),
                });
            }

            // Now that we've gone through every member of the node group and
            // added smart variables for its conditions, we'll generate the
            // 'hub' node that actually adds the nodes as candidates, selects
            // one (or doesn't, depending on runtime conditions), and runs the
            // appropriate node.

            var hubNode = new Node()
            {
                Name = NodeGroupName,
            };

            var hubNodeDebugInfo = new NodeDebugInfo("<generated>", NodeGroupName);
            this.CurrentNode = hubNode;
            this.CurrentNodeDebugInfo = hubNodeDebugInfo;

            // For each member in the group, add code that registers it as a
            // candidate. We'll also keep track of each instruction that
            // registers a node as a candidate.
            var addCandidateInstructions = new Dictionary<YarnSpinnerParser.NodeContext, Instruction>();

            foreach (var nodeContext in NodeContexts)
            {

                // Register this node as a candidate
                Instruction i;
                this.Emit(i = new Instruction
                {
                    AddSaliencyCandidateFromNode = new AddSaliencyCandidateFromNodeInstruction
                    {
                        NodeName = nodeContext.NodeTitle,
                        Destination = -1,
                    }
                });
                addCandidateInstructions[nodeContext] = i;
            }

            Instruction jumpToNoContentAvailableInstruction;

            this.Emit(null,
                // Ask the VM to select the most salient option from what's
                // available.
                new Instruction { SelectSaliencyCandidate = new SelectSaliencyCandidateInstruction { } },
                // The top of the stack contains either (true, destination), or
                // (false). If there's no content, jump over this next
                // instruction
                jumpToNoContentAvailableInstruction = new Instruction
                {
                    JumpIfFalse = new JumpIfFalseInstruction { Destination = -1 }
                },
                // If we're here, then the top of the stack is true. Pop it, and
                // jump to our node.
                new Instruction { Pop = new PopInstruction { } },
                // Now jump to the destination that's at the top of the stack.
                new Instruction { PeekAndJump = new PeekAndJumpInstruction { } }
            );

            // Update our jump-to-end instruction
            jumpToNoContentAvailableInstruction.Destination = this.CurrentInstructionNumber;
            hubNodeDebugInfo.AddLabel("no_content_available", this.CurrentInstructionNumber);

            // If we're here, then there was no content. Return immediately.
            this.Emit(
                new Instruction { Return = new ReturnInstruction { } }
            );

            // For each member of the group, emit code that detours into the
            // chosen node and return.
            foreach (var nodeContext in NodeContexts)
            {
                // Update the add-candidate instruction that points to here
                addCandidateInstructions[nodeContext].Destination = this.CurrentInstructionNumber;
                hubNodeDebugInfo.AddLabel("run_candidate_" + nodeContext.NodeTitle, this.CurrentInstructionNumber);

                var onceHeader = nodeContext.GetWhenHeaders().FirstOrDefault(h => h.IsOnce);

                if (onceHeader != null)
                {
                    // The node has a 'when: once' header. We're about to detour
                    // into it, so we need to set its 'once' flag to true so
                    // that we don't run it again.

                    // Get the name of the 'once' variable.
                    var onceVariable = Compiler.GetContentViewedVariableName(nodeContext.NodeTitle
                        ?? throw new InvalidOperationException("Internal error: node has no title"));

                    // Emit code that sets the 'once' variable to true.
                    this.Emit(onceHeader.header_expression.once,
                      new Instruction { PushBool = new PushBoolInstruction { Value = true } },
                      new Instruction { StoreVariable = new StoreVariableInstruction { VariableName = onceVariable } },
                      new Instruction { Pop = new PopInstruction { } });
                }

                // Emit code that detours into the node and then returns
                this.Emit(null,
                    new Instruction
                    {
                        DetourToNode = new DetourToNodeInstruction
                        {
                            NodeName = nodeContext.NodeTitle
                        }
                    },
                    new Instruction { Return = new ReturnInstruction { } }
                );
            }

            // We're now at the end of the node.
            generatedNodes.Add(new NodeCompilationResult
            {
                Node = this.CurrentNode,
                NodeDebugInfo = this.CurrentNodeDebugInfo,
            });

            // Return the collection of nodes we've generated.
            return generatedNodes;
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
            foreach (var instruction in instructions)
            {
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
                Node currentNode = this.CurrentNode
                    ?? throw new InvalidOperationException($"Can't get current instruction number: {nameof(this.CurrentNode)} is null");

                return currentNode.Instructions.Count;
            }
        }
    }
}
