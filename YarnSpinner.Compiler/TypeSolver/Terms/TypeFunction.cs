using System.Collections.Generic;
using System.Linq;

namespace TypeChecker
{

    public class TypeFunction : TypeTerm
    {
        public TypeTerm ReturnType { get; set; }
        public IEnumerable<TypeTerm> ArgumentTypes { get; set; }

        public TypeFunction(TypeTerm returnType, params TypeTerm[] argumentTypes)
        {
            ReturnType = returnType ?? Types.Error;
            ArgumentTypes = argumentTypes;
        }

        public string ToString() => $"({string.Join(", ", ArgumentTypes)}) -> {ReturnType}";

        public override TypeTerm Substitute(Substitution s)
        {
            return new TypeFunction(ReturnType.Substitute(s), ArgumentTypes.Select(a => a.Substitute(s)).ToArray());
        }

        public override bool Equals(TypeTerm other)
        {
            return other is TypeFunction otherFunction && otherFunction.ReturnType == ReturnType && ArgumentTypes.Zip(otherFunction.ArgumentTypes, (a,b) => (First: a, Second: b)).All(pair => pair.First == pair.Second);
        }
    }
}
