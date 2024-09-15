// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    /// <summary>
    /// Provides methods for constructing <see cref="Yarn.FunctionType"/>
    /// objects.
    /// </summary>
    /// <remarks>
    /// To use this class, create an instance of it, and call the
    /// <c>With</c>-prefixed methods to set properties. When you're done, access
    /// the <see cref="FunctionType"/> property to get the final, constructed
    /// <see cref="Yarn.FunctionType"/>.
    /// </remarks>
    public class FunctionTypeBuilder
    {
        /// <summary>
        /// Gets the <see cref="FunctionType"/> instance constructed by this
        /// <see cref="FunctionTypeBuilder"/>.
        /// </summary>
        public FunctionType FunctionType { get; } = new FunctionType(Types.Error);

        /// <summary>
        /// Sets the <see cref="FunctionType.ReturnType"/> of the <see
        /// cref="FunctionType"/>.
        /// </summary>
        /// <param name="returnType">The return type to apply to the
        /// function.</param>
        /// <returns>The <see cref="FunctionTypeBuilder"/> instance that
        /// received this method call.</returns>
        public FunctionTypeBuilder WithReturnType(IType returnType)
        {
            this.FunctionType.ReturnType = returnType;
            return this;
        }

        /// <summary>
        /// Adds a new parameter of type <paramref name="parameterType"/> to the
        /// <see cref="FunctionType"/>.
        /// </summary>
        /// <param name="parameterType">The type of the new parameter to add to the function.</param>
        /// <inheritdoc cref="WithReturnType(IType)" path="/returns"/>
        public FunctionTypeBuilder WithParameter(IType parameterType)
        {
            this.FunctionType.AddParameter(parameterType);
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="FunctionTypeBuilder"/> based on a delegate
        /// type.
        /// </summary>
        /// <param name="type">The type of a delegate to produce a type builder
        /// from. This type must be a delegate.</param>
        /// <returns>
        /// A newly created <see cref="FunctionTypeBuilder"/>.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when the provided
        /// type is not a delegate or has invalid components.</exception>
        public static FunctionTypeBuilder FromFunctionType(System.Type type)
        {
            // Check that this type is actually a delegate
            if (typeof(System.Delegate).IsAssignableFrom(type) == false)
            {
                throw new System.ArgumentException($"Type {type} is not assignable from {nameof(System.Delegate)}");
            }

            // Get the 'Invoke' method - we'll reflect on that to get
            // information about the parameters and return type of the delegate.
            var method = type.GetMethod("Invoke") ?? throw new System.ArgumentException($"Type {type} has no method Invoke");

            var functionTypeBuilder = new FunctionTypeBuilder();

            // Ensure that the return type is one that we can handle
            if (Types.TypeMappings.TryGetValue(method.ReturnType, out var returnType) == false)
            {
                throw new System.ArgumentException($"Type {type} has an invalid return type ({method.ReturnType})");
            }

            functionTypeBuilder.FunctionType.ReturnType = returnType;

            // For each parameter, check to see that it meets the requirements for being a Yarn function
            foreach (var param in method.GetParameters())
            {
                if (param.IsOptional)
                {
                    throw new System.ArgumentException($"Parameter {param} must not be optional");
                }

                if (Types.TypeMappings.TryGetValue(param.ParameterType, out var paramType) == false)
                {
                    throw new System.ArgumentException($"Parameter {param.Name} has invalid type ({param.ParameterType})");
                }
                functionTypeBuilder.FunctionType.AddParameter(paramType);
            }

            // We're all done - return the function type builder, ready to
            // produce a new function type
            return functionTypeBuilder;
        }
    }
}
