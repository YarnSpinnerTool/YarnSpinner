// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A type that represents a function.
    /// </summary>
    /// <remarks>
    /// Functions have parameters and a return type, and can be called from
    /// script. Instances of this type are created when the host
    /// application registers new functions (such as through using the <see
    /// cref="Library.RegisterFunction"/> methods or similar.)
    /// </remarks>
    public class FunctionType : IType, IEquatable<IType>
    {
        /// <inheritdoc/>
        public string Name { get => "Function"; }

        /// <inheritdoc/>
        public string Description
        {
            get
            {
                List<string> parameterNames = new List<string>();
                foreach (var param in this.Parameters)
                {
                    if (param == null)
                    {
                        parameterNames.Add("Undefined");
                    }
                    else
                    {
                        parameterNames.Add(param.Name);
                    }
                }

                var returnTypeName = this.ReturnType?.Name ?? "Undefined";

                return $"({string.Join(", ", parameterNames)}) -> {returnTypeName}";
            }

            set
            {
                throw new System.InvalidOperationException();
            }
        }

        /// <inheritdoc/>
        public IType Parent { get => Types.Any; }

        /// <summary>
        /// Gets the type of value that this function returns.
        /// </summary>
        public IType ReturnType { get; internal set; }

        /// <summary>
        /// Gets the list of the parameter types that this function is
        /// called with.
        /// </summary>
        /// <remarks>
        /// The length of this list also determines the number of
        /// parameters this function accepts (also known as the function's
        /// <i>arity</i>).
        /// </remarks>
        public List<IType> Parameters { get; } = new List<IType>();

        /// <summary>
        /// Gets the type of the parameter at the given index.
        /// </summary>
        /// <param name="index">The index of the parameter to get the type
        /// for.</param>
        /// <returns>The type of the parameter. If <paramref name="index"/> is
        /// beyond the length of <see cref="Parameters"/>, and <see
        /// cref="VariadicParameterType"/> is not <see langword="null"/>, <see
        /// cref="VariadicParameterType"/> is returned. </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">Thrown when
        /// <paramref name="index"/> is less than zero, or is beyond the length
        /// of <see cref="Parameters"/> and <see cref="VariadicParameterType"/>
        /// is <see langword="null"/>.
        /// </exception>
        public IType GetParameterAt(int index)
        {
            if (index < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }

            if (index < this.Parameters.Count)
            {
                return this.Parameters[index];
            }
            else if (this.VariadicParameterType != null)
            {
                return this.VariadicParameterType;
            }
            else
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Gets the type of value that this type of function accepts as a
        /// variadic parameter.
        /// </summary>
        /// <remarks>This value is <see langword="null"/> if this type of
        /// function does not accept variadic parameters.</remarks>
        public IType? VariadicParameterType { get; internal set; }

        /// <inheritdoc/>
        // Functions do not have any type members
        public IReadOnlyDictionary<string, ITypeMember> TypeMembers => TypeBase.EmptyTypeMemberDictionary;

        /// <summary>
        /// Initialises a new instances of the <see cref="FunctionType"/> class.
        /// </summary>
        /// <param name="returnType">The type of the value that this type of
        /// function returns.</param>
        /// <param name="parameterTypes">The types of the parameters that this
        /// type of function accepts.</param>
        public FunctionType(IType returnType, params IType[] parameterTypes)
        {
            ReturnType = returnType ?? Types.Error;
            Parameters = parameterTypes.ToList();
            VariadicParameterType = null;
        }

        /// <summary>
        /// Adds a new parameter to the function.
        /// </summary>
        /// <param name="parameterType">The type of parameter to
        /// add.</param>
        internal void AddParameter(IType parameterType)
        {
            this.Parameters.Add(parameterType);
        }

        /// <inheritdoc/>
        public override string ToString() => $"({string.Join(", ", Parameters)}) -> {ReturnType}";

        /// <inheritdoc/>
        public bool Equals(IType other)
        {
            return other is FunctionType otherFunction
                && otherFunction.ReturnType == ReturnType
                && Parameters
                    .Zip(otherFunction.Parameters, (a, b) => (First: a, Second: b))
                    .All(pair => pair.First == pair.Second);
        }
    }
}
