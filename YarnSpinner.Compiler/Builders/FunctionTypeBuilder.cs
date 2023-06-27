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
        public FunctionType FunctionType { get; } = new FunctionType();

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
    }
}
