using System;
using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{

    public class TypeVariable : IType
    {
        public string Name { get; set; }

        public IType Parent => null;

        public string Description => $"Type variable representing \"{Name}\"";

        public IReadOnlyDictionary<string, Delegate> Methods => new Dictionary<string, Delegate>();

        public IReadOnlyDictionary<string, IType> Members => new Dictionary<string, IType>();

        public TypeVariable(string name)
        {
            Name = name;
        }

        public override string ToString() => Name;

        public bool Equals(IType other)
        {
            return other is TypeVariable otherVariable && otherVariable.Name == Name;
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public static implicit operator TypeVariable(string input)
        {
            return new TypeVariable(input);
        }
    }

}
