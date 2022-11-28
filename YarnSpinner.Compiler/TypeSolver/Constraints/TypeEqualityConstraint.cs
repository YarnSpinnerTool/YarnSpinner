#define DISALLOW_NULL_EQUATION_TERMS


using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{

    internal class TypeEqualityConstraint : TypeConstraint
    {
        public IType Left { get; set; }
        public IType Right { get; set; }

        /// <inheritdoc/>
        public override IEnumerable<TypeVariable> AllVariables => new[] { Left, Right }.OfType<TypeVariable>();

        public TypeEqualityConstraint(IType left, IType right)
        {
            Left = left;
            Right = right;
        }

        public override string ToString() => $"{Left} == {Right} ({SourceRange})";

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            // Equality constraints are already at their most simple - they can't be
            // simplified further
            return this;
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf()
        {
            yield return this;
        }
    }
}
