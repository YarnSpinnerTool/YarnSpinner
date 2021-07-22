namespace Yarn
{
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// Defines properties that describe a type in the Yarn language.
    /// </summary>
    public interface IType
    {
        /// <summary>
        /// The name of this type.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The parent of this type.
        /// </summary>
        /// <remarks>All types have <see cref="BuiltinTypes.Any"/> as their
        /// ultimate parent type (except for <see cref="BuiltinTypes.Any"/>
        /// itself.)</remarks>
        IType Parent { get; }

        /// <summary>
        /// A more verbose description of this type.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The collection of methods that are available on this type.
        /// </summary>
        MethodCollection Methods { get; }
    }
}
