using System;
using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace TypeChecker
{

    public class TypeFunction : IType
    {
        public IType ReturnType { get; set; }
        public IEnumerable<IType> ArgumentTypes { get; set; }

        public string Name => "Function";

        public IType Parent => throw new NotImplementedException();

        public string Description => ToString();

        public IReadOnlyDictionary<string, Delegate> Methods => throw new NotImplementedException();

        public TypeFunction(IType returnType, params IType[] argumentTypes)
        {
            ReturnType = returnType ?? Types.Error;
            ArgumentTypes = argumentTypes;
        }

        public string ToString() => $"({string.Join(", ", ArgumentTypes)}) -> {ReturnType}";

        

        public bool Equals(IType other)
        {
            return other is TypeFunction otherFunction
                && otherFunction.ReturnType == ReturnType
                && ArgumentTypes
                    .Zip(otherFunction.ArgumentTypes, (a, b) => (First: a, Second: b))
                    .All(pair => pair.First == pair.Second);
        }
    }
}
