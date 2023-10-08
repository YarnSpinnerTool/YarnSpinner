using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using Yarn;

namespace TypeChecker
{
    /// <summary>
    /// A type variable represents a type that is not yet known.
    /// </summary>
    public class TypeVariable : IType, IEquatable<TypeVariable>
    {
        /// <summary>
        /// Gets or sets the name of this type variable.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets the parser context associated with this type (that is, the
        /// expression or other parse context whose type is represented by this
        /// type variable.)
        /// </summary>
        public ParserRuleContext Context { get; }


        /// <inheritdoc/>
        public IType? Parent => null;

        /// <inheritdoc/>
        public string Description => $"Type variable representing \"{Name}\"";
        
        /// <summary>
        /// Gets the collection of members belonging to this type.
        /// </summary>
        /// <remarks>
        /// This collection is always empty, because a type variable represents
        /// an unknown type.
        /// </remarks>
        /// 
        public IReadOnlyDictionary<string, ITypeMember> TypeMembers => TypeBase.EmptyTypeMemberDictionary;

        /// <summary>
        /// Initialises a new <see cref="TypeVariable"/>.
        /// </summary>
        /// <param name="name">The name of the type variable.</param>
        /// <param name="context">The parser context that this type variable
        /// represents the type of.</param>
        public TypeVariable(string name, Antlr4.Runtime.ParserRuleContext context)
        {
            Name = name;
            Context = context;
        }
        
        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is IType type && this.Equals(type);
        }
        
        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Context != null) {
                return $@"{Name} ('{Context.GetText()}')";
            } else {
                return Name;
            }
        }
        
        /// <inheritdoc/>
        public bool Equals(IType other)
        {
            return other is TypeVariable otherVariable && this.Equals(otherVariable);
        }
        
        /// <inheritdoc/>
        public bool Equals(TypeVariable other)
        {
            return other.Name == Name;
        }
    }

}
