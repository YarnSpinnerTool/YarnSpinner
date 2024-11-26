namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    public class EnumBase : TypeBase
    {
        public EnumBase() : base(null)
        {
        }

        public override string Name => "Enum";

        public override IType? Parent => Types.Any;

        public override string Description => "Enum";

        internal override IConvertible DefaultValue => throw new InvalidOperationException();
    }

    /// <summary>
    /// A type that represents enumerations.
    /// </summary>
    public class EnumType : TypeBase
    {
        private readonly string name;
        private readonly string description;
        private readonly TypeBase rawType;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnumType"/> class that
        /// represents an enum type. This type has no methods of its own, and is
        /// a subtype of <see cref="Types.Any"/>.
        /// </summary>
        /// <param name="name">The name of this method.</param>
        /// <param name="description">A string that describes this
        /// method.</param>
        /// <param name="rawType"></param>
        public EnumType(string name, string description, TypeBase rawType)
            : base(DefaultMethods)
        {
            this.name = name;
            this.description = description;
            this.rawType = rawType;

            this.AddConvertibleTo(rawType);
        }

        /// <inheritdoc/>
        public override string Name => name;

        /// <inheritdoc/>
        public override IType Parent => Types.Enum;

        /// <inheritdoc/>
        public override string Description => description;

        /// <summary>
        /// Gets the type of this enum's members.
        /// </summary>
        public TypeBase RawType => rawType;

        /// <summary>
        /// Gets the collection of enum cases in this enum.
        /// </summary>
        public IEnumerable<KeyValuePair<string, ConstantTypeProperty>> EnumCases
        {
            get
            {
                foreach (var member in TypeMembers)
                {
                    if (member.Value is ConstantTypeProperty constant)
                    {
                        yield return new KeyValuePair<string, ConstantTypeProperty>(member.Key, constant);
                    }
                }
            }
        }

        private static MethodCollection DefaultMethods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !MethodEqualTo(a, b)) },
        };

        internal override IConvertible DefaultValue
        {
            get
            {
                // The default value for an enum type is the first case found
                // in it. 
                if (this._typeMembers.Count == 0)
                {
                    throw new InvalidOperationException($"Cannot get a default value for enum {Name}, because it has no members (which is not allowed)");
                }
                else
                {
                    var member = System.Linq.Enumerable.First(this._typeMembers).Value;
                    return (member as ConstantTypeProperty)?.Value ?? 0;
                }
            }
        }

        /// <summary>
        /// Adds a new case to this enumeration type.
        /// </summary>
        /// <param name="name">The name of the case.</param>
        /// <param name="member">The member to add for the given name.</param>
        internal void AddMember(string name, ConstantTypeProperty member)
        {
            this._typeMembers.Add(name, member);
        }

        private static bool MethodEqualTo(Value a, Value b)
        {
            if (a.InternalValue is string)
            {
                return a.ConvertTo<string>() == b.ConvertTo<string>();
            }
            else
            {
                return a.ConvertTo<int>() == b.ConvertTo<int>();
            }
        }
    }
}
