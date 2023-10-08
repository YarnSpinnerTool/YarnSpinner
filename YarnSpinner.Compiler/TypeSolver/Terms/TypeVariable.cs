using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{

    public class TypeVariable : IType, IEquatable<TypeVariable>
    {
        public string Name { get; set; }
        
        public ParserRuleContext Context { get; }

        public IType Parent => null;

        public string Description => $"Type variable representing \"{Name}\"";

        public IReadOnlyDictionary<string, Delegate> Methods => new Dictionary<string, Delegate>();

        public IReadOnlyDictionary<string, IType> Members => new Dictionary<string, IType>();

        // Type variables do not have any members.
        public IReadOnlyDictionary<string, ITypeMember> TypeMembers => TypeBase.EmptyTypeMemberDictionary;

        public TypeVariable(string name, Antlr4.Runtime.ParserRuleContext context)
        {
            Name = name;
            Context = context;
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

        public override string ToString()
        {
            if (Context != null) {
                return $@"{Name} ('{Context.GetText()}')";
            } else {
                return Name;
            }
        }

        public bool Equals(IType other)
        {
            return other is TypeVariable otherVariable && this.Equals(otherVariable);
        }

        public bool Equals(TypeVariable other)
        {
            return other.Name == Name;
        }

        public static implicit operator TypeVariable(string input)
        {
            return new TypeVariable(input, null);
        }
    }

}
