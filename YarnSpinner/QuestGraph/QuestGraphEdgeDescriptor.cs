using System;
using System.Text.RegularExpressions;

namespace Yarn
{
    [System.Serializable]
    public struct QuestGraphEdgeDescriptor : IEquatable<QuestGraphEdgeDescriptor>
    {

        public enum VariableType
        {
            Implicit,
            External,
            None,
        }


        public QuestGraphEdgeDescriptor(QuestGraphNodeDescriptor fromNode, QuestGraphNodeDescriptor toNode, string? requirement, string? description)
        {
            this.FromNode = fromNode;
            this.ToNode = toNode;
            this.Requirement = requirement;
            this.Description = description;
        }

        public QuestGraphNodeDescriptor FromNode { get; }
        public QuestGraphNodeDescriptor ToNode { get; }
        public string? Requirement { get; }
        public string? Description { get; }

        // matches "quest A -- B [when C]"
        private static readonly Regex regex = new Regex(@"^quest\s+(.*?)\s*--\s*(.*?)(?:\s+when\s+(.*?))?$");

        public static bool CanParse(string input) => regex.IsMatch(input);

        public static QuestGraphEdgeDescriptor Parse(string input, string? description)
        {
            var match = regex.Match(input);
            if (match.Success == false)
            {
                throw new ArgumentException($"Failed to parse edge {input}");
            }

            string from = match.Groups[1].Value;
            string to = match.Groups[2].Value;
            string? requirement = null;

            if (match.Groups.Count > 2 && string.IsNullOrEmpty(match.Groups[3].Value) == false)
            {
                requirement = match.Groups[3].Value;
            }

            return new QuestGraphEdgeDescriptor(from, to, requirement, description);
        }

        public VariableType VariableCreation
        {
            get
            {
                // If an edge explicitly has a condition, its condition is
                // external
                if (!string.IsNullOrEmpty(this.Requirement))
                {
                    return VariableType.External;
                }

                // By default, we don't add a variable condition on links from a
                // step to a task, so its variable type is None
                if (this.FromNode.Type == QuestGraphNodeDescriptor.NodeType.Step
                    && this.ToNode.Type == QuestGraphNodeDescriptor.NodeType.Task
                    && string.IsNullOrEmpty(this.Requirement))
                {
                    return VariableType.None;
                }

                // Otherwise, it implicitly creates a variable
                return VariableType.Implicit;
            }
        }

        public string? VariableName
        {
            get
            {
                switch (this.VariableCreation)
                {
                    case QuestGraphEdgeDescriptor.VariableType.Implicit:
                        if (string.IsNullOrEmpty(this.Description) == false)
                        {
                            return "$" + this.Description!.Replace(" ", "_");
                        }
                        else
                        {
                            return $"${this.FromNode.Quest}{this.FromNode.Name}_{this.ToNode.Quest}{this.ToNode.Name}";
                        }

                    case QuestGraphEdgeDescriptor.VariableType.External:
                        return this.Requirement ?? throw new System.InvalidOperationException("Variable type is external but variable name is null");

                    case QuestGraphEdgeDescriptor.VariableType.None:
                    default:
                        return null;

                }
            }
        }

        public override int GetHashCode()
        {
            return this.FromNode.GetHashCode() ^ this.ToNode.GetHashCode() ^ (this.Requirement?.GetHashCode() ?? 1);
        }

        public override bool Equals(object obj)
        {
            return obj is QuestGraphEdgeDescriptor descriptor && Equals(descriptor);
        }

        public int GetHashCode(QuestGraphEdgeDescriptor obj)
        {
            int hash = 17;
            hash = hash * 31 + this.FromNode.GetHashCode();
            hash = hash * 31 + this.ToNode.GetHashCode();
            hash = hash * 31 + (this.Requirement ?? string.Empty).GetHashCode();
            return hash;
        }

        public bool Equals(QuestGraphEdgeDescriptor other)
        {
            return this.FromNode == other.FromNode && this.ToNode == other.ToNode && this.Requirement == other.Requirement;
        }

        public static bool operator ==(QuestGraphEdgeDescriptor left, QuestGraphEdgeDescriptor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(QuestGraphEdgeDescriptor left, QuestGraphEdgeDescriptor right)
        {
            return !(left == right);
        }
    }
}
