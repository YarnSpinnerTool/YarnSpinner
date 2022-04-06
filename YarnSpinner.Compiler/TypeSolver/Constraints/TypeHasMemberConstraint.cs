#define DISALLOW_NULL_EQUATION_TERMS

using System.Linq;

namespace TypeChecker
{

    internal class TypeHasMemberConstraint : TypeConstraint
    {
        public TypeTerm Type { get; private set; }
        public string MemberName { get; private set; }

        public TypeHasMemberConstraint(TypeTerm type, string memberName)
        {
            this.Type = type;
            this.MemberName = memberName;
        }

        public override string ToString()
        {
            return $"{Type}.[{MemberName}]";
        }

        public override TypeConstraint Simplify(Substitution subst)
        {
            // Find all types that have this member and return a disjunction
            // constraint that checks each of them
            return new DisjunctionConstraint(
                Types.AllTypes
                .Where(e => e.Members.ContainsKey(MemberName))
                .Select(e => new TypeEqualityConstraint(this.Type, e))
                );
        }
    }
}
