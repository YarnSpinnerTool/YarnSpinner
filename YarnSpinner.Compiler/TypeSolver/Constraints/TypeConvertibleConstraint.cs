#define DISALLOW_NULL_EQUATION_TERMS


using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{
    internal class TypeConvertibleConstraint : TypeConstraint
    {
        /// <summary>
        /// Gets or sets the type that is being constrained.
        /// </summary>
        public IType Type { get; set; }

        /// <summary>
        /// Gets or sets the type that <see cref="Type"/> can be converted to.
        /// </summary>
        public IType ConvertibleToType { get; set; }

        public TypeConvertibleConstraint(IType type, IType convertibleToType)
        {
            this.Type = type;
            this.ConvertibleToType = convertibleToType;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.Type} <c {this.ConvertibleToType}";

        /// <inheritdoc/>
        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
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
                // This constraint indicates that the type is convertible to
                // some other type. To simplify this constraint, we'll also get
                // all builtin literals that THAT other type is convertible to; we are
                // transitively convertible to them.

                var possibleChildTypes = knownTypes.Where(other => childLiteral.IsConvertibleTo(other));

                if (possibleChildTypes.Count() == 1)
                {
                    // There's only a single possible type that ConvertibleTo could be. Constrain Term to be equal to it.
                    return new TypeEqualityConstraint(resolvedChildTerm, possibleChildTypes.Single());
                }

                // This type could be convertible to a number of other literals.
                // Create a disjunction of type equality constraints (i.e. (T1
                // == T2 or T1 == T3 or ...) )

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
