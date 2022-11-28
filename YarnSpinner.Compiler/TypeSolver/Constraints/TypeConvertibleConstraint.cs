#define DISALLOW_NULL_EQUATION_TERMS

namespace TypeChecker
{
    using System.Collections.Generic;
    using System.Linq;
    using Yarn;

    internal class TypeConvertibleConstraint : TypeConstraint
    {
        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="TypeConvertibleConstraint"/> class.
        /// </summary>
        /// <param name="type">The type to constraint to one that is convertible
        /// to <paramref name="convertibleToType"/></param>
        /// <param name="convertibleToType">The type that <paramref
        /// name="type"/> should be constrained to be convertible to.</param>
        public TypeConvertibleConstraint(IType type, IType convertibleToType)
        {
            this.FromType = type;
            this.ToType = convertibleToType;
        }

        /// <summary>
        /// Gets or sets the type that is being constrained to one that is
        /// convertible to <see cref="ToType"/>.
        /// </summary>
        public IType FromType { get; set; }

        /// <summary>
        /// Gets or sets the type that <see cref="FromType"/> can be converted
        /// to.
        /// </summary>
        public IType ToType { get; set; }

        /// <inheritdoc/>
        public override IEnumerable<TypeVariable> AllVariables => new[] { this.FromType, this.ToType }.OfType<TypeVariable>();

        /// <inheritdoc/>
        public override string ToString() => $"{this.FromType} c> {this.ToType} ({SourceRange})";

        /// <inheritdoc/>
        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            if (this.FromType.Equals(this.ToType))
            {
                // Early out: if the terms are identical, there's no additional
                // constraints to generate.
                return null;
            }

            IEnumerable<TypeBase> AllTypesConvertibleFrom(TypeBase from)
            {
                return knownTypes.Where(other => from.IsConvertibleTo(other)).OrderByDescending(t => t.TypeDepth);
            }

            IEnumerable<TypeBase> AllTypesConvertibleTo(TypeBase to)
            {
                return knownTypes.Where(other => other.IsConvertibleTo(to)).OrderByDescending(t => t.TypeDepth);
            }

            var substitutedFromType = this.FromType.Substitute(subst);
            var substitutedToType = this.ToType.Substitute(subst);

            if (substitutedFromType is TypeBase actualFromLiteral
            && substitutedToType is TypeBase actualToLiteral)
            {
                // We know their concrete types already! We can do a fast check
                // to see if 'from' is convertible to 'to'.
                if (actualFromLiteral.IsConvertibleTo(actualToLiteral))
                {
                    // The two types are convertible because they're declared to
                    // be.

                    // Return a constraint that we know will work: fromLiteral
                    // == fromLiteral
                    var equality = new TypeEqualityConstraint(actualFromLiteral, actualFromLiteral);
                    equality.SourceFileName = this.SourceFileName;
                    equality.SourceRange = this.SourceRange;
                    equality.FailureMessageProvider = this.FailureMessageProvider;
                    return equality;
                }
                else
                {
                    // We know their concrete types and they're not convertible.
                    // Return a constraint that is guaranteed to fail: from
                    // fromLiteral == toLiteral
                    var equality = new TypeEqualityConstraint(actualFromLiteral, actualToLiteral);
                    equality.SourceFileName = this.SourceFileName;
                    equality.SourceRange = this.SourceRange;
                    equality.FailureMessageProvider = this.FailureMessageProvider;
                    return equality;
                }
            }

            IEnumerable<IType> fromTypes;
            IEnumerable<IType> toTypes;

            IEnumerable<IEnumerable<IType>> allPairs;

            if (substitutedFromType is TypeBase fromLiteral)
            {
                // We know 'from' is a literal. This means 'to' must be a type
                // that is convertible from 'from'.
                fromTypes = new[] { fromLiteral };
                toTypes = AllTypesConvertibleFrom(fromLiteral);
                allPairs = new[] { fromTypes, toTypes }.CartesianProduct();
            }
            else if (substitutedToType is TypeBase toLiteral)
            {
                // We know 'to' is a literal. This means 'from' must be a type
                // that is convertible to 'to'.
                fromTypes = AllTypesConvertibleTo(toLiteral);
                toTypes = new[] { toLiteral };
                allPairs = new[] { fromTypes, toTypes }.CartesianProduct();
            }
            else
            {
                // Neither 'from' nor 'to' are literals, so we have no way to
                // produce a reduced list of candidates for equalities. The best
                // we can do is to test all possible combinations (which is a
                // lot of work!)
                fromTypes = new[] { this.FromType };
                toTypes = new[] { this.ToType };

                allPairs = new[] { fromTypes, knownTypes }.CartesianProduct()
                    .Concat(new[] { toTypes, knownTypes }.CartesianProduct());
            }

            var allPossibleEqualities = allPairs.Select(pair =>
            {
                var fromConstraint = new TypeEqualityConstraint(pair.ElementAt(0), this.FromType);
                var toConstraint = new TypeEqualityConstraint(pair.ElementAt(1), this.ToType);

                foreach (var constraint in new[] { fromConstraint, toConstraint }) {
                    constraint.SourceFileName = this.SourceFileName;
                    constraint.SourceRange = this.SourceRange;
                    constraint.FailureMessageProvider = this.FailureMessageProvider;
                }

                var constraints = new[]
                {
                        fromConstraint,
                        toConstraint,
                }.WithoutTautologies();

                if (constraints.Count() == 0)
                {
                    return new TrueConstraint();
                } else if (constraints.Count() == 1)
                {
                    return constraints.Single();
                }
                else
                {
                    var conjunction = new ConjunctionConstraint(constraints);
                    conjunction.SourceFileName = this.SourceFileName;
                    conjunction.SourceRange = this.SourceRange;
                    conjunction.FailureMessageProvider = this.FailureMessageProvider;
                    return conjunction;
                }
            });

            if (allPossibleEqualities.Count() == 1)
            {
                // Precisely one possible equality. Return a single constraint
                // constraint.
                var equality = allPossibleEqualities.Single();
                equality.SourceFileName = this.SourceFileName;
                equality.SourceRange = this.SourceRange;
                equality.FailureMessageProvider = this.FailureMessageProvider;
                return equality;
            }
            else
            {
                // More than one possible equality. Return a disjunction
                // constraint.
                var disjunction = new DisjunctionConstraint(allPossibleEqualities);
                disjunction.SourceFileName = this.SourceFileName;
                disjunction.SourceRange = this.SourceRange;
                disjunction.FailureMessageProvider = this.FailureMessageProvider;
                return disjunction;
            }
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf()
        {
            yield return this;
        }
    }
}
