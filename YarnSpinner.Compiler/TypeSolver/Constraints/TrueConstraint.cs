using System;
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
        public TrueConstraint(TypeConstraint source)
        {
            this.FailureMessageProvider = source.FailureMessageProvider;
            this.SourceContext = source.SourceContext;
            this.SourceExpression = source.SourceExpression;
            this.SourceFileName = source.SourceFileName;
            this.SourceRange = source.SourceRange;
        }

        public override IEnumerable<TypeVariable> AllVariables => Enumerable.Empty<TypeVariable>();

        public override IEnumerable<TypeConstraint> DescendantsAndSelf
        {
            get
            {
                yield return this;
            }
        }

        public override IEnumerable<TypeConstraint> Children => Array.Empty<TypeConstraint>();

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes) => this;

        public override string ToString() => "true";

        // A 'true' constraint is by definition tautological
        public override bool IsTautological => true;
    }

}
