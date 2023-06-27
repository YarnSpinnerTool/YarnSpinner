// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// An <see cref="IBridgeableType{T}"/> that bridges to <see
    /// cref="string"/> values.
    /// </summary>
    internal class StringType : TypeBase, IBridgeableType<string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StringType"/>
        /// class.
        /// </summary>
        public StringType()
            : base(StringType.DefaultMethods)
        {
        }

        /// <inheritdoc/>
        public override string Name => "String";

        /// <inheritdoc/>
        public override IType Parent => BuiltinTypes.Any;

        /// <inheritdoc/>
        public override string Description { get; }

        /// <inheritdoc/>
        public string DefaultValue => string.Empty;

        private static MethodCollection DefaultMethods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !MethodEqualTo(a, b)) },
            { Operator.Add.ToString(), TypeUtil.GetMethod(MethodConcatenate) },
        };

        /// <inheritdoc/>
        public string ToBridgedType(Value value)
        {
            return value.ConvertTo<string>();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return base.ToString();
        }

        private static string MethodConcatenate(Value arg1, Value arg2)
        {
            return string.Concat(arg1.ConvertTo<string>(), arg2.ConvertTo<string>());
        }

        private static bool MethodEqualTo(Value a, Value b)
        {
            return a.ConvertTo<string>().Equals(b.ConvertTo<string>());
        }
    }
}
