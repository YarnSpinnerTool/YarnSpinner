using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{

    public class TypeFunction : ITypeTerm
    {
        public ITypeTerm ReturnType { get; set; }
        public IEnumerable<ITypeTerm> ArgumentTypes { get; set; }

        public TypeFunction(ITypeTerm returnType, params ITypeTerm[] argumentTypes)
        {
            ReturnType = returnType ?? Types.Error;
            ArgumentTypes = argumentTypes;
        }

        public string ToString() => $"({string.Join(", ", ArgumentTypes)}) -> {ReturnType}";

        public ITypeTerm Substitute(Substitution s)
        {
            return new TypeFunction(ReturnType.Substitute(s), ArgumentTypes.Select(a => a.Substitute(s)).ToArray());
        }

        public bool Equals(ITypeTerm other)
        {
            return other is TypeFunction otherFunction
                && otherFunction.ReturnType == ReturnType
                && ArgumentTypes
                    .Zip(otherFunction.ArgumentTypes, (a, b) => (First: a, Second: b))
                    .All(pair => pair.First == pair.Second);
        }
    }
}
