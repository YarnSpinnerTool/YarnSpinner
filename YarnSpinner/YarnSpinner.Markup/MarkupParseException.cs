// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Markup
{
    using System;

    /// <summary>
    /// An exception representing something going wrong when parsing markup.
    /// </summary>
    /// <seealso cref="LineParser"/>
    [Serializable]
    public class MarkupParseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="MarkupParseException"/> class.
        /// </summary>
        internal MarkupParseException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="MarkupParseException"/> class.
        /// </summary>
        /// <param name="message">An explanation of the exception.</param>
        internal MarkupParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="MarkupParseException"/> class.
        /// </summary>
        /// <param name="message">An explanation of the exception.</param>
        /// <param name="inner">The exception that caused this
        /// exception.</param>
        internal MarkupParseException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
