namespace Yarn
{
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
        /// <remarks>All types have <see cref="Types.Any"/> as their
        /// ultimate parent type (except for <see cref="Types.Any"/>
        /// itself.)</remarks>
        IType Parent { get; }

        /// <summary>
        /// Gets a more verbose description of this type.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the collection of type information for this type's type members.
        /// </summary>
        System.Collections.Generic.IReadOnlyDictionary<string, ITypeMember> TypeMembers { get; }
    }
}
