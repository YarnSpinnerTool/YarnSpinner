// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// Represents any type. This type is used in circumstances when a type
    /// is known to have a value, but the specific type is not known or
    /// required to be known.
    /// </summary>
    /// <remarks>
    /// This is the parent type of all types.
    /// </remarks>
    internal class AnyType : TypeBase
    {
        public AnyType() : base(null) { }

        public AnyType(MethodCollection methods) : base(methods) { }

        /// <inheritdoc/>
        public override string Name => "Any";

        /// <inheritdoc/>
        public override IType? Parent { get => null; }

        /// <inheritdoc/>
        public override string Description { get => "Any type."; }

        /// <inheritdoc/>
        public override MethodCollection Methods => new Dictionary<string, System.Delegate>();

        public IReadOnlyDictionary<string, IType> Members =>  new Dictionary<string, Yarn.IType>();

        internal override IConvertible DefaultValue => throw new InvalidOperationException("The Any type does not have a default value.");
    }
}
