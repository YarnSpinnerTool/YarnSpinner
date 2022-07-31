using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{
    internal class TypeHasNameConstraint : TypeConstraint
    {
        public TypeHasNameConstraint(IType type, string name)
        {
            this.Type = type;
            this.Name = name;
        }

        public IType Type { get; private set; }
        public string Name { get; private set; }

        public override TypeConstraint Simplify(Substitution subst, IEnumerable<TypeBase> knownTypes)
        {
            // Find all types that have this name, and return a disjunction
            // containing an equality constraint of each possibility.
            TypeConstraint typeConstraint = new DisjunctionConstraint(
                            knownTypes.Where(t => t.Name == this.Name)
                            .Select(t => new TypeEqualityConstraint(this.Type, t))).Simplify(subst, knownTypes);
            typeConstraint.FailureMessageProvider = this.FailureMessageProvider;
            return typeConstraint;
        }

        public override string ToString()
        {
            return $"nameof({Type}) == \"{this.Name}\"";
        }
    }
}
