// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;

    /// <summary>
    /// Contains the built-in types available in the Yarn language.
    /// </summary>
    public static class BuiltinTypes
    {
        /// <summary>An undefined type.</summary>
        /// <remarks>This value is not valid except during compilation. It
        /// is used to represent values that have not yet been assigned a
        /// type by the type system.</remarks>
        internal const IType Undefined = null;

        /// <summary>Gets the type representing strings.</summary>
        #pragma warning disable CA1720
        public static IType String { get; } = new StringType();
        #pragma warning restore CA1720

        /// <summary>Gets the type representing numbers.</summary>
        public static IType Number { get; } = new NumberType();

        /// <summary>Gets the type representing boolean values.</summary>
        public static IType Boolean { get; } = new BooleanType();

        /// <summary>Gets the type representing any value.</summary>
        public static IType Any { get; } = new AnyType();

        /// <summary>
        /// Gets a dictionary that maps CLR types to their corresponding
        /// Yarn types.
        /// </summary>
        public static IReadOnlyDictionary<System.Type, Yarn.IType> TypeMappings { get; } = new Dictionary<System.Type, Yarn.IType>
        {
            { typeof(string), BuiltinTypes.String },
            { typeof(bool), BuiltinTypes.Boolean },
            { typeof(int), BuiltinTypes.Number },
            { typeof(float), BuiltinTypes.Number },
            { typeof(double), BuiltinTypes.Number },
            { typeof(sbyte), BuiltinTypes.Number },
            { typeof(byte), BuiltinTypes.Number },
            { typeof(short), BuiltinTypes.Number },
            { typeof(ushort), BuiltinTypes.Number },
            { typeof(uint), BuiltinTypes.Number },
            { typeof(long), BuiltinTypes.Number },
            { typeof(ulong), BuiltinTypes.Number },
            { typeof(decimal), BuiltinTypes.Number },
            { typeof(object), BuiltinTypes.Any },
        };

        /// <summary>
        /// Gets a <see cref="IEnumerable{T}"/> containing all built-in
        /// properties defined in this class.
        /// </summary>
        /// <value>The list of built-in type objects.</value>
        internal static IEnumerable<IType> AllBuiltinTypes
        {
            get
            {
                // Find all static properties of BuiltinTypes that are
                // public
                var propertyInfos = typeof(BuiltinTypes)
                    .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                List<IType> result = new List<IType>();

                foreach (var propertyInfo in propertyInfos)
                {
                    // If the type of this property is IType, then this is
                    // a built-in type!
                    if (propertyInfo.PropertyType == typeof(IType)) {
                        // Get that value.
                        var builtinType = (IType)propertyInfo.GetValue(null);

                        // If it's not null (i.e. the undefined type), then
                        // add it to the type objects we're returning!
                        if (builtinType != null) {
                            result.Add(builtinType);
                        }
                    }
                }

                return result;
            }
        }
    }
}
