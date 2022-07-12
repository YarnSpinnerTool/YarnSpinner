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

        // override object.Equals
        public override bool Equals(object obj)
        {
            return obj is IType type && this.Equals(type);
        }
        
        // override object.GetHashCode
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override string ToString() => Name;

        public bool Equals(IType other)
        {
            return other is TypeVariable otherVariable && otherVariable.Name == Name;
        }

        public static implicit operator TypeVariable(string input)
        {
            return new TypeVariable(input);
        }
    }

}
