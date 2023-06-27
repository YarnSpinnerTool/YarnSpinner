// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    /// <summary>
    /// Defines properties that describe a type in the Yarn language that
    /// can be converted to and from a type in the Common Language Runtime.
    /// </summary>
    /// <typeparam name="TBridgedType">The CLR type that this type can be
    /// bridged to.</typeparam>
    internal interface IBridgeableType<out TBridgedType> : IType
    {
        /// <summary>
        /// Gets a default value appropriate for <typeparamref
        /// name="TBridgedType"/>.
        /// </summary>
        /// <value>A default value.</value>
        TBridgedType DefaultValue { get; }

        /// <summary>
        /// Converts a Value to the type <typeparamref
        /// name="TBridgedType"/>.
        /// </summary>
        /// <param name="value">The Value to convert from.</param>
        /// <returns>A value of type <typeparamref name="TBridgedType"/>,
        /// derived from <paramref name="value"/>.</returns>
        TBridgedType ToBridgedType(Value value);
    }
}
