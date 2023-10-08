// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provides properties used to work with members of a type.
    /// </summary>
    public interface ITypeMember {
        /// <summary>
        /// Gets or sets the type of this member.
        /// </summary>
        IType Type { get; }
    }

    /// <summary>
    /// Represents a property that belongs to a type and contains a read-only
    /// value.
    /// </summary>
    /// <remarks>
    /// This kind of type member is useful for constant properties, like enum
    /// cases.
    /// </remarks>
    internal class ConstantTypeProperty : ITypeMember
    {
        public IType Type { get; }

        /// <summary>
        /// Gets the value that is stored in this property.
        /// </summary>
        public IConvertible Value { get; }

        public string Description { get; }

        public ConstantTypeProperty(IType type, IConvertible value, string description)
        {
            this.Type = type;
            this.Value = value;
            this.Description = description;
        }
    }

    /// <summary>
    /// Provides the base class for all concrete types.
    /// </summary>
    internal abstract class TypeBase : IType, IEquatable<TypeBase>
    {

        public static readonly IReadOnlyDictionary<string, ITypeMember> EmptyTypeMemberDictionary = new Dictionary<string, ITypeMember>();

        public abstract string Name { get; }
        public abstract IType? Parent { get; }
        public abstract string Description { get; }

        public override string ToString() => Name;
        
        /// <summary>
        /// Gets the collection of methods that are defined on this type.
        /// </summary>
        public IReadOnlyDictionary<string, Delegate> Methods => methods;

        internal Dictionary<string, Delegate> methods = new Dictionary<string, Delegate>();

        public IReadOnlyDictionary<string, ITypeMember> TypeMembers => typeMembers;

        protected Dictionary<string, ITypeMember> typeMembers = new Dictionary<string, ITypeMember>();

        public IReadOnlyCollection<IType> ConvertibleToTypes => convertibleToTypes;

        internal HashSet<IType> convertibleToTypes = new HashSet<IType>();

        internal abstract IConvertible DefaultValue { get; }

        /// <summary>
        /// Gets the depth of this type in the hierarchy, measured as the total
        /// number of parent-child relationships between this type and a root of
        /// the type system.
        /// </summary>
        /// <remarks>
        /// <see cref="Types.Any"/> and <see cref="Types.Error"/> have a depth
        /// of zero.
        /// </remarks>
        public int TypeDepth
        {
            get
            {
                // Start with zero depth, in case we have no parent
                int depth = 0;

                IType? parent = this.Parent;
                
                // Walk up the parent hierarchy, adding 1 to our depth each time
                while (parent != null)
                {
                    depth += 1;
                    parent = parent.Parent;
                }
                return depth;
            }
        }

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
            // A type is convertible to another type if:
            // 1. there is an explicit conversion available, or 
            // 2. it is a descendant of that type, or
            // 3. the two types are identical.
            if (convertibleToTypes.Contains(otherType))
            {
                // An explicit conversion exists.
                return true;
            }

            if (otherType.IsAncestorOf(this))
            {
                // This type is a descendant of otherType.
                return true;
            }

            if (this.Equals(otherType))
            {
                // The two types are identical.
                return true;
            }

            // This type is not convertible to otherType.
            return false;
        }

        protected TypeBase(IReadOnlyDictionary<string, Delegate>? methods) {
            if (methods == null) {
                return;
            }
            
            foreach (var method in methods) {
                this.methods.Add(method.Key, method.Value);
            }
        }

        public bool IsAncestorOf(TypeBase other)
        {
            IType? current = other;
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

        public override bool Equals(object other)
        {
            if (other is TypeBase otherType)
            {
                return Equals(otherType);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }
}
