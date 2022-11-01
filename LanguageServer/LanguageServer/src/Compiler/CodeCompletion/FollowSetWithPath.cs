using System;
using System.Collections.Generic;
using System.Diagnostics;
using Antlr4.Runtime.Misc;

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
    [DebuggerDisplay("FollowSetWithPath: following = {Following} , intervals = {Intervals}, path = {Path}")]
    internal class FollowSetWithPath : IEquatable<FollowSetWithPath>
    {
        internal IList<int> Following { get; set; }
        internal IntervalSet Intervals { get; set; }
        internal IList<int> Path { get; set; }

        public override bool Equals(object obj) => this.Equals(obj as FollowSetWithPath);

        public bool Equals(FollowSetWithPath other) => other != null &&
                   EqualityComparer<IList<int>>.Default.Equals(this.Following, other.Following) &&
                   EqualityComparer<IntervalSet>.Default.Equals(this.Intervals, other.Intervals) &&
                   EqualityComparer<IList<int>>.Default.Equals(this.Path, other.Path);

        public override int GetHashCode()
        {
            var hashCode = 904860839;
            hashCode = (hashCode * -1521134295) + EqualityComparer<IList<int>>.Default.GetHashCode(this.Following);
            hashCode = (hashCode * -1521134295) + EqualityComparer<IntervalSet>.Default.GetHashCode(this.Intervals);
            hashCode = (hashCode * -1521134295) + EqualityComparer<IList<int>>.Default.GetHashCode(this.Path);
            return hashCode;
        }
    }
}