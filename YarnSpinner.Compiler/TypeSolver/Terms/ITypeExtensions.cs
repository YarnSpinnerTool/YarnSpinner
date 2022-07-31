using System.Collections.Generic;
using System.Linq;
using Yarn;
namespace TypeChecker
{
    public static class ITypeExtensions
    {
        /// <summary>
        /// Attempts to provide a substituted version of this term, given a
        /// substitution to draw from.
        /// </summary>
        /// <remarks>
        /// <para>You can use <see cref="Solver.Solve"/> to build a Substitution from
        /// a system of <see cref="TypeConstraint"/> objects. Once you have a
        /// <see cref="Substitution"/>, you can use this method to convert an
        /// <see cref="IType"/> into the solution's result for that type.
        /// </para><para> (That
        /// is: 'substitution' is how you go from a type variable to a type
        /// literal, given a type solution.)</para>
        /// </remarks>
        /// <param name="term">The term to substitute.</param>
        /// <param name="s">A <see cref="Substitution"/> to use.</param>
        /// <returns>A substituted term, or <paramref name="term"/>.</returns>
        public static IType Substitute(this IType term, Substitution s)
        {
            if (term is TypeVariable variable)
            {
                if (s.ContainsKey(variable))
                {
                    // The substitution contains a match for this term, but the
                    // substitute item might ITSELF need substituting. Get the
                    // substite, and then apply the substitution again.
                    return s[variable].Substitute(s);
                }
                else
                {
                    // The substitution does not contain this variable. We can
                    // only return the original variable unmodified.
                    return term;
                }
            }
            else if (term is FunctionType function)
            {
                // Functions are substituted by applying the substitution to
                // their return types and to each of their argument types.
                return new FunctionType(function.ReturnType.Substitute(s), function.Parameters.Select(a => a.Substitute(s)).ToArray());
            }
            else
            {
                // No subsitution to apply here. Return the original type.
                return term;
            }
        }

        /// <summary>
        /// Returns a new collection of type constraints containing all items
        /// from <paramref name="collection"/> that are not tautologies (that
        /// is, that are not equalities of the form X == X).
        /// </summary>
        /// <remarks>
        /// Tautologies are not useful when solving a system of type
        /// constraints, because they always evaluate to true and do not provide
        /// any useful new information. It's often useful to remove them, to
        /// save time processing them.
        /// </remarks>
        /// <param name="collection">A collection of type constraints.</param>
        /// <returns>A new collection of constraints that does not include
        /// tautologies.</returns>
        internal static IEnumerable<TypeConstraint> WithoutTautologies(this IEnumerable<TypeConstraint> collection) {
            return collection.Where(c =>
                !(c is TypeEqualityConstraint equality && equality.Left == equality.Right)
            );
        }
    }
}
