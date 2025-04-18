// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Contains extension methods for producing <see cref="BasicBlock"/>
    /// objects from a Node.
    /// </summary>
    public static class InstructionCollectionExtensions
    {
        /// <summary>
        /// Produces <see cref="BasicBlock"/> objects from a Node.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public static IEnumerable<BasicBlock> GetBasicBlocks(this Node node, NodeDebugInfo info)
        {
            // If we don't have any instructions, return an empty collection
            if (node == null || node.Instructions == null || node.Instructions.Count == 0)
            {
                return Enumerable.Empty<BasicBlock>();
            }

            var result = new List<BasicBlock>();

            var leaderIndices = new HashSet<int>
            {
                // The first instruction is a leader.
                0,
            };

            for (int i = 0; i < node.Instructions.Count; i++)
            {
                switch (node.Instructions[i].InstructionTypeCase)
                {
                    case Instruction.InstructionTypeOneofCase.JumpTo:
                    case Instruction.InstructionTypeOneofCase.PeekAndJump:
                    case Instruction.InstructionTypeOneofCase.RunNode:
                    case Instruction.InstructionTypeOneofCase.PeekAndRunNode:
                    case Instruction.InstructionTypeOneofCase.DetourToNode:
                    case Instruction.InstructionTypeOneofCase.PeekAndDetourToNode:
                    case Instruction.InstructionTypeOneofCase.JumpIfFalse:
                    case Instruction.InstructionTypeOneofCase.Stop:
                    case Instruction.InstructionTypeOneofCase.Return:
                        // Every instruction after a jump (conditional or
                        // nonconditional), or a return, is a leader.
                        leaderIndices.Add(i + 1);
                        break;
                    default:
                        // nothing to do
                        break;
                }

                // If the instruction is labelled (i.e. it is the target of a
                // jump), it is a leader.
                if (info.GetLabel(i) != null)
                {
                    leaderIndices.Add(i);
                }
            }

            // Now that we know what the leaders are, run through the
            // instructions; every time we encounter a leader, start a new basic
            // block.
            var currentBlockInstructions = new List<Instruction>();

            int lastLeader = 0;

            for (int i = 0; i < node.Instructions.Count; i++)
            {
                // The current instruction is a leader! If we have accumulated
                // instructions, create a new block from them, store it, and
                // start a new list of instructions.
                if (leaderIndices.Contains(i))
                {
                    if (currentBlockInstructions.Count > 0)
                    {
                        var block = new BasicBlock
                        {
                            Node = node,
                            Instructions = new List<Instruction>(currentBlockInstructions),
                            FirstInstructionIndex = lastLeader,
                            LabelName = info.GetLabel(lastLeader) ?? null,
                        };
                        result.Add(block);
                    }

                    lastLeader = i;
                    currentBlockInstructions.Clear();
                }

                // Add the current instruction to our current accumulation.
                currentBlockInstructions.Add(node.Instructions[i]);
            }

            // We've reached the end of the instruction list. If we have any
            // accumulated instructions, create a final block here.
            if (currentBlockInstructions.Count > 0)
            {
                var block = new BasicBlock
                {
                    Node = node,
                    Instructions = new List<Instruction>(currentBlockInstructions),
                    FirstInstructionIndex = lastLeader,
                    LabelName = info.GetLabel(lastLeader) ?? null,
                };
                result.Add(block);
            }

            BasicBlock GetBlock(int startInstructionIndex)
            {

                try
                {
                    return result.First(block => block.FirstInstructionIndex == startInstructionIndex);
                }
                catch (System.InvalidOperationException)
                {
                    // nothing found
                    throw new System.InvalidOperationException($"No block in {node.Name} starts at index {startInstructionIndex}");
                }
            }

            var peekAndJumpDestinations = new List<(int DestinationInstruction, string? ContentID)>();

            // Final pass: now that we have all the blocks, go through each of
            // them and build the links between them
            foreach (var block in result)
            {
                string? currentStringAtTopOfStack = null;
                int count = 0;
                foreach (var instruction in block.Instructions)
                {
                    switch (instruction.InstructionTypeCase)
                    {
                        case Instruction.InstructionTypeOneofCase.AddOption:
                            {
                                // Track the destination that this instruction says
                                // it'll jump to. 
                                var destinationIndex = instruction.AddOption.Destination;
                                peekAndJumpDestinations.Add((destinationIndex, instruction.AddOption.LineID));
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.PeekAndJump:
                            {
                                // We're jumping to a labeled section of the same node.

                                // PeekAndJump is really only used inside option
                                // selection handlers, so we can confidently
                                // assume that a PeekAndJump is an option.
                                foreach (var destination in peekAndJumpDestinations)
                                {
                                    var (destinationIndex, destinationLineID) = destination;
                                    var destinationBlock = GetBlock(destinationIndex);

                                    block.AddDestination(destinationBlock, BasicBlock.Condition.Option, destinationLineID);
                                }
                                peekAndJumpDestinations.Clear();

                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.JumpTo:
                            {
                                var destinationIndex = GetBlock(instruction.JumpTo.Destination);
                                block.AddDestination(destinationIndex, BasicBlock.Condition.DirectJump);
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.PushString:
                            {
                                // The top of the stack is now a string. (This
                                // isn't perfect, because it doesn't handle
                                // stuff like functions, which modify the stack,
                                // but the most common case is <<jump
                                // NodeName>>, which is a combination of 'push
                                // string' followed by 'run node at top of
                                // stack')
                                currentStringAtTopOfStack = instruction.PushString.Value;
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.SelectSaliencyCandidate:
                        case Instruction.InstructionTypeOneofCase.CallFunc:
                        case Instruction.InstructionTypeOneofCase.Pop:
                        case Instruction.InstructionTypeOneofCase.PushBool:
                        case Instruction.InstructionTypeOneofCase.PushFloat:
                        case Instruction.InstructionTypeOneofCase.PushVariable:
                            {
                                // All of these instructions modify the stack,
                                // which means that the top of the stack is now
                                // no longer a string. Again, not a fully
                                // accurate representation of what's going on,
                                // but for the moment, we're not supporting
                                // 'jump to expression' here.
                                currentStringAtTopOfStack = null;
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.RunNode:
                            block.AddDestination(instruction.RunNode.NodeName, BasicBlock.Condition.DirectJump);
                            break;
                        case Instruction.InstructionTypeOneofCase.PeekAndRunNode:
                            {
                                if (currentStringAtTopOfStack != null)
                                {
                                    block.AddDestination(currentStringAtTopOfStack, BasicBlock.Condition.DirectJump);
                                }
                                else
                                {
                                    // We don't know the string at the top of
                                    // the stack. This could be a jump to
                                    // anywhere.
                                    block.AddDestinationToAnywhere();
                                }
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.DetourToNode:
                            {
                                // We will return to the block that starts on
                                // the next instruction, so find that block now.
                                var returnToBlock = GetBlock(block.FirstInstructionIndex + count + 1);

                                block.AddDetourDestination(
                                    instruction.DetourToNode.NodeName,
                                    BasicBlock.Condition.DirectJump,
                                    returnToBlock);
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.PeekAndDetourToNode:
                            {
                                // We will return to the block that starts on
                                // the next instruction, so find that block now.
                                var returnToBlock = GetBlock(block.FirstInstructionIndex + count + 1);

                                if (currentStringAtTopOfStack != null)
                                {
                                    block.AddDetourDestination(currentStringAtTopOfStack,
                                        BasicBlock.Condition.DirectJump,
                                        returnToBlock);
                                }
                                else
                                {
                                    // We don't know the string at the top of
                                    // the stack. This could be a detour to
                                    // anywhere.
                                    block.AddDestinationToAnywhere(returnToBlock);
                                }
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.JumpIfFalse:
                            {
                                var destinationIndex = instruction.JumpIfFalse.Destination;

                                // Jump-if-false falls through to the next
                                // instruction if the top of the stack is true,
                                // so the true block is whatever block is
                                // started by the next instruction.
                                var destinationTrueBlock = GetBlock(block.FirstInstructionIndex + count + 1);

                                // The false block is whichever block is started
                                // by the instruction's destination.
                                var destinationFalseBlock = GetBlock(destinationIndex);

                                block.AddDestination(destinationFalseBlock, BasicBlock.Condition.ExpressionIsFalse);
                                block.AddDestination(destinationTrueBlock, BasicBlock.Condition.ExpressionIsTrue);
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.AddSaliencyCandidate:
                            {
                                var destinationIndex = instruction.AddSaliencyCandidate.Destination;
                                peekAndJumpDestinations.Add((
                                    DestinationInstruction: destinationIndex,
                                    ContentID: instruction.AddSaliencyCandidate.ContentID
                                ));
                                break;
                            }
                        case Instruction.InstructionTypeOneofCase.AddSaliencyCandidateFromNode:
                            {
                                var destinationIndex = instruction.AddSaliencyCandidateFromNode.Destination;
                                peekAndJumpDestinations.Add((
                                    DestinationInstruction: destinationIndex,
                                    ContentID: null
                                ));
                                break;
                            }
                    }
                    count += 1;
                }

                if (block.Destinations.Count() == 0)
                {
                    // We've reached the end of this block, and don't have any
                    // destinations. If our last destination isn't 'stop' or 'return', then
                    // we'll fall through to the next node.

                    var lastInstructionType = block.Instructions.Last().InstructionTypeCase;
                    if (lastInstructionType != Instruction.InstructionTypeOneofCase.Stop &&
                        lastInstructionType != Instruction.InstructionTypeOneofCase.Return)
                    {
                        var nextBlockStartInstruction = block.FirstInstructionIndex + block.Instructions.Count();

                        if (nextBlockStartInstruction >= node.Instructions.Count)
                        {
                            // We've reached the very end of the node's
                            // instructions. There are no blocks to jump to.
                        }
                        else
                        {

                            var destination = GetBlock(nextBlockStartInstruction);
                            block.AddDestination(destination, BasicBlock.Condition.Fallthrough);
                        }
                    }
                }
            }

            return result;
        }
    }
}
