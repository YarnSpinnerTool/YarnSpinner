// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Markup
{
    /// <summary>
    /// A type of <see cref="MarkupValue"/>.
    /// </summary>
    public enum MarkupValueType
    {
#pragma warning disable CA1720
        /// <summary>An integer.</summary>
        Integer,

        /// <summary>A float.</summary>
        Float,

        /// <summary>A string.</summary>
        String,

        /// <summary>A bool.</summary>
        Bool,
#pragma warning restore CA1720
    }
}
