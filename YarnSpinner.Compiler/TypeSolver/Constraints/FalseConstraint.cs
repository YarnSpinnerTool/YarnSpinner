using System;
using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{
    /// <summary>
    /// A constraint that always fails to resolve.
    /// </summary>
    /// <remarks>This type of constraint is useful for representing error
    /// messages, and for simplifying larger but known-to-be-unresolvable
    /// constraint systems.</remarks>
    internal class FalseConstraint : TypeConstraint
    {
        public FalseConstraint(TypeConstraint source) {
            this.FailureMessageProvider = source.FailureMessageProvider;
            this.SourceExpression = source.SourceExpression;
            this.SourceFileName = source.SourceFileName;
            this.SourceRange = source.SourceRange;
        }

        public override IEnumerable<TypeVariable> AllVariables => Enumerable.Empty<TypeVariable>();

        public override IEnumerable<TypeConstraint> Children => Array.Empty<TypeConstraint>();

        public override IEnumerable<TypeConstraint> DescendantsAndSelf { get{ yield return this; } }

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes) => this;

        public override string ToString() => "false";
    }

}
