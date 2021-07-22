namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// An <see cref="IBridgeableType{T}"/> that bridges to <see
    /// cref="string"/> values.
    /// </summary>
    internal class StringType : IBridgeableType<string>
    {
        /// <inheritdoc/>
        public string Name => "String";

        /// <inheritdoc/>
        public IType Parent => BuiltinTypes.Any;

        /// <inheritdoc/>
        public string Description { get; }

        /// <inheritdoc/>
        public string DefaultValue => string.Empty;

        /// <inheritdoc/>
        public MethodCollection Methods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(this.MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !this.MethodEqualTo(a, b)) },
            { Operator.Add.ToString(), TypeUtil.GetMethod(this.MethodConcatenate) },
        };

        /// <inheritdoc/>
        public string ToBridgedType(Value value)
        {
            return value.ConvertTo<string>();
        }

        private string MethodConcatenate(Value arg1, Value arg2)
        {
            return string.Concat(arg1.ConvertTo<string>(), arg2.ConvertTo<string>());
        }

        private bool MethodEqualTo(Value a, Value b)
        {
            return a.ConvertTo<string>().Equals(b.ConvertTo<string>());
        }

    }
}
