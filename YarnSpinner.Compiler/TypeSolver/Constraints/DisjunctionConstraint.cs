#define DISALLOW_NULL_EQUATION_TERMS

using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{
    /// <summary>
    /// A disjunction constraint is a logical 'or' that resolves if at least one
    /// of its child constraints resolves.
    /// </summary>
    internal class DisjunctionConstraint : TypeConstraint, IEnumerable<TypeConstraint>
    {
        public IEnumerable<TypeConstraint> Constraints { get; private set; }
        
        /// <inheritdoc/>
        public override IEnumerable<TypeVariable> AllVariables => Constraints.SelectMany(c => c.AllVariables).Distinct();

        public DisjunctionConstraint(TypeConstraint other) {
            if (other is DisjunctionConstraint disjunctionConstraint) {
                this.Constraints = disjunctionConstraint.Constraints;
            } else {
                this.Constraints = new[] { other };
            }
        }

        public DisjunctionConstraint(params TypeConstraint[] constraints) 
        : this((IEnumerable<TypeConstraint>)constraints) { }

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
            // There are three ways to simplify a disjunction:
            //
            // 1. If we represent a tautology, simplify to 'true'.
            // 2. If we have only a single term, simplify to that term.
            // 3. Otherwise, discard any redundant terms.
            if (this.IsTautological) {
                return new TrueConstraint(this);
            }
            else if (this.Constraints.Count() == 1)
            {
                return this.Constraints.Single().Simplify(subst, knownTypes);
            }
            else
            {
                var disjunct = new DisjunctionConstraint(
                    this.Constraints.Distinct()
                                    .Select(c => c.Simplify(subst, knownTypes))
                                    .Where(t => t.GetType() != typeof(FalseConstraint)));
                disjunct.FailureMessageProvider = this.FailureMessageProvider;
                disjunct.SourceContext = this.SourceContext;
                disjunct.SourceFileName = this.SourceFileName;
                disjunct.SourceRange = this.SourceRange;
                disjunct.SourceExpression = this.SourceExpression;
                return disjunct;
            }
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf => Constraints.SelectMany(c => c.DescendantsAndSelf).Prepend(this);

        public override IEnumerable<TypeConstraint> Children => Constraints;

        // A disjunction is tautological if any of its children are tautological
        // (that is, OR(true, X) == true)
        public override bool IsTautological => Children.Any(c => c.IsTautological);
    }
}
