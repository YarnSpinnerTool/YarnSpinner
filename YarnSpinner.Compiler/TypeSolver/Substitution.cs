using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{

    public class Substitution : Dictionary<TypeVariable, IType>
    {
        public List<string> FailureChain { get; private set; } = new List<string>();
        public bool IsFailed => FailureChain.Count > 0;

        public void Fail(string reason) => FailureChain.Add(reason);

        public string FailureReason => string.Join(", because ", FailureChain);

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
            clone.FailureChain = new List<string>(FailureChain);
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
