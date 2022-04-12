using System.Linq;

namespace TypeChecker
{
    internal class TypeHasNameConstraint : TypeConstraint
    {
        public TypeHasNameConstraint(ITypeTerm type, string name)
        {
            this.Type = type;
            this.Name = name;
        }

        public ITypeTerm Type { get; private set; }
        public string Name { get; private set; }

        public override TypeConstraint Simplify(Substitution subst)
        {
            // Find all types that have this name, and return a disjunction
            // containing an equality constraint of each possibility.
            return new DisjunctionConstraint(
                Types.AllTypes.Where(t => t.Name == this.Name)
                .Select(t => new TypeEqualityConstraint(this.Type, t))
            );
        }

        public override string ToString()
        {
            return $"nameof({Type}) == \"{this.Name}\"";
        }
    }
}
