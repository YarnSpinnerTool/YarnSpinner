using System;
using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{

    public class TypeVariable : IType
    {
        public string Name { get; set; }

        public IType Parent => throw new NotImplementedException();

        public string Description => throw new NotImplementedException();

        public IReadOnlyDictionary<string, Delegate> Methods => throw new NotImplementedException();

        public IReadOnlyDictionary<string, IType> Members => throw new NotImplementedException();

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
