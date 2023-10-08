#define DISALLOW_NULL_EQUATION_TERMS

namespace TypeChecker
{
    using System;
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
        public override string ToString() => $"{this.FromType} c> {this.ToType} ({SourceRange}: {SourceExpression})";

        /// <inheritdoc/>
        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            if (this.FromType.Equals(this.ToType))
            {
                // Early out: if the terms are identical, we simplify to 'true'.
                return new TrueConstraint(this);
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
                    var equality = new TypeEqualityConstraint(actualFromLiteral, actualFromLiteral)
                    {
                        SourceExpression = this.SourceExpression,
                        SourceFileName = this.SourceFileName,
                        SourceRange = this.SourceRange,
                        FailureMessageProvider = this.FailureMessageProvider
                    };
                    return equality;
                }
                else
                {
                    // We know their concrete types and they're not convertible.
                    // Return a constraint that is guaranteed to fail: from
                    // fromLiteral == toLiteral
                    var equality = new TypeEqualityConstraint(actualFromLiteral, actualToLiteral)
                    {
                        SourceExpression = this.SourceExpression,
                        SourceFileName = this.SourceFileName,
                        SourceRange = this.SourceRange,
                        FailureMessageProvider = this.FailureMessageProvider
                    };
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
                // Both the 'from' and 'to' types are variables, so we can't
                // make any assertions about their relationship to any concrete
                // types. Assert that they're the same type.

                allPairs = new[] { new[] { ToType, FromType } };
            }

            var allPossibleEqualities = allPairs.Select(pair =>
            {
                var fromConstraint = new TypeEqualityConstraint(pair.ElementAt(0), this.FromType);
                var toConstraint = new TypeEqualityConstraint(pair.ElementAt(1), this.ToType);

                foreach (var constraint in new[] { fromConstraint, toConstraint }) {
                    constraint.SourceExpression = this.SourceExpression;
                    constraint.SourceFileName = this.SourceFileName;
                    constraint.SourceRange = this.SourceRange;
                    constraint.FailureMessageProvider = this.FailureMessageProvider;
                }

                var constraints = new[]
                {
                        fromConstraint,
                        toConstraint,
                };

                var conjunction = new ConjunctionConstraint(constraints)
                {
                    SourceExpression = this.SourceExpression,
                    SourceFileName = this.SourceFileName,
                    SourceRange = this.SourceRange,
                    FailureMessageProvider = this.FailureMessageProvider
                };
                return conjunction;
            
            }).NotNull();

            if (allPossibleEqualities.Count() == 1)
            {
                // Precisely one possible equality. Return a single constraint
                // constraint.
                var equality = allPossibleEqualities.Single();
                equality.SourceExpression = this.SourceExpression;
                equality.SourceFileName = this.SourceFileName;
                equality.SourceRange = this.SourceRange;
                equality.FailureMessageProvider = this.FailureMessageProvider;
                return equality;
            }
            else
            {
                // More than one possible equality. Return a disjunction
                // constraint.
                var disjunction = new DisjunctionConstraint(allPossibleEqualities)
                {
                    SourceExpression = this.SourceExpression,
                    SourceFileName = this.SourceFileName,
                    SourceRange = this.SourceRange,
                    FailureMessageProvider = this.FailureMessageProvider
                };
                return disjunction;
            }
        }

        public override IEnumerable<TypeConstraint> DescendantsAndSelf
        {
            get
            {
                yield return this;
            }
        }

        public override IEnumerable<TypeConstraint> Children => Array.Empty<TypeConstraint>();

        public override bool IsTautological
        {
            get
            {
                if (ITypeExtensions.Equals(FromType, ToType)) {
                    return true;
                } else if (FromType is TypeBase fromLiteral && ToType is TypeBase toLiteral && fromLiteral.IsConvertibleTo(toLiteral)) {
                    return true;
                }

                return false;
            }
        }
    }
}
