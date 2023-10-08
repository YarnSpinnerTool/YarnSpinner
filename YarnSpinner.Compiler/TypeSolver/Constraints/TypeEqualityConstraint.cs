#define DISALLOW_NULL_EQUATION_TERMS


using System;
using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{

    internal class TypeEqualityConstraint : TypeConstraint, IEquatable<TypeEqualityConstraint>
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

        public override string ToString() => $"{Left} == {Right} ({SourceRange}: {SourceExpression})";

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            // Equality constraints are already at their most simple - they can't be
            // simplified further
            return this;
        }

        public bool Equals(TypeEqualityConstraint other)
        {
            return this.Left == other.Left && this.Right == other.Right
            || this.Left == other.Right && this.Right == other.Left;
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf
        {
            get
            {
                yield return this;
            }
        }

        public override IEnumerable<TypeConstraint> Children => Array.Empty<TypeConstraint>();

        public override bool IsTautological => ITypeExtensions.Equals(Left,Right);
    }
}
