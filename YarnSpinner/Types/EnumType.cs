namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// A type that represents enumerations.
    /// </summary>
    internal class EnumType : TypeBase
    {
        private string name;
        private IType parent;
        private string description;

        private List<EnumMember> members = new List<EnumMember>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumType"/> class
        /// that represents the base Enum type. This type has the name
        /// 'Enum', is a subtype of <see cref="BuiltinTypes.Any"/>, and
        /// implements operators common to all Enum subtypes.
        /// </summary>
        public EnumType()
            : base(DefaultMethods)
        {
            this.name = "Enum";
            this.parent = BuiltinTypes.Any;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumType"/> class
        /// that represents a subtype of the base Enum type. This type has
        /// no methods of its own, and is a subtype of <see
        /// cref="BuiltinTypes.Enum"/>.
        /// </summary>
        /// <param name="name">The name of this method.</param>
        /// <param name="description">A string that describes this
        /// method.</param>
        public EnumType(string name, string description)
            : base(null)
        {
            this.name = name;
            this.parent = BuiltinTypes.Enum;
            this.description = description;
        }

        /// <inheritdoc/>
        public override string Name => name;

        /// <inheritdoc/>
        public override IType Parent => parent;

        /// <inheritdoc/>
        public override string Description => description;
        
        /// <summary>
        /// Gets the collection of members that this enumeration type has.
        /// </summary>
        public IEnumerable<EnumMember> Members { get => this.members; }

        private static MethodCollection DefaultMethods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !MethodEqualTo(a, b)) },
        };

        /// <summary>
        /// Adds a new <see cref="EnumMember"/> to this enumeration type.
        /// </summary>
        /// <param name="member">The member to add.</param>
        internal void AddMember(EnumMember member)
        {
            this.members.Add(member);
        }

        private static bool MethodEqualTo(Value a, Value b)
        {
            return a.ConvertTo<int>() == b.ConvertTo<int>();
        }
    }
}
