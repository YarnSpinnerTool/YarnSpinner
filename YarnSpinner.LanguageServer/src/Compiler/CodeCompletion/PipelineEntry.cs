using System;
using System.Collections.Generic;
using System.Diagnostics;
using Antlr4.Runtime.Atn;

namespace Antlr4CodeCompletion.Core.CodeCompletion
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// Port of antlr-c3 javascript library to c#
    /// The c3 engine is able to provide code completion candidates useful for
    /// editors with ANTLR generated parsers, independent of the actual
    /// language/grammar used for the generation.
    /// https://github.com/mike-lischke/antlr4-c3
    /// </remarks>
    [DebuggerDisplay("PipelineEntry: state = {State} , tokenIndex = {TokenIndex}")]
    internal class PipelineEntry : IEquatable<PipelineEntry>
    {
        internal ATNState State { get; }
        internal int TokenIndex { get; }

        internal PipelineEntry(ATNState state, int tokenIndex)
        {
            this.State = state;
            this.TokenIndex = tokenIndex;
        }

        public override bool Equals(object obj) => this.Equals(obj as PipelineEntry);

        public bool Equals(PipelineEntry other) => other != null &&
                   EqualityComparer<ATNState>.Default.Equals(this.State, other.State) &&
                   this.TokenIndex == other.TokenIndex;

        public override int GetHashCode()
        {
            var hashCode = -944266740;
            hashCode = (hashCode * -1521134295) + EqualityComparer<ATNState>.Default.GetHashCode(this.State);
            hashCode = (hashCode * -1521134295) + this.TokenIndex.GetHashCode();
            return hashCode;
        }
    }
}