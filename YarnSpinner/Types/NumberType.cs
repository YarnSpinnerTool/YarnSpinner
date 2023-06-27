// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System.Collections.Generic;
    using MethodCollection = System.Collections.Generic.IReadOnlyDictionary<string, System.Delegate>;

    /// <summary>
    /// An <see cref="IBridgeableType{T}"/> that bridges to <see
    /// cref="float"/> values.
    /// </summary>
    internal class NumberType : TypeBase, IBridgeableType<float>
    {
        /// <inheritdoc/>
        public float DefaultValue => default;

        /// <inheritdoc/>
        public override string Name => "Number";

        /// <inheritdoc/>
        public override IType Parent => BuiltinTypes.Any;

        /// <inheritdoc/>
        public override string Description => "Number";

        private static MethodCollection DefaultMethods => new Dictionary<string, System.Delegate>
        {
            { Operator.EqualTo.ToString(), TypeUtil.GetMethod(MethodEqualTo) },
            { Operator.NotEqualTo.ToString(), TypeUtil.GetMethod((a, b) => !MethodEqualTo(a, b)) },
            { Operator.Add.ToString(), TypeUtil.GetMethod(MethodAdd) },
            { Operator.Minus.ToString(), TypeUtil.GetMethod(MethodSubtract) },
            { Operator.Divide.ToString(), TypeUtil.GetMethod(MethodDivide) },
            { Operator.Multiply.ToString(), TypeUtil.GetMethod(MethodMultiply) },
            { Operator.Modulo.ToString(), TypeUtil.GetMethod(MethodModulus) },
            { Operator.UnaryMinus.ToString(), TypeUtil.GetMethod(MethodUnaryMinus) },
            { Operator.GreaterThan.ToString(), TypeUtil.GetMethod(MethodGreaterThan) },
            { Operator.GreaterThanOrEqualTo.ToString(), TypeUtil.GetMethod(MethodGreaterThanOrEqualTo) },
            { Operator.LessThan.ToString(), TypeUtil.GetMethod(MethodLessThan) },
            { Operator.LessThanOrEqualTo.ToString(), TypeUtil.GetMethod(MethodLessThanOrEqualTo) },
        };

        public NumberType() : base(NumberType.DefaultMethods) {}

        /// <inheritdoc/>
        public float ToBridgedType(Value value)
        {
            throw new System.NotImplementedException();
        }

        private static bool MethodEqualTo(Value a, Value b)
        {
            return a.ConvertTo<float>() == b.ConvertTo<float>();
        }

        private static float MethodAdd(Value a, Value b)
        {
            return a.ConvertTo<float>() + b.ConvertTo<float>();
        }

        private static float MethodSubtract(Value a, Value b)
        {
            return a.ConvertTo<float>() - b.ConvertTo<float>();
        }

        private static float MethodDivide(Value a, Value b)
        {
            return a.ConvertTo<float>() / b.ConvertTo<float>();
        }

        private static float MethodMultiply(Value a, Value b)
        {
            return a.ConvertTo<float>() * b.ConvertTo<float>();
        }

        private static int MethodModulus(Value a, Value b)
        {
            return a.ConvertTo<int>() % b.ConvertTo<int>();
        }

        private static float MethodUnaryMinus(Value a)
        {
            return -a.ConvertTo<float>();
        }

        private static bool MethodGreaterThan(Value a, Value b)
        {
            return a.ConvertTo<float>() > b.ConvertTo<float>();
        }

        private static bool MethodGreaterThanOrEqualTo(Value a, Value b)
        {
            return a.ConvertTo<float>() >= b.ConvertTo<float>();
        }

        private static bool MethodLessThan(Value a, Value b)
        {
            return a.ConvertTo<float>() < b.ConvertTo<float>();
        }

        private static bool MethodLessThanOrEqualTo(Value a, Value b)
        {
            return a.ConvertTo<float>() <= b.ConvertTo<float>();
        }
    }
}
