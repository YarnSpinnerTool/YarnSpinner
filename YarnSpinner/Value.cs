// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Represents a read-only value in the Yarn Spinner virtual machine.
    /// </summary>
    public interface IYarnValue
    {
        /// <summary>
        /// Converts this <see cref="IYarnValue"/> to type <typeparamref
        /// name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to convert to.</typeparam>
        /// <returns>A value of type T.</returns>
        public T ConvertTo<T>() where T : IConvertible;
    }

    /// <summary>
    /// A value from inside Yarn.
    /// </summary>
    internal partial class Value : IYarnValue
    {
        public Yarn.IType Type { get; internal set; }

        internal IConvertible InternalValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Value"/> class,
        /// using the specified object.
        /// </summary>
        /// <remarks>
        /// If the <c>value</c> is another <see cref="Value"/>, its contents
        /// will be copied into the new instance.
        /// </remarks>
        /// <throws cref="ArgumentException">
        /// Thrown when the <c>value</c> is not a <see cref="Value"/>, string,
        /// int, float, double, bool, or null.
        /// </throws>
        /// <param name="value">The value that this <see cref="Value"/>
        /// should contain.</param>
        public Value(Value value)
        {
            this.Type = value.Type;
            this.InternalValue = value.InternalValue;
        }

        public Value(IType type, IConvertible internalValue)
        {
            this.Type = type;
            this.InternalValue = internalValue;
        }

        /// <summary>
        /// Compares this <see cref="Value"/> to another object. The other object must either be another instance of <see cref="Value"/>, or <c>null</c>.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>Returns the same results as <see cref="IComparable.CompareTo"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when <c>obj</c> is not a <see cref="Value"/>.</exception>
        public int CompareTo(object obj)
        {
            // not a value
            if (!(obj is Value other))
            {
                throw new ArgumentException("Object is not a Value");
            }

            // it is a value!
            return ((IComparable<Value>)this).CompareTo(other);
        }

        public T ConvertTo<T>()
            where T : IConvertible
        {
            System.Type targetType = typeof(T);

            return (T)this.ConvertTo(targetType);
        }

        public object ConvertTo(System.Type targetType)
        {
            if (targetType == typeof(Yarn.Value))
            {
                return this;
            }

            return Convert.ChangeType(this.InternalValue, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts this value to a string.
        /// </summary>
        /// <returns>The string representation of this value</returns>
        public override string ToString()
        {
            return string.Format(CultureInfo.CurrentCulture,
                "[Value: type={0}, value={1}]",
                this.Type.Name,
                this.ConvertTo<string>());
        }

        public override int GetHashCode()
        {
            return InternalValue.GetHashCode();
        }

        public static implicit operator Value(string str)
        {
            return new Value(Types.String, str);
        }
        public static implicit operator Value(float f)
        {
            return new Value(Types.Number, f);
        }
        public static implicit operator Value(double d)
        {
            return new Value(Types.Number, d);
        }
        public static implicit operator Value(int i)
        {
            return new Value(Types.Number, i);
        }
        public static implicit operator Value(bool b)
        {
            return new Value(Types.Boolean, b);
        }
        public static Value From(IConvertible convertible)
        {
            if (Types.TypeMappings.TryGetValue(convertible.GetType(), out var type))
            {
                return new Value(type, convertible);
            }
            else
            {
                throw new ArgumentException($"Can't convert value of type {convertible} to a Yarn value");
            }
        }
    }
}
