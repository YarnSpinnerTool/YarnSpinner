// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// A basic block is a run of instructions inside a Node. Basic blocks group
    /// instructions up into segments such that execution only ever begins at
    /// the start of a block (that is, a program never jumps into the middle of
    /// a block), and execution only ever leaves at the end of a block.
    /// </summary>
    public class BasicBlock
    {
        /// <summary>
        /// Gets the name of the label that this block begins at, or null if this basic block does not begin at a labelled instruction.
        /// </summary>
        public string? LabelName { get; set; }

        /// <summary>
        /// Gets the name of the node that this block is in.
        /// </summary>
        public string NodeName => Node?.Name ?? "(Unknown)";

        /// <summary>
        /// Gets the index of the first instruction of the node that this block is in.
        /// </summary>
        public int FirstInstructionIndex { get; set; }

        /// <summary>
        /// Gets the Node that this block was extracted from.
        /// </summary>
        public Node? Node;

        /// <summary>
        /// Gets a descriptive name for the block.
        /// </summary>
        /// <remarks>
        /// If this block begins at a labelled instruction, the name will be <c>[NodeName].[LabelName]</c>. Otherwise, it will be <c>[NodeName].[FirstInstructionIndex]</c>.
        /// </remarks>
        public string Name
        {
            get
            {
                if (LabelName != null)
                {
                    return $"{NodeName}.{LabelName}";
                }
                else
                {
                    return $"{NodeName}.{FirstInstructionIndex}";
                }
            }
        }

        /// <inheritdoc/>
        public override string ToString() => this.Name;

        /// <summary>
        /// Gets a string containing the textual description of the instructions
        /// in this <see cref="BasicBlock"/>.
        /// </summary>
        /// <param name="library">The <see cref="ILibrary"/> to use when
        /// converting instructions to strings.</param>
        /// <param name="compilationResult">The <see cref="CompilationResult"/>
        /// that produced <see cref="Node"/>.</param>
        /// <returns>A string containing the text version of the
        /// instructions.</returns>
        public string ToString(ILibrary? library, CompilationResult? compilationResult)
        {
            var sb = new StringBuilder();
            foreach (var i in this.Instructions)
            {
                var desc = i.ToDescription(this.Node, library, compilationResult);
                sb.Append($"{desc.Type} {string.Join(",", desc.Operands)}");
                if (desc.Comments.Count() > 0)
                {
                    sb.AppendLine(" ; " + string.Join(", ", desc.Comments));
                }
                else
                {
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Get the ancestors of this block - that is, blocks that may run immediately before this block.
        /// </summary>
        public IEnumerable<BasicBlock> Ancestors => ancestors;

        /// <summary>
        /// Gets the destinations of this block - that is, blocks or nodes that
        /// may run immediately after this block.
        /// </summary>
        /// <seealso cref="Destination"/>
        public IEnumerable<Destination> Destinations => destinations;

        /// <summary>
        /// Gets the Instructions that form this block.
        /// </summary>
        public IEnumerable<Instruction> Instructions { get; set; } = new List<Instruction>();

        /// <summary>
        /// Adds a new destination to this block, that points to another block.
        /// </summary>
        /// <param name="descendant">The new descendant node.</param>
        /// <param name="condition">The condition under which <paramref
        /// name="descendant"/> will be run.</param>
        /// <exception cref="ArgumentNullException">Thrown when descendant is
        /// <see langword="null"/>.</exception>
        public void AddDestination(BasicBlock descendant, Condition condition)
        {
            if (descendant is null)
            {
                throw new ArgumentNullException(nameof(descendant));
            }

            destinations.Add(new BlockDestination(descendant, condition));
            descendant.ancestors.Add(this);
        }

        /// <summary>
        /// Adds a new destination to this block, that points to a node.
        /// </summary>
        /// <param name="nodeName">The name of the destination node.</param>
        /// <param name="condition">The condition under which <paramref
        /// name="nodeName"/> will be jumped to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref
        /// name="nodeName"/> is <see langword="null"/>.</exception>
        public void AddDestination(string nodeName, Condition condition)
        {
            if (string.IsNullOrEmpty(nodeName))
            {
                throw new ArgumentException($"'{nameof(nodeName)}' cannot be null or empty.", nameof(nodeName));
            }

            destinations.Add(new NodeDestination(nodeName, condition));
        }

        /// <summary>
        /// Adds a new destination to this block that represents a detour to a
        /// node.
        /// </summary>
        /// <param name="nodeName">The name of the destination node.</param>
        /// <param name="condition">The condition under which <paramref
        /// name="nodeName"/> will be jumped to.</param>
        /// <param name="returnToBlock">The name of the block that will be
        /// returned to when the detour returns.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref
        /// name="nodeName"/> is <see langword="null"/>.</exception>
        public void AddDetourDestination(string nodeName, Condition condition, BasicBlock returnToBlock)
        {
            if (string.IsNullOrEmpty(nodeName))
            {
                throw new ArgumentException($"'{nameof(nodeName)}' cannot be null or empty.", nameof(nodeName));
            }

            destinations.Add(new NodeDestination(nodeName, condition, returnToBlock));
        }

        /// <summary>
        /// Adds a new destination to this block that points at any other node
        /// in the program.
        /// </summary>
        public void AddDestinationToAnywhere(BasicBlock? returnToBlock = null)
        {
            destinations.Add(new AnyNodeDestination(returnToBlock));
        }

        /// <summary>
        /// Adds a new destination to this block that points to a node, with a
        /// option's line ID for context.
        /// </summary>
        /// <param name="descendant">The new descendant node.</param>
        /// <param name="condition">The condition under which the node <paramref
        /// name="descendant"/> will be run.</param>
        /// <param name="lineID">The line ID of the option that must be selected
        /// in order for <paramref name="descendant"/> to run.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref
        /// name="descendant"/> is <see langword="null"/>.</exception>
        public void AddDestination(BasicBlock descendant, Condition condition, string? lineID)
        {
            if (descendant is null)
            {
                throw new ArgumentNullException(nameof(descendant));
            }

            destinations.Add(new OptionDestination(lineID, descendant));
            descendant.ancestors.Add(this);
        }

        private readonly HashSet<BasicBlock> ancestors = new HashSet<BasicBlock>();

        private readonly HashSet<Destination> destinations = new HashSet<Destination>();

        /// <summary>
        /// A destination represents a <see cref="BasicBlock"/> or node that may
        /// be run, following the execution of a <see cref="BasicBlock"/>.
        /// </summary>
        /// <remarks>
        /// Destination objects represent links between blocks, or between
        /// blocks and nodes.
        /// </remarks>
        public abstract class Destination
        {
            /// <summary>
            /// Initialises a new instance of the <see cref="Destination"/>
            /// class.
            /// </summary>
            /// <param name="condition">The condition under which the
            /// destination will be run.</param>
            /// <param name="returnTo">An optional block that may be returned to
            /// later in the program execution.</param>
            protected Destination(Condition condition, BasicBlock? returnTo = null)
            {
                this.Condition = condition;
                this.ReturnTo = returnTo;
            }

            /// <summary>
            /// The condition that causes this destination to be reached.
            /// </summary>
            public Condition Condition { get; set; }

            /// <summary>
            /// When this destination is taken, if this value is non-null, a VM
            /// should push this block onto the call stack. When a Return
            /// instruction is reached, pop a block off the call stack and
            /// return to it. If the value is null, the VM should clear the call
            /// stack.
            /// </summary>
            public BasicBlock? ReturnTo { get; set; }

            /// <inheritdoc/>
            public override string? ToString()
            {
                string result;
                switch (this.Condition)
                {
                    case Condition.ExpressionIsTrue:
                        result = "true";
                        break;
                    case Condition.ExpressionIsFalse:
                        result = "false";
                        break;
                    case Condition.Option:
                        result = "(option)";
                        break;
                    case Condition.Fallthrough:
                        result = "(fallthrough)";
                        break;
                    case Condition.DirectJump:
                        result = "(jump)";
                        break;
                    default:
                        result = "(unknown)";
                        break;
                }
                if (ReturnTo != null)
                {
                    result += $" (return to: {ReturnTo})";
                }
                return result;
            }


        }

        /// <summary>
        /// A destination that represents a jump or detour to a node.
        /// </summary>
        public class NodeDestination : Destination
        {
            /// <summary>
            /// Initialises a new instance of the <see cref="NodeDestination"/>
            /// class.
            /// </summary>
            /// <param name="condition">The condition under which the
            /// destination will be run.</param>
            /// <param name="returnTo">An optional block that may be returned to
            /// later in the program execution.</param>
            /// <param name="nodeName">The name of the node to jump or detour
            /// to.</param>
            public NodeDestination(string nodeName, Condition condition, BasicBlock? returnTo = null) : base(condition, returnTo)
            {
                this.NodeName = nodeName;
            }

            /// <summary>
            /// The name of the node that this destination refers to.
            /// </summary>
            public string NodeName { get; set; }
        }

        /// <summary>
        /// A destination that represents a jump or detour to any possible node.
        /// </summary>
        public class AnyNodeDestination : Destination
        {
            /// <summary>
            /// Initialises a new instance of the <see cref="AnyNodeDestination"/>
            /// class.
            /// </summary>
            /// <param name="returnTo">An optional block that may be returned to
            /// later in the program execution.</param>
            public AnyNodeDestination(BasicBlock? returnTo = null) : base(Condition.DirectJump, returnTo) { }
        }

        /// <summary>
        /// A destination that represents a jump to a specific block.
        /// </summary>
        public class BlockDestination : Destination
        {
            /// <summary>
            /// The block that this destination refers to.
            /// </summary>
            public BasicBlock Block { get; set; }

            /// <summary>
            /// Gets the index of the first instruction of the block in its
            /// containing node.
            /// </summary>
            public int DestinationInstructionIndex { get => Block.FirstInstructionIndex; }

            /// <summary>
            /// Initialises a new instance of the <see
            /// cref="BlockDestination"/> class.
            /// </summary>
            /// <param name="block">The block that is jumped to.</param>
            /// <param name="condition">The condition under which the
            /// destination is taken.</param>
            public BlockDestination(BasicBlock block, Condition condition) : base(condition)
            {
                this.Block = block;
            }
        }

        /// <summary>
        /// A destination that represents an option being selected.
        /// </summary>
        public class OptionDestination : BlockDestination
        {
            /// <summary>
            /// Initialises a new instance of the <see
            /// cref="OptionDestination"/> class.
            /// </summary>
            /// <param name="optionLineID">The ID of the line associated with
            /// this option.</param>
            /// <param name="block">The block that is jumped to when this option
            /// is selected.</param>
            public OptionDestination(string? optionLineID, BasicBlock block) : base(block, Condition.Option)
            {
                this.OptionLineID = optionLineID;
            }

            /// <summary>
            /// Gets or sets the ID of the line associated with this option.
            /// </summary>
            public string? OptionLineID { get; set; }

            /// <inheritdoc/>
            public override string ToString()
            {
                return this.OptionLineID ?? "(option)";
            }
        }

        /// <summary>
        /// Gets all descendants (that is, destinations, and destinations of
        /// those destinations, and so on), recursively.
        /// </summary>
        /// <remarks>
        /// Cycles are detected and avoided.
        /// </remarks>
        public IEnumerable<BasicBlock> Descendants
        {
            get
            {
                // Start with a queue of immediate children that link to blocks
                Queue<BasicBlock> candidates = new Queue<BasicBlock>(this.Destinations.OfType<BlockDestination>().Select(d => d.Block));

                List<BasicBlock> descendants = new List<BasicBlock>();

                while (candidates.Count > 0)
                {
                    var next = candidates.Dequeue();
                    if (descendants.Contains(next))
                    {
                        // We've already seen this one - skip it.
                        continue;
                    }
                    descendants.Add(next);
                    foreach (var destination in next.Destinations.OfType<BlockDestination>().Select(d => d.Block))
                    {
                        candidates.Enqueue(destination);
                    }
                }

                return descendants;

            }
        }

        /// <summary>
        /// Gets all descendants (that is, destinations, and destinations of
        /// those destinations, and so on) that have any player-visible content,
        /// recursively.
        /// </summary>
        /// <remarks>
        /// Cycles are detected and avoided.
        /// </remarks>
        public IEnumerable<BasicBlock> DescendantsWithPlayerVisibleContent
        {
            get
            {
                return Descendants.Where(d => d.PlayerVisibleContent.Any());
            }
        }

        /// <summary>
        /// The conditions under which a <see cref="Destination"/> may be
        /// reached at the end of a BasicBlock.
        /// </summary>
        public enum Condition
        {
            /// <summary>
            /// The Destination is reached because the preceding BasicBlock
            /// reached the end of its execution, and the Destination's target
            /// is the block immediately following.
            /// </summary>
            Fallthrough,

            /// <summary>
            /// The Destination is reached beacuse of an explicit instruction to
            /// go to this block or node.
            /// </summary>
            DirectJump,

            /// <summary>
            /// The Destination is reached because an expression evaluated to
            /// true.
            /// </summary>
            ExpressionIsTrue,

            /// <summary>
            /// The Destination is reached because an expression evaluated to
            /// false.
            /// </summary>
            ExpressionIsFalse,

            /// <summary>
            /// The Destination is reached because the player made an in-game
            /// choice to go to it.
            /// </summary>
            Option,
        }

        /// <summary>
        /// An abstract class that represents some content that is shown to the
        /// player.
        /// </summary>
        /// <remarks>
        /// This class is used, rather than the runtime classes Yarn.Line or
        /// Yarn.OptionSet, because when the program is being analysed, no
        /// values for any substitutions are available. Instead, these classes
        /// represent the data that is available offline.
        /// </remarks>
        public abstract class PlayerVisibleContentElement
        {
        }

        /// <summary>
        /// A line of dialogue that should be shown to the player.
        /// </summary>
        public class LineElement : PlayerVisibleContentElement
        {
            /// <summary>
            /// The string table ID of the line that will be shown to the player.
            /// </summary>
            public string LineID;

            /// <summary>
            /// Initialises a new instance of the <see cref="LineElement"/>
            /// class.
            /// </summary>
            /// <param name="lineID">The ID of the line that this element
            /// represents.</param>
            public LineElement(string lineID) => this.LineID = lineID;
        }

        /// <summary>
        /// A collection of options that should be shown to the player.
        /// </summary>
        public class OptionsElement : PlayerVisibleContentElement
        {
            /// <summary>
            /// Represents a single option that may be presented to the player.
            /// </summary>
            public struct Option
            {
                /// <summary>
                /// The string table ID that will be shown to the player.
                /// </summary>
                public string LineID;

                /// <summary>
                /// The destination that will be run if this option is selected
                /// by the player.
                /// </summary>
                public int Destination;
            }

            /// <summary>
            /// The collection of options that will be delivered to the player.
            /// </summary>
            public IEnumerable<Option> Options;

            /// <summary>
            /// Initialises a new instance of the <see cref="OptionsElement"/>
            /// class.
            /// </summary>
            /// <param name="options">The options that will be delivered to the
            /// player.</param>
            public OptionsElement(IEnumerable<Option> options)
            {
                this.Options = options;
            }
        }

        /// <summary>
        /// A command that will be executed.
        /// </summary>
        public class CommandElement : PlayerVisibleContentElement
        {
            /// <summary>
            /// The text of the command.
            /// </summary>
            public string CommandText;

            /// <summary>
            /// Initialises a new instance of the <see cref="CommandElement"/>
            /// class.
            /// </summary>
            /// <param name="commandText">The text of the command.</param>
            public CommandElement(string commandText)
            {
                this.CommandText = commandText;
            }
        }

        /// <summary>
        /// Gets the collection of player-visible content that will be delivered
        /// when this block is run.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Player-visible content means lines, options and commands. When this
        /// block is run, the entire contents of this collection will be
        /// displayed to the player, in the same order as they appear in this
        /// collection.
        /// </para>
        /// <para>
        /// If this collection is empty, then the block contains no visible
        /// content. This is the case for blocks that only contain logic, and do
        /// not contain any lines, options or commands.
        /// </para>
        /// <example>
        /// To tell the difference between the different kinds of content, use
        /// the <see langword="is"/> operator to check the type of each item:
        /// <code>
        /// foreach (var item in block.PlayerVisibleContent) { 
        ///     if (item is LineElement line) {
        ///          // Do something with line 
        ///     } 
        /// }
        /// </code>
        /// </example>
        /// </remarks>
        public IEnumerable<PlayerVisibleContentElement> PlayerVisibleContent
        {
            get
            {
                var accumulatedOptions = new List<(string LineID, int Destination)>();
                foreach (var instruction in Instructions)
                {
                    switch (instruction.InstructionTypeCase)
                    {
                        case Instruction.InstructionTypeOneofCase.RunLine:
                            yield return new LineElement(instruction.RunLine.LineID);
                            break;

                        case Instruction.InstructionTypeOneofCase.RunCommand:
                            yield return new CommandElement(instruction.RunCommand.CommandText);
                            break;

                        case Instruction.InstructionTypeOneofCase.AddOption:
                            accumulatedOptions.Add(
                                (
                                    instruction.AddOption.LineID,
                                    instruction.AddOption.Destination
                                )
                            );
                            break;

                        case Instruction.InstructionTypeOneofCase.ShowOptions:
                            yield return new OptionsElement(
                                accumulatedOptions.Select(o => new OptionsElement.Option
                                {
                                    Destination = o.Destination,
                                    LineID = o.LineID,
                                }));

                            accumulatedOptions.Clear();
                            break;
                    }
                }
            }
        }
    }
}
