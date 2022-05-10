using System;
using System.Collections.Generic;

namespace Yarn
{
    /// <summary>
    /// Provides the base class for all concrete types.
    /// </summary>
    internal abstract class TypeBase : IType, IEquatable<TypeBase>
    {
        public abstract string Name { get; }
        public abstract IType Parent { get; }
        public abstract string Description { get; }

        public IReadOnlyDictionary<string, Delegate> Methods => methods;

        internal Dictionary<string, Delegate> methods = new Dictionary<string, Delegate>();

        public IReadOnlyDictionary<string, IType> Members => members;

        internal Dictionary<string, IType> members = new Dictionary<string, IType>();

        public IReadOnlyCollection<IType> ConvertibleToTypes => convertibleToTypes;

        internal HashSet<IType> convertibleToTypes = new HashSet<IType>();

        /// <summary>
        /// Registers that this type is convertible to <paramref name="otherType"/>.
        /// </summary>
        /// <param name="otherType"></param>
        public void AddConvertibleTo(TypeBase otherType)
        {
            convertibleToTypes.Add(otherType);
        }

        public bool IsConvertibleTo(TypeBase otherType)
        {
            // A type is convertible to another type if 1. there is an explicit
            // conversion available, or 2. it is a descendant of that type, or
            // 3. the two types are identical.
            if (convertibleToTypes.Contains(otherType))
            {
                return true;
            }

            return this == otherType || otherType.IsAncestorOf(this);
        }

        protected TypeBase(IReadOnlyDictionary<string, Delegate> methods) {
            if (methods == null) {
                return;
            }
            
            foreach (var method in methods) {
                this.methods.Add(method.Key, method.Value);
            }
        }

        public bool IsAncestorOf(TypeBase other)
        {
            IType current = other;
            while (current != null)
            {
                if (current.Equals(this))
                {
                    return true;
                }
                current = current.Parent;
            }
            return false;
        }

        public bool Equals(TypeBase other)
        {
            return other != null
                && this.Name == other.Name;
        }

        public override bool Equals(object other) {
            if (!(other is TypeBase otherType)) {
                return false;
            }

            return Equals(otherType);
        }
    }
}
