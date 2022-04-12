#define DISALLOW_NULL_EQUATION_TERMS


namespace TypeChecker
{

    public class TypeEqualityConstraint : TypeConstraint
    {
        public ITypeTerm Left { get; set; }
        public ITypeTerm Right { get; set; }

        public TypeEqualityConstraint(ITypeTerm left, ITypeTerm right)
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
