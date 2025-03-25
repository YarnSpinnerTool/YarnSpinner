using System;
using System.Text.RegularExpressions;

namespace Yarn
{
    public struct QuestGraphNodeDescriptor : IEquatable<QuestGraphNodeDescriptor>
    {
        public struct NodePosition
        {
            public NodePosition(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }

            public int X { get; set; }
            public int Y { get; set; }

        }
        public enum NodeType
        {
            Step, Task
        }

        public string Name { get; set; }

        public NodeType Type { get; set; }

        public string Quest { get; set; }

        public NodePosition Position { get; set; }

        public readonly string FullName => $"Quest_{Quest}_{Name}";

        public QuestGraphNodeDescriptor(string name, NodeType type, string quest)
        {
            this.Name = name;
            this.Quest = quest;
            this.Type = type;
            this.Position = default;
        }

        static System.Text.RegularExpressions.Regex ParseRegex = new Regex(@"^(.*?)(?::(.*?)){1,2}$");

        public static bool CanParse(string input)
        {
            return ParseRegex.IsMatch(input);
        }

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

        public bool Equals(QuestGraphNodeDescriptor other)
        {
            return this.Name == other.Name && this.Quest == other.Quest && this.Type == other.Type;
        }

        public static implicit operator QuestGraphNodeDescriptor(string input) => new QuestGraphNodeDescriptor(input);

        public static implicit operator string(QuestGraphNodeDescriptor input) => input.ToString();

        public override string ToString() => $"{this.Quest}:{this.Type}:{this.Name}";


        public override int GetHashCode()
        {
            return this.ToString().GetHashCode();
        }
    }
}
