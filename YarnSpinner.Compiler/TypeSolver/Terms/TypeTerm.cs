#define DISALLOW_NULL_EQUATION_TERMS


using System;

namespace TypeChecker
{

    /// <summary>
    /// An abstract parent class for all types and type variables.
    /// </summary>
    public abstract class TypeTerm : IEquatable<TypeTerm>
    {
        public abstract bool Equals(TypeTerm other);

        /// <summary>
        /// Applies a substitution to this term.
        /// </summary>
        /// <param name="s">The substitution to apply.</param>
        /// <returns>The substituted value.</returns>
        public abstract TypeTerm Substitute(Substitution s);

        public override bool Equals(object obj)
        {
            if (obj is TypeTerm term)
            {
                return Equals(term);
            }
            else
            {
                return base.Equals(obj);
            }
        }
    }

}
