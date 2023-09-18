#define DISALLOW_NULL_EQUATION_TERMS

using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{

    internal class TypeHasMemberConstraint : TypeConstraint
    {
        public IType Type { get; private set; }
        public string MemberName { get; private set; }

        /// <inheritdoc/>
        public override IEnumerable<TypeVariable> AllVariables => new[] { Type }.OfType<TypeVariable>();

        public TypeHasMemberConstraint(IType type, string memberName)
        {
            this.Type = type;
            this.MemberName = memberName;
        }

        public override string ToString()
        {
            return $"{Type}.[{MemberName}] ({SourceRange}: {SourceExpression})";
        }

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            // Find all types that have this member and return a disjunction
            // constraint that checks each of them
            var typeConstraint = new DisjunctionConstraint(
                knownTypes
                .Where(e => e.TypeMembers.ContainsKey(MemberName))
                .Select(e => new TypeEqualityConstraint(this.Type, e)))
            .Simplify(subst, knownTypes);

            typeConstraint.SourceRange = this.SourceRange;
            typeConstraint.SourceFileName = this.SourceFileName;
            typeConstraint.FailureMessageProvider = this.FailureMessageProvider;
            return typeConstraint;
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf()
        {
            yield return this;
        }
    }
}
