using System.Collections.Generic;

namespace TypeChecker
{

    /// <summary>
    /// Contains the definitions for base types, like Number and String, and
    /// provides methods for registering new types.
    /// </summary>/
    public static class Types
    {
        /// <summary>
        /// The Any type. All types, besides Any itself, are subtypes of Any.
        /// </summary>
        public static readonly TypeLiteral Any = RegisterType("Any", null);

        /// <summary>
        /// The Error type. Represents a type that could not be resolved.
        /// </summary>
        public static readonly TypeLiteral Error = RegisterType("<ERROR>", null);

        /// <summary>
        /// A type that represents numeric values.
        /// </summary>
        public static readonly TypeLiteral Number = RegisterType("Number", Any);

        /// <summary>
        /// A type that represents boolean values.
        /// </summary>
        public static readonly TypeLiteral Bool = RegisterType("Bool", Any);

        /// <summary>
        /// A type that represents string values.
        /// </summary>
        public static readonly TypeLiteral String = RegisterType("String", Any);

        /// <summary>
        /// Gets all registered types.
        /// </summary>
        public static IEnumerable<TypeLiteral> AllTypes => allTypes;

        private static List<TypeLiteral> allTypes;

        static Types()
        {
            if (allTypes == null)
            {
                allTypes = new List<TypeLiteral>();
            }
        }

        /// <summary>
        /// Registers a new type.
        /// </summary>
        /// <param name="name">The name of the new type.</param>
        /// <param name="parent">The parent of the type. If <see langword="null"/>,
        /// <see cref="Types.Any"/> is used.</param>
        /// <returns>The newly registered type.</returns>
        public static TypeLiteral RegisterType(string name, TypeLiteral parent = null)
        {
            if (allTypes == null) {
                allTypes = new List<TypeLiteral>();
            }

            var t = new TypeLiteral(name, parent);
            allTypes.Add(t);
            return t;
        }
    }


}
