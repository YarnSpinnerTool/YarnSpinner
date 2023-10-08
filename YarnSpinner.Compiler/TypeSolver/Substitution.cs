using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{

    /// <summary>
    /// A substitution is a mapping between type variables and types (or other
    /// type variables). It is produced during the process of type resolution by
    /// the <see cref="Solver"/> class.
    /// </summary>
    public class Substitution : Dictionary<TypeVariable, IType>
    {
        /// <summary>
        /// Returns a duplicate of this <see cref="Substitution"/>.
        /// </summary>
        /// <returns>The cloned <see cref="Substitution"/>.</returns>
        public Substitution Clone()
        {
            var clone = new Substitution();
            foreach (var entry in this)
            {
                clone.Add(entry.Key, entry.Value);
            }
            return clone;
        }

        public bool TryResolveTypeVariable(TypeVariable variable, out IType result) {
            var checkedSet = new HashSet<TypeVariable>();

            var current = variable;

            while (checkedSet.Contains(current) == false) {
                if (this.ContainsKey(current) == false) {
                    result = default;
                    return false;
                }
                result = this[current];
                if (result is TypeVariable typeVariable) {
                    checkedSet.Add(current);
                    current = typeVariable;
                    continue;
                } else {
                    return true;
                }
            }

            result = default;
            return false;
        }
    }

}
