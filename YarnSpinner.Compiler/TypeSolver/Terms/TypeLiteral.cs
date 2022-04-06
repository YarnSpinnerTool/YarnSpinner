using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{

    public class TypeLiteral : TypeTerm
    {
        public TypeLiteral Parent { get; set; }
        public string Name { get; set; }

        public Dictionary<string, TypeTerm> Members { get; private set; } = new Dictionary<string, TypeTerm>();

        public HashSet<TypeLiteral> ConvertibleToTypes = new HashSet<TypeLiteral>();

        public void AddMember(string name, TypeTerm type)
        {
            Members.Add(name, type);
        }

        /// <summary>
        /// Registers that this type is convertible to <paramref name="otherType"/>.
        /// </summary>
        /// <param name="otherType"></param>
        public void AddConvertibleTo(TypeLiteral otherType)
        {
            ConvertibleToTypes.Add(otherType);
        }

        public bool IsConvertibleTo(TypeLiteral otherType)
        {
            // A type is convertible to another type if 1. there is an explicit
            // conersion available, or 2. it is a descendant of that type, or 3. the
            // two types are identical.
            if (ConvertibleToTypes.Contains(otherType))
            {
                return true;
            }

            return this == otherType || otherType.IsAncestorOf(this);
        }

        public TypeLiteral(string name, TypeLiteral parent = null)
        {
            Name = name;
            Parent = parent;
        }

        public string ToString() => Name;

        public string ToStringWithMembers() => Members.Count == 0 ? ToString() : ToString() + "{" + string.Join(", ", Members.Keys) + "}";

        public override TypeTerm Substitute(Substitution s)
        {
            return new TypeLiteral(Name, Parent)
            {
                Members = Members.ToDictionary(kv => kv.Key, kv => kv.Value is TypeLiteral lit ? lit : kv.Value.Substitute(s)),
            };
        }

        public override bool Equals(TypeTerm other)
        {
            return other is TypeLiteral otherLiteral && otherLiteral.Name == Name;
        }

        public static bool operator ==(TypeLiteral a, TypeTerm b)
        {
            if (a is null && b is null)
            {
                return true;
            }
            return a?.Equals(b) ?? false;
        }

        public static bool operator !=(TypeLiteral a, TypeTerm b)
        {
            return !(a == b);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static implicit operator TypeLiteral(string input)
        {
            return new TypeLiteral(input);
        }

        public bool IsAncestorOf(TypeLiteral other)
        {
            TypeLiteral current = other;
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

        /// <summary>
        /// Gets the number of parents in this type literal's hierarchy.
        /// </summary>
        /// <remarks>
        /// For example, <see cref="Types.Any"/> has a depth of zero, and <see
        /// cref="Types.String"/> has a depth of 1.
        /// </remarks>
        public int TypeDepth
        {
            get
            {
                int depth = 0;
                TypeLiteral current = this;
                while (current != null)
                {
                    current = current.Parent;
                    depth++;
                }
                return depth;
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (ReferenceEquals(obj, null))
            {
                return false;
            }

            if (obj is TypeLiteral other)
            {
                return this.Equals(other);
            }
            else
            {
                return false;
            }
        }
    }
}
