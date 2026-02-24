// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// A type that represents boolean values.
    /// </summary>
    internal class BooleanType : TypeBase
    {
        /// <inheritdoc/>
        internal override System.IConvertible DefaultValue => default(bool);

        /// <inheritdoc/>
        public override string Name => "Bool";

        /// <inheritdoc/>
        public override IType Parent => Types.Any;

        /// <inheritdoc/>
        public override string Description => "Bool";

        internal BooleanType() : base(null) { }
    }
}
