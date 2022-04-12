#define DISALLOW_NULL_EQUATION_TERMS


using System;

namespace TypeChecker
{

    /// <summary>
    /// An abstract parent class for all types and type variables.
    /// </summary>
    public interface ITypeTerm : IEquatable<ITypeTerm>
    {
        bool Equals(ITypeTerm other);

        /// <summary>
        /// Applies a substitution to this term.
        /// </summary>
        /// <param name="s">The substitution to apply.</param>
        /// <returns>The substituted value.</returns>
        ITypeTerm Substitute(Substitution s);
    }

}
