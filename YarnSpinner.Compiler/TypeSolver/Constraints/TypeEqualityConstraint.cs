#define DISALLOW_NULL_EQUATION_TERMS


using Yarn;

namespace TypeChecker
{

    public class TypeEqualityConstraint : TypeConstraint
    {
        public IType Left { get; set; }
        public IType Right { get; set; }

        public TypeEqualityConstraint(IType left, IType right)
        {
            Left = left;
            Right = right;
        }

        public override string ToString() => $"{Left} == {Right}";

        public override TypeConstraint Simplify(Substitution subst)
        {
            // Equality constraints are already at their most simple - they can't be
            // simplified further
            return this;
        }
    }
}
