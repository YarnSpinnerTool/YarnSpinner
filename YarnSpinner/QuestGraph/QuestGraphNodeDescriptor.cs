using System;
using System.Text.RegularExpressions;

namespace Yarn
{
    /// <summary>
    /// Describes a node on a graph.
    /// </summary>
    public struct QuestGraphNodeDescriptor : IEquatable<QuestGraphNodeDescriptor>
    {
        /// <summary>
        /// A 2D position.
        /// </summary>
        public struct NodePosition : IEquatable<NodePosition>
        {
            /// <summary>
            /// Initialises a new instance of the <see cref="NodePosition"/>
            /// struct with an X and Y coordinate.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            public NodePosition(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            /// <summary>
            /// Gets or sets the position's X coordinate.
            /// </summary>
            public int X { get; set; }

            /// <summary>
            /// Gets or sets the position's Y coordinate.
            /// </summary>
            public int Y { get; set; }

            /// <inheritdoc/>
            public override readonly bool Equals(object obj)
            {
                return obj is NodePosition otherPosition && this.Equals(otherPosition);
            }

            /// <inheritdoc/>
            public readonly bool Equals(NodePosition other)
            {
                return this.X == other.X && this.Y == other.Y;
            }

            /// <inheritdoc/>
            public override readonly int GetHashCode()
            {
                var hash = 17;
                hash = hash * 31 + this.X.GetHashCode();
                hash = hash * 31 + this.Y.GetHashCode();
                return hash;
            }

            /// <inheritdoc/>
            public static bool operator ==(NodePosition left, NodePosition right)
            {
                return left.Equals(right);
            }

            /// <inheritdoc/>
            public static bool operator !=(NodePosition left, NodePosition right)
            {
                return !(left == right);
            }
        }

        /// <summary>
        /// The type of a quest node.
        /// </summary>
        public enum NodeType
        {
            /// <summary>
            /// The node is a step in the quest graph.
            /// </summary>
            Step,
            /// <summary>
            /// The node is a task in the quest graph.
            /// </summary>
            Task
        }

        /// <summary>
        /// The name of the node.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The type of the node.
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// The name of the quest that contains the node.
        /// </summary>
        public string Quest { get; set; }

        /// <summary>
        /// The position of the node in the graph.
        /// </summary>
        public NodePosition Position { get; set; }

        /// <summary>
        /// The fully-qualified name of the node.
        /// </summary>
        public readonly string FullName => $"Quest_{Quest}_{Name}";

        /// <summary>
        /// Initialises a new instance of the <see cref="QuestGraphNodeDescriptor"/> struct with a name, type and quest.
        /// </summary>
        /// <param name="name"><inheritdoc cref="Name" path="/summary"/></param>
        /// <param name="type"><inheritdoc cref="Type" path="/summary"/></param>
        /// <param name="quest"><inheritdoc cref="Quest" path="/summary"/></param>
        public QuestGraphNodeDescriptor(string name, NodeType type, string quest)
        {
            this.Name = name;
            this.Quest = quest;
            this.Type = type;
            this.Position = default;
        }

        static System.Text.RegularExpressions.Regex ParseRegex = new Regex(@"^(.*?)(?::(.*?)){1,2}$");

        /// <summary>
        /// Gets a value indicating whether <paramref name="input"/> can be
        /// parsed into a <see cref="QuestGraphNodeDescriptor"/>.
        /// </summary>
        /// <param name="input">The text to test.</param>
        /// <returns><see langword="true"/> if <paramref name="input"/> can be
        /// parsed as a <see cref="QuestGraphNodeDescriptor"/>; <see
        /// langword="false"/> otherwise.</returns>
        public static bool CanParse(string input)
        {
            return ParseRegex.IsMatch(input);
        }

        /// <summary>
        /// Creates a new <see cref="QuestGraphNodeDescriptor"/> from an input
        /// string.
        /// </summary>
        /// <param name="input">The text to parse.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref
        /// name="input"/> cannot be parsed as a quest graph node
        /// descriptor.</exception>
        public QuestGraphNodeDescriptor(string input)
        {
            if (!CanParse(input))
            {
                throw new ArgumentException("Invalid node descriptor " + input);
            }

            this.Position = default;

            var match = ParseRegex.Match(input);

            this.Quest = match.Groups[1].Value;

            if (match.Groups[2].Captures.Count == 1)
            {
                this.Name = match.Groups[2].Captures[0].Value;
                this.Type = NodeType.Step;
            }
            else
            {
                var type = match.Groups[2].Captures[0].Value.ToUpperInvariant();
                switch (type)
                {
                    case "STEP":
                        this.Type = NodeType.Step;
                        break;
                    case "TASK":
                        this.Type = NodeType.Task;
                        break;
                    default:
                        throw new System.ArgumentException($"Unknown node type {type}");
                }

                this.Name = match.Groups[2].Captures[1].Value;
            }
        }

        /// <summary>
        /// Gets the name of the smart variable that can be used to fetch
        /// current value of the specified property of a quest node.
        /// </summary>
        /// <param name="property">The property to get a smart variable name for.</param>
        /// <returns>The name of the smart variable.</returns>
        public string GetSmartVariableForState(QuestNodeStateProperty property)
        {
            return $"$Quest_{Quest}_{Name}_{property}";
        }


        /// <inheritdoc/>
        public readonly bool Equals(QuestGraphNodeDescriptor other)
        {
            return this.Name == other.Name && this.Quest == other.Quest && this.Type == other.Type;
        }

        /// <summary>
        /// Implicitly converts a string to a descriptor.
        /// </summary>
        /// <param name="input">The string to convert.</param>
        public static implicit operator QuestGraphNodeDescriptor(string input) => new QuestGraphNodeDescriptor(input);

        /// <summary>
        /// Implicitly converts the descriptor to a string.
        /// </summary>
        /// <param name="input">The descriptor to convert.</param>
        public static implicit operator string(QuestGraphNodeDescriptor input) => input.ToString();

        /// <inheritdoc/>
        public override readonly string ToString() => $"{this.Quest}:{this.Type}:{this.Name}";

        /// <inheritdoc/>
        public override readonly int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }

        /// <inheritdoc/>
        public override readonly bool Equals(object obj)
        {
            return obj is QuestGraphNodeDescriptor otherDescriptor && Equals(otherDescriptor);
        }

        /// <inheritdoc/>
        public static bool operator ==(QuestGraphNodeDescriptor left, QuestGraphNodeDescriptor right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(QuestGraphNodeDescriptor left, QuestGraphNodeDescriptor right)
        {
            return !(left == right);
        }
    }
}
