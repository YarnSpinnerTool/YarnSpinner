#define DISALLOW_NULL_EQUATION_TERMS

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{

    internal class DisjunctionConstraint : TypeConstraint, IEnumerable<TypeConstraint>
    {
        public IEnumerable<TypeConstraint> Constraints { get; private set; }
        
        /// <inheritdoc/>
        public override IEnumerable<TypeVariable> AllVariables => Constraints.SelectMany(c => c.AllVariables).Distinct();

        public DisjunctionConstraint(TypeConstraint left, TypeConstraint right)
        {
            Constraints = new[] { left, right };
        }

        public DisjunctionConstraint(IEnumerable<TypeConstraint> constraints)
        {
            Constraints = constraints;
        }

        public override string ToString() => string.Join(" ∨ ", Constraints.Select(t => $"({t.ToString()})"));

        public IEnumerator<TypeConstraint> GetEnumerator()
        {
            return Constraints.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Constraints).GetEnumerator();
        }

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<Yarn.TypeBase> knownTypes)
        {
            // There are two ways to simplify a disjunction:
            //
            // 1. If we have only a single term, simplify to that term.
            // 2. Otherwise, discard any redundant terms.
            if (this.Constraints.Count() == 1)
            {
                return this.Constraints.Single().Simplify(subst, knownTypes);
            }
            else
            {
                var disjunct = new DisjunctionConstraint(
                    this.Constraints.Distinct()
                                    .Select(c => c.Simplify(subst, knownTypes))
                                    .Where(t => t.GetType() != typeof(TrueConstraint)));
                disjunct.FailureMessageProvider = this.FailureMessageProvider;
                disjunct.SourceFileName = this.SourceFileName;
                disjunct.SourceRange = this.SourceRange;
                return disjunct;
            }
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf()
        {
            return Constraints.SelectMany(c => c.DescendantsAndSelf()).Prepend(this);
        }
    }
}
