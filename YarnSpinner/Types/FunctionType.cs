// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// A type that represents functions.
    /// </summary>
    /// <remarks>
    /// Functions have parameters and a return type, and can be called from
    /// script. Instances of this type are created when the host
    /// application registers new functions (such as through using the <see
    /// cref="Library.RegisterFunction"/> methods or similar.)
    /// </remarks>
    public class FunctionType : IType
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
        public IType Parent { get => BuiltinTypes.Any; }

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

        /// <inheritdoc/>
        // Functions do not have any methods themselves
        public MethodCollection Methods => null;

        /// <summary>
        /// Adds a new parameter to the function.
        /// </summary>
        /// <param name="parameterType">The type of parameter to
        /// add.</param>
        internal void AddParameter(IType parameterType)
        {
            this.Parameters.Add(parameterType);
        }
    }
}
