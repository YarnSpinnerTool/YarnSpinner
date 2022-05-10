#define DISALLOW_NULL_EQUATION_TERMS

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{

    public class DisjunctionConstraint : TypeConstraint, IEnumerable<TypeConstraint>
    {
        public IEnumerable<TypeConstraint> Constraints { get; private set; }

        public DisjunctionConstraint(TypeConstraint left, TypeConstraint right)
        {
            Constraints = new[] { left, right };
        }

        public DisjunctionConstraint(IEnumerable<TypeConstraint> constraints)
        {
            Constraints = constraints;
        }

        public override string ToString() => string.Join(" ∨ ", Constraints.Select(t => t.ToString()));

        public IEnumerator<TypeConstraint> GetEnumerator()
        {
            return Constraints.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Constraints).GetEnumerator();
        }

        public override TypeConstraint Simplify(Substitution subst)
        {
            // TODO: simplify disjunctions by elimiminating redundant terms
            return this;
        }
    }
}
