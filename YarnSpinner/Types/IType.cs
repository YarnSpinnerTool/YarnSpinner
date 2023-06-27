// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// Defines properties that describe a type in the Yarn language.
    /// </summary>
    public interface IType
    {
        /// <summary>
        /// Gets the name of this type.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the parent of this type.
        /// </summary>
        /// <remarks>All types have <see cref="BuiltinTypes.Any"/> as their
        /// ultimate parent type (except for <see cref="BuiltinTypes.Any"/>
        /// itself.)</remarks>
        IType Parent { get; }

        /// <summary>
        /// Gets a more verbose description of this type.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the collection of methods that are available on this type.
        /// </summary>
        MethodCollection Methods { get; }
    }
}
