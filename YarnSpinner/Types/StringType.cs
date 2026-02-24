// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// A type that represents string values.
    /// </summary>
    internal class StringType : TypeBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StringType"/>
        /// class.
        /// </summary>
        public StringType(): base(null) { }

        /// <inheritdoc/>
        public override string Name => "String";

        /// <inheritdoc/>
        public override IType Parent => Types.Any;

        /// <inheritdoc/>
        public override string Description { get; } = "String";

        /// <inheritdoc/>
        internal override System.IConvertible DefaultValue => string.Empty;

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return base.ToString();
        }
    }
}
