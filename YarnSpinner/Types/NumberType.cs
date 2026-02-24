// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// A type that represents floating-point number values.
    /// </summary>
    internal class NumberType : TypeBase
    {
        /// <inheritdoc/>
        internal override System.IConvertible DefaultValue => default(float);

        /// <inheritdoc/>
        public override string Name => "Number";

        /// <inheritdoc/>
        public override IType Parent => Types.Any;

        /// <inheritdoc/>
        public override string Description => "Number";

        public NumberType() : base(null) { }

        /// <inheritdoc/>
        public float ToBridgedType(Value value)
        {
            throw new System.NotImplementedException();
        }
    }
}
