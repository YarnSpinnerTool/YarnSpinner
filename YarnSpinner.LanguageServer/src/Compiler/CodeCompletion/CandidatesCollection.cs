using System;
using System.Collections.Generic;
using System.Diagnostics;

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
    [DebuggerDisplay("CandidatesCollection: tokens = {Tokens} , rules = {Rules} , ruleStrings = {RulePositions}")]
    public class CandidatesCollection : IEquatable<CandidatesCollection>
    {
        /// <summary>
        /// Collection of Rule candidates, each with the callstack of rules to
        /// reach the candidate
        /// </summary>
        public IDictionary<int, IList<int>> Rules { get; set; } = new Dictionary<int, IList<int>>();

        /// <summary>
        /// Collection of Token ID candidates, each with a follow-on List of
        /// subsequent tokens
        /// </summary>
        public IDictionary<int, IList<int>> Tokens { get; set; } = new Dictionary<int, IList<int>>();

        /// <summary>
        /// Collection of matched Preferred Rules each with their start and end
        /// offsets
        /// </summary>
        public IDictionary<int, IList<int>> RulePositions { get; set; } = new Dictionary<int, IList<int>>();

        public override bool Equals(object obj) => this.Equals(obj as CandidatesCollection);

        public bool Equals(CandidatesCollection other) => other != null &&
                   EqualityComparer<IDictionary<int, IList<int>>>.Default.Equals(this.Rules, other.Rules) &&
                   EqualityComparer<IDictionary<int, IList<int>>>.Default.Equals(this.Tokens, other.Tokens) &&
                   EqualityComparer<IDictionary<int, IList<int>>>.Default.Equals(this.RulePositions, other.RulePositions);

        public override int GetHashCode()
        {
            var hashCode = -1987583628;
            hashCode = (hashCode * -1521134295) + EqualityComparer<IDictionary<int, IList<int>>>.Default.GetHashCode(this.Rules);
            hashCode = (hashCode * -1521134295) + EqualityComparer<IDictionary<int, IList<int>>>.Default.GetHashCode(this.Tokens);
            hashCode = (hashCode * -1521134295) + EqualityComparer<IDictionary<int, IList<int>>>.Default.GetHashCode(this.RulePositions);
            return hashCode;
        }
    }
}