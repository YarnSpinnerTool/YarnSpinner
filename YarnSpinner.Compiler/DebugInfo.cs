// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Contains debug information for a node in a Yarn file.
    /// </summary>
    public class DebugInfo
    {
        /// <summary>
        /// Gets or sets the file that this DebugInfo was produced from.
        /// </summary>
        internal string FileName { get; set; }

        /// <summary>
        /// Gets or sets the node that this DebugInfo was produced from.
        /// </summary>
        internal string NodeName { get; set; }

        /// <summary>
        /// Gets or sets the mapping of instruction numbers to line and
        /// character information in the file indicated by <see
        /// cref="FileName"/>.
        /// </summary>
        internal Dictionary<int, (int Line, int Character)> LineInfos { get; set; } = new Dictionary<int, (int Line, int Character)>();

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
            if (this.LineInfos.TryGetValue(instructionNumber, out var info))
            {
                return new LineInfo
                {
                    FileName = this.FileName,
                    NodeName = this.NodeName,
                    LineNumber = info.Line,
                    CharacterNumber = info.Character,
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
            /// The zero-indexed line number in <see cref="FileName"/> that
            /// contains the statement or expression that this line was produced
            /// from.
            /// </summary>
            public int LineNumber;

            /// <summary>
            /// The zero-indexed character number in <see cref="FileName"/> that
            /// contains the statement or expression that this line was produced
            /// from.
            /// </summary>
            public int CharacterNumber;
        }
    }
}
