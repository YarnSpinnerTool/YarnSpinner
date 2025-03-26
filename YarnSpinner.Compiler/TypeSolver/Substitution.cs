using System.Collections;
using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{

    /// <summary>
    /// A substitution is a mapping between type variables and types (or other
    /// type variables). It is produced during the process of type resolution by
    /// the <see cref="Solver"/> class.
    /// </summary>
    public class Substitution : IDictionary<TypeVariable, IType>
    {
        private Dictionary<TypeVariable, IType> data;

        public IType this[TypeVariable key] { get => this.data[key]; set => this.data[key] = value; }

        public ICollection<TypeVariable> Keys => this.data.Keys;

        public ICollection<IType> Values => this.data.Values;

        public int Count => ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).IsReadOnly;

        public void Add(TypeVariable key, IType value)
        {
            this.data.Add(key, value);
        }

        public void Add(KeyValuePair<TypeVariable, IType> item)
        {
            ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).Add(item);
        }

        public void Clear()
        {
            ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).Clear();
        }

        /// <summary>
        /// Returns a duplicate of this <see cref="Substitution"/>.
        /// </summary>
        /// <returns>The cloned <see cref="Substitution"/>.</returns>
        public Substitution Clone()
        {
            var clone = new Substitution(this);

            return clone;
        }

        public Substitution()
        {
            this.data = new Dictionary<TypeVariable, IType>();
        }

        public Substitution(Substitution other)
        {
            this.data = new Dictionary<TypeVariable, IType>(other.data);
        }

        public bool Contains(KeyValuePair<TypeVariable, IType> item)
        {
            return ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).Contains(item);
        }

        public bool ContainsKey(TypeVariable key)
        {
            return this.data.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TypeVariable, IType>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TypeVariable, IType>> GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TypeVariable, IType>>)this.data).GetEnumerator();
        }

        public bool Remove(TypeVariable key)
        {
            return this.data.Remove(key);
        }

        public bool Remove(KeyValuePair<TypeVariable, IType> item)
        {
            return ((ICollection<KeyValuePair<TypeVariable, IType>>)this.data).Remove(item);
        }

        public bool TryGetValue(TypeVariable key, out IType value)
        {
            return this.data.TryGetValue(key, out value);
        }

        internal bool TryResolveTypeVariable(TypeVariable variable, out IType? result)
        {
            var checkedSet = new HashSet<TypeVariable>();

            var current = variable;

            while (checkedSet.Contains(current) == false)
            {
                if (this.ContainsKey(current) == false)
                {
                    result = default;
                    return false;
                }
                result = this[current];
                if (result is TypeVariable typeVariable)
                {
                    checkedSet.Add(current);
                    current = typeVariable;
                    continue;
                }
                else
                {
                    return true;
                }
            }

            result = default;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.data).GetEnumerator();
        }
    }

}
