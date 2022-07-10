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
        public IType FromType { get; set; }

        /// <summary>
        /// Gets or sets the type that <see cref="FromType"/> can be converted to.
        /// </summary>
        public IType ToType { get; set; }

        public TypeConvertibleConstraint(IType type, IType convertibleToType)
        {
            this.FromType = type;
            this.ToType = convertibleToType;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.FromType} c> {this.ToType}";

        /// <inheritdoc/>
        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            if (this.FromType.Equals(this.ToType))
            {
                // Early out: if the terms are identical, there's no additional
                // constraints to generate.
                return null;
            }

            IEnumerable<TypeBase> AllTypesConvertibleFrom(TypeBase from) {
                return knownTypes.Where(other => from.IsConvertibleTo(other)).OrderByDescending(t => t.TypeDepth);
            }

            IEnumerable<TypeBase> AllTypesConvertibleTo(TypeBase to) {
                return knownTypes.Where(other => other.IsConvertibleTo(to)).OrderByDescending(t => t.TypeDepth);
            }

            IEnumerable<IType> fromTypes;
            IEnumerable<IType> toTypes;

            if (FromType is TypeBase fromLiteral) {
                // We know 'from' is a literal. This means 'to' must be a type
                // that is convertible from 'from'.
                fromTypes = new[] { fromLiteral };
                toTypes = AllTypesConvertibleFrom(fromLiteral);
            } else if (ToType is TypeBase toLiteral) {
                // We know 'to' is a literal. This means 'from' must be a type
                // that is convertible to 'to'.
                fromTypes = AllTypesConvertibleTo(toLiteral);
                toTypes = new[] { toLiteral };
            } else {
                // Neither 'from' nor 'to' are literals. They could be anything;
                // check all pairs. TODO: this is really inefficient!!
                fromTypes = knownTypes;
                toTypes = knownTypes;
            }

            var allPairs = new[] { fromTypes, toTypes }.CartesianProduct();

            var allPossibleEqualities = allPairs.Select(pair =>
            {
                var fromConstraint = new TypeEqualityConstraint(pair.ElementAt(0), this.FromType);
                var toConstraint = new TypeEqualityConstraint(pair.ElementAt(1), this.ToType);
                
                toConstraint.FailureMessageProvider = this.FailureMessageProvider;
                fromConstraint.FailureMessageProvider = this.FailureMessageProvider;

                var constraints = new[] {
                        fromConstraint,
                        toConstraint,
                }.WithoutTautologies();

                if (constraints.Count() == 1) {
                    return constraints.Single();
                } else {
                    var conjunction = new ConjunctionConstraint(constraints);
                    conjunction.FailureMessageProvider = this.FailureMessageProvider;
                    return conjunction;
                }
            });

            if (allPossibleEqualities.Count() == 1) {
                // Precisely one possible equality. Return a single constraint
                // constraint.
                var equality = allPossibleEqualities.Single();
                equality.FailureMessageProvider = this.FailureMessageProvider;
                return equality;
            } else {
                // More than one possible equality. Return a disjunction
                // constraint.
                var disjunction = new DisjunctionConstraint(allPossibleEqualities);
                disjunction.FailureMessageProvider = this.FailureMessageProvider;
                return disjunction;
            }
        }
    }
}
