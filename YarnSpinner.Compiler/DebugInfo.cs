// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;

    public class ProjectDebugInfo {
        public List<NodeDebugInfo> Nodes { get; set; } = new List<NodeDebugInfo>();

        public NodeDebugInfo? GetNodeDebugInfo(string nodeName) {
            foreach (var debugInfo in Nodes) {
                if (debugInfo.NodeName == nodeName) {
                    return debugInfo;
                }
            }
            return null;
        }

        internal static ProjectDebugInfo Combine(params ProjectDebugInfo[] debugInfos)
        {
            var newDebugInfo = new ProjectDebugInfo();
            foreach (var otherDebugInfo in debugInfos) {
                newDebugInfo.Nodes.AddRange(otherDebugInfo.Nodes);
            }

            return newDebugInfo;
            
        }
    }

    /// <summary>
    /// Contains debug information for a node in a Yarn file.
    /// </summary>
    public class NodeDebugInfo
    {
        public NodeDebugInfo(string fileName, string nodeName)
        {
            this.FileName = fileName;
            this.NodeName = nodeName;
        }

        /// <summary>
        /// Gets or sets the file that this DebugInfo was produced from.
        /// </summary>
        internal string FileName { get; private set; }

        /// <summary>
        /// Gets or sets the node that this DebugInfo was produced from.
        /// </summary>
        internal string NodeName { get; set; }

        /// <summary>
        /// Gets or sets the mapping of instruction numbers to <see
        /// cref="Position"/> information in the file indicated by <see
        /// cref="FileName"/>.
        /// </summary>
        internal Dictionary<int, Position> LinePositions { get; set; } = new Dictionary<int, Position>();

        internal IReadOnlyDictionary<int, string> Labels => this.instructionLabels;

        private Dictionary<int, string> instructionLabels = new Dictionary<int, string>();

        private HashSet<int> instructionsThatAreDestinations = new HashSet<int>();

        internal void AddLabel(string label, int instructionIndex) {
            // Ensure that this label is unique
            label = $"L{this.instructionLabels.Count}_" + label;
            
            this.instructionLabels[instructionIndex] = label;
        }
        
        internal string? GetLabel(int instructionIndex) {
            if (this.instructionLabels.TryGetValue(instructionIndex, out string label)) {
                return label;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Gets a <see cref="LineInfo"/> object that describes the specified
        /// instruction at the index <paramref name="instructionNumber"/>.
        /// </summary>
        /// <param name="instructionNumber">The index of the instruction to
        /// retrieve information for.</param>
        /// <returns>A <see cref="LineInfo"/> object that describes the position
        /// of the instruction.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref
        /// name="instructionNumber"/> is less than zero, or greater than the
        /// number of instructions present in the node.</exception>
        public LineInfo GetLineInfo(int instructionNumber)
        {
            if (this.LinePositions.TryGetValue(instructionNumber, out var info))
            {
                return new LineInfo
                {
                    FileName = this.FileName,
                    NodeName = this.NodeName,
                    Position = new Position {
                        Character = info.Character,
                        Line = info.Line,
                    },
                };
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(instructionNumber));
            }
        }

        /// <summary>
        /// Contains positional information about an instruction.
        /// </summary>
        public struct LineInfo
        {
            /// <summary>
            /// The file name of the source that this intruction was produced
            /// from.
            /// </summary>
            public string FileName;

            /// <summary>
            /// The node name of the source that this intruction was produced
            /// from.
            /// </summary>
            public string NodeName;

            /// <summary>
            /// The position in <see cref="FileName"/> that
            /// contains the statement or expression that this line was produced
            /// from.
            /// </summary>
            public Position Position;
        }
    }
}
