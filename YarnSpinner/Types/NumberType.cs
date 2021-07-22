namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// An <see cref="IBridgeableType{T}"/> that bridges to <see
    /// cref="float"/> values.
    /// </summary>
    internal class NumberType : IBridgeableType<float>
    {
        /// <inheritdoc/>
        public float DefaultValue => default;

        /// <inheritdoc/>
        public string Name => "Number";

        /// <inheritdoc/>
        public IType Parent => BuiltinTypes.Any;

        /// <inheritdoc/>
        public string Description => "Number";

        /// <inheritdoc/>
        public MethodCollection Methods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(this.MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !this.MethodEqualTo(a, b)) },
            { Operator.Add.ToString(), TypeUtil.GetMethod(this.MethodAdd) },
            { Operator.Minus.ToString(), TypeUtil.GetMethod(this.MethodSubtract) },
            { Operator.Divide.ToString(), TypeUtil.GetMethod(this.MethodDivide) },
            { Operator.Multiply.ToString(), TypeUtil.GetMethod(this.MethodMultiply) },
            { Operator.Modulo.ToString(), TypeUtil.GetMethod(this.MethodModulus) },
            { Operator.UnaryMinus.ToString(), TypeUtil.GetMethod(this.MethodUnaryMinus) },
            { Operator.GreaterThan.ToString(), TypeUtil.GetMethod(this.MethodGreaterThan) },
            { Operator.GreaterThanOrEqualTo.ToString(), TypeUtil.GetMethod(this.MethodGreaterThanOrEqualTo) },
            { Operator.LessThan.ToString(), TypeUtil.GetMethod(this.MethodLessThan) },
            { Operator.LessThanOrEqualTo.ToString(), TypeUtil.GetMethod(this.MethodLessThanOrEqualTo) },
        };

        /// <inheritdoc/>
        public float ToBridgedType(Value value)
        {
            throw new System.NotImplementedException();
        }

        private bool MethodEqualTo(Value a, Value b)
        {
            return a.ConvertTo<float>() == b.ConvertTo<float>();
        }

        private float MethodAdd(Value a, Value b)
        {
            return a.ConvertTo<float>() + b.ConvertTo<float>();
        }

        private float MethodSubtract(Value a, Value b)
        {
            return a.ConvertTo<float>() - b.ConvertTo<float>();
        }

        private float MethodDivide(Value a, Value b)
        {
            return a.ConvertTo<float>() / b.ConvertTo<float>();
        }

        private float MethodMultiply(Value a, Value b)
        {
            return a.ConvertTo<float>() * b.ConvertTo<float>();
        }

        private int MethodModulus(Value a, Value b)
        {
            return a.ConvertTo<int>() % b.ConvertTo<int>();
        }

        private float MethodUnaryMinus(Value a)
        {
            return -a.ConvertTo<float>();
        }

        private bool MethodGreaterThan(Value a, Value b)
        {
            return a.ConvertTo<float>() > b.ConvertTo<float>();
        }

        private bool MethodGreaterThanOrEqualTo(Value a, Value b)
        {
            return a.ConvertTo<float>() >= b.ConvertTo<float>();
        }

        private bool MethodLessThan(Value a, Value b)
        {
            return a.ConvertTo<float>() < b.ConvertTo<float>();
        }

        private bool MethodLessThanOrEqualTo(Value a, Value b)
        {
            return a.ConvertTo<float>() <= b.ConvertTo<float>();
        }
    }
}
