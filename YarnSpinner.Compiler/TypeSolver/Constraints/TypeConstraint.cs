#define DISALLOW_NULL_EQUATION_TERMS


namespace TypeChecker
{

    /// <summary>
    /// Stores information that a Solver can use to solve a system of type
    /// equations.
    /// </summary>
    internal abstract class TypeConstraint
    {
        /// <inheritdoc/>
        new public abstract string ToString();

        /// <summary>
        /// Simplifies this constraint, producing either a <see
        /// cref="TypeEqualityConstraint"/> or a <see
        /// cref="DisjunctionConstraint"/>, or <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// If this method returns <see langword="null"/>, it represents that
        /// the constraint has determined that it is a tautology (e.g. 'T0 ==
        /// T0'), and does not need further evaluation.
        /// </remarks>
        /// <param name="subst">A <see cref="Substitution"/> that the constraint
        /// can use to help decide how to simplify.</param>
        /// <param name="knownTypes">The collection of all currently known types.</param>
        /// <returns>A <see cref="TypeEqualityConstraint"/> or a <see
        /// cref="DisjunctionConstraint"/> that represents a simplified version
        /// of this constraint, or null.</returns>
        public abstract TypeConstraint Simplify(Substitution subst, System.Collections.Generic.IEnumerable<Yarn.TypeBase> knownTypes);

        /// <summary>
        /// Gets or sets the range of text that contained the expression that
        /// produced this constraint.
        /// </summary>
        public Yarn.Compiler.Range SourceRange { get; set; }

        /// <summary>
        /// Gets or sets the name of the file that contained the expression that
        /// produced this constraint.
        /// </summary>
        public string SourceFileName { get; set; }
    }
}
