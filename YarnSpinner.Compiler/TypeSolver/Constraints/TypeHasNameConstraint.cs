// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{
    /// <summary>
    /// A <see cref="TypeConstraint"/> that represents a type with an explicit name.
    /// The most common case of this is when encountering a declaration statement.
    /// </summary>
    internal class TypeHasNameConstraint : TypeConstraint
    {
        public TypeHasNameConstraint(IType type, string name)
        {
            this.Type = type;
            this.Name = name;
        }

        public IType Type { get; private set; }
        public string Name { get; private set; }

        /// <inheritdoc/>
        public override IEnumerable<TypeVariable> AllVariables => new[] { Type }.OfType<TypeVariable>();

        public override IEnumerable<TypeConstraint> DescendantsAndSelf
        {
            get
            {
                yield return this;
            }
        }

        public override IEnumerable<TypeConstraint> Children => Array.Empty<TypeConstraint>();

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            // Find all types that have this name, and return a disjunction
            // containing an equality constraint of each possibility.
            TypeConstraint typeConstraint = new DisjunctionConstraint(
                            knownTypes.Where(t => t.Name == this.Name)
                            .Select(t => new TypeEqualityConstraint(this.Type, t))).Simplify(subst, knownTypes);

            typeConstraint.SourceExpression = this.SourceExpression;
            typeConstraint.SourceRange = this.SourceRange;
            typeConstraint.SourceFileName = this.SourceFileName;
            typeConstraint.DiagnosticProvider = this.DiagnosticProvider;
            return typeConstraint;
        }

        public override string ToString()
        {
            return $"nameof({Type}) == \"{this.Name}\" ({SourceRange}: {SourceExpression})";
        }

        public override bool IsTautological => Type is TypeBase concreteType && concreteType.Name == this.Name;
    }
}
