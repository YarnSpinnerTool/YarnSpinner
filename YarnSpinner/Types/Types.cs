// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#pragma warning disable CA1720 // Identifier name contains type name

namespace Yarn
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains the built-in types available in the Yarn language.
    /// </summary>
    public static class Types
    {
        /// <summary>Gets the type representing strings.</summary>
        public static IType String { get; } = new StringType();

        /// <summary>Gets the type representing numbers.</summary>
        public static IType Number { get; } = new NumberType();

        /// <summary>Gets the type representing boolean values.</summary>
        public static IType Boolean { get; } = new BooleanType();

        /// <summary>Gets the type representing any value.</summary>
        public static IType Any { get; } = new AnyType(null);

        /// <summary>Gets the type representing a typing error.</summary>
        internal static IType Error { get; } = new ErrorType();

        /// <summary>
        /// Gets the collection of all built-in types.
        /// </summary>
        internal static IEnumerable<IType> AllBuiltinTypes => _allTypes;

        private static List<IType> _allTypes = new List<IType> { String, Number, Boolean, Any, Error };

        /// <summary>
        /// Gets a dictionary that maps CLR types to their corresponding
        /// Yarn types.
        /// </summary>
        public static IReadOnlyDictionary<System.Type, Yarn.IType> TypeMappings { get; } = new Dictionary<System.Type, Yarn.IType>
        {
            { typeof(string), Types.String },
            { typeof(bool),   Types.Boolean },
            { typeof(int),    Types.Number },
            { typeof(float),  Types.Number },
            { typeof(double), Types.Number },
            { typeof(sbyte),  Types.Number },
            { typeof(byte),   Types.Number },
            { typeof(short),  Types.Number },
            { typeof(ushort), Types.Number },
            { typeof(uint),   Types.Number },
            { typeof(long),   Types.Number },
            { typeof(ulong),  Types.Number },
            { typeof(decimal),Types.Number },
            { typeof(object), Types.Any },
        };
    }
}
