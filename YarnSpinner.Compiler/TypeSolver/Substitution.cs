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
    }

}
