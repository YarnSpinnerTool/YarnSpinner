using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("YarnSpinner.Tests")]

namespace Yarn.Compiler.Upgrader
{
    /// <summary>
    /// Represents an exception occuring during a language syntax upgrade
    /// operation.
    /// </summary>
    internal class UpgradeException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeException"/> class.
        /// </summary>
        public UpgradeException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeException"/> class.
        /// </summary>
        /// <inheritdoc cref="Exception"/>
        public UpgradeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeException"/> class.
        /// </summary>
        /// <inheritdoc cref="Exception"/>
        public UpgradeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpgradeException"/> class.
        /// </summary>
        /// <inheritdoc cref="Exception"/>
        protected UpgradeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
