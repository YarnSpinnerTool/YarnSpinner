using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{
    /// <summary>
    /// A <see cref="TypeConstraint"/> that is always resolvable under any circumstance.
    /// </summary>
    internal class TrueConstraint : TypeConstraint
    {
        public override IEnumerable<TypeVariable> AllVariables => Enumerable.Empty<TypeVariable>();

        public override IEnumerable<TypeConstraint> DescendantsAndSelf()
        {
            yield return this;
        }

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes) => this;

        public override string ToString() => "true";
    }

}
