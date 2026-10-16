// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

using System;

namespace Yarn
{
    /// <summary>
    /// Represents a type that is an error.
    /// </summary>
    /// <remarks>
    /// The purpose of this type is to be a catchall for any scenario where the type can't be resolved or is impossible
    /// </remarks>
    internal class ErrorType : TypeBase
    {
        /// <summary>
        /// Creates a new error type
        /// </summary>
        public ErrorType() : base(null)
        {
        }

        /// <inheritdoc/>
        public override string Name => "<ERROR>";

        /// <inheritdoc/>
        public override IType? Parent => null;

        /// <inheritdoc/>
        public override string Description => "(type error)";

        /// <inheritdoc/>
        internal override System.IConvertible DefaultValue => throw new InvalidOperationException();
    }
}
