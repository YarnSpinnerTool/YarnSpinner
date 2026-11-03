using System.Collections.Generic;

namespace Yarn
{
    /// <summary>
    /// A forward-declared type represents a type that is not known ahead of compilation type - that is, it's a type that's declared in the Yarn Spinner script itself. The compiler will resolve it during compilation, or fail with an error if it can't be resolved.
    /// </summary>
    internal class ForwardDeclaredType : IType
    {
        public ForwardDeclaredType(string Name)
        {
            this.Name = Name;
        }

        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public IType? Parent => Types.Any;

        /// <inheritdoc/>
        public string Description => $"Forward-declared type '{Name}'";

        /// <inheritdoc/>
        public IReadOnlyDictionary<string, ITypeMember> TypeMembers => new Dictionary<string, ITypeMember>();
    }
}
