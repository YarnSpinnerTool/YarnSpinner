namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// An <see cref="IBridgeableType{T}"/> that bridges to <see
    /// cref="bool"/> values.
    /// </summary>
    internal class BooleanType : IBridgeableType<bool>
    {
        /// <inheritdoc/>
        public bool DefaultValue => default;

        /// <inheritdoc/>
        public string Name => "Bool";

        /// <inheritdoc/>
        public IType Parent => BuiltinTypes.Any;

        /// <inheritdoc/>
        public string Description => "Bool";

        /// <inheritdoc/>
        public MethodCollection Methods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(this.MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !this.MethodEqualTo(a, b)) },
            { Operator.And.ToString(), TypeUtil.GetMethod(this.MethodAnd) },
            { Operator.Or.ToString(), TypeUtil.GetMethod(this.MethodOr) },
            { Operator.Xor.ToString(), TypeUtil.GetMethod(this.MethodXor) },
            { Operator.Not.ToString(), TypeUtil.GetMethod(this.MethodNot) },
        };

        private bool MethodEqualTo(Value a, Value b)
        {
            return a.ConvertTo<bool>() == b.ConvertTo<bool>();
        }

        private bool MethodAnd(Value a, Value b)
        {
            return a.ConvertTo<bool>() && b.ConvertTo<bool>();
        }

        private bool MethodOr(Value a, Value b)
        {
            return a.ConvertTo<bool>() || b.ConvertTo<bool>();
        }

        private bool MethodXor(Value a, Value b)
        {
            return a.ConvertTo<bool>() ^ b.ConvertTo<bool>();
        }

        private bool MethodNot(Value a)
        {
            return !a.ConvertTo<bool>();
        }

        public bool ToBridgedType(Value value)
        {
            throw new System.NotImplementedException();
        }
    }
}
