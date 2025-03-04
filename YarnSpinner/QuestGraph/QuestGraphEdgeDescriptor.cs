using System;
using System.Text.RegularExpressions;

namespace Yarn
{

    public struct QuestGraphEdgeDescriptor : IEquatable<QuestGraphEdgeDescriptor>
    {
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



        // matches "A --> B [when C]"
        private static readonly Regex regex = new Regex(@"^(.*?)\s*--\s*(.*?)(?:\s+when\s+(.*?))?$");

        public static bool CanParse(string input) => regex.IsMatch(input);

        public static QuestGraphEdgeDescriptor Parse(string input, string? description)
        {

            var match = regex.Match(input);

            string from = match.Groups[1].Value;
            string to = match.Groups[2].Value;
            string? requirement = null;

            if (match.Groups.Count > 2 && string.IsNullOrEmpty(match.Groups[3].Value) == false)
            {
                requirement = match.Groups[3].Value;
            }

            return new QuestGraphEdgeDescriptor(from, to, requirement, description);
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
    }
}
