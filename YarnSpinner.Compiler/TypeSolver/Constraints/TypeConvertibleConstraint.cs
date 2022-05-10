#define DISALLOW_NULL_EQUATION_TERMS


using System.Linq;
using Yarn;

namespace TypeChecker
{
    public class TypeConvertibleConstraint : TypeConstraint
    {
        public IType Type { get; set; }
        public IType ConvertibleToType { get; set; }

        public TypeConvertibleConstraint(IType type, IType convertibleToType)
        {
            this.Type = type;
            this.ConvertibleToType = convertibleToType;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.Type} <c {this.ConvertibleToType}";

        /// <inheritdoc/>
        public override TypeConstraint Simplify(Substitution subst)
        {
            var resolvedParentTerm = this.ConvertibleToType.Substitute(subst);
            var resolvedChildTerm = this.Type.Substitute(subst);

            if (resolvedChildTerm.Equals(resolvedParentTerm))
            {
                // Early out: if the terms are identical, there's no additional
                // constraints to generate.
                return null;
            }

            if (resolvedChildTerm is TypeBase childLiteral)
            {
                var possibleChildTypes = Types.AllTypes.Where(other => childLiteral.IsConvertibleTo(other));

                if (possibleChildTypes.Count() == 1)
                {
                    // There's only a single possible type that ConvertibleTo could be. Constrain Term to be equal to it.
                    return new TypeEqualityConstraint(resolvedChildTerm, possibleChildTypes.Single());
                }

                // Sort the list by descending depth - we want the most specific
                // type to be checked first
                possibleChildTypes = possibleChildTypes.OrderByDescending(t => t.TypeDepth);

                var possibleConstraints = possibleChildTypes.Select(subtype => new TypeEqualityConstraint(resolvedChildTerm, subtype));

                return new DisjunctionConstraint(possibleConstraints);
            }
            else
            {
                // It's something else. The only useful constraint we can generate
                // is that they're the same.
                return new TypeEqualityConstraint(resolvedParentTerm, resolvedChildTerm);
            }
        }
    }
}
