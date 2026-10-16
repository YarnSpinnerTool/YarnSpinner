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
    public class Value : IYarnValue, IConvertible
    {
        /// <summary>
        /// The type of this value.
        /// Because Yarn has a much simpler list of supported types (numbers, bools, strings) and internally Yarn values are IConvertible without having an associated type we wouldn't know how to handle the value
        /// </summary>
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
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            this.Type = value.Type;
            this.InternalValue = value.InternalValue;
        }
        
        /// <inheritdoc cref="Value(Value)" />
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

        /// <inheritdoc cref="ConvertTo(Type)" />
        public T ConvertTo<T>() where T : IConvertible
        {
            System.Type targetType = typeof(T);

            return (T)this.ConvertTo(targetType);
        }

        /// <summary>
        /// Converts a value from it's current form to the requested type
        /// </summary>
        /// <param name="targetType">The new type</param>
        /// <returns>the value now as a converted object</returns>
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

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return InternalValue.GetHashCode();
        }
        
        /// <summary>
        /// Implictly creates a Value from a string
        /// </summary>
        public static implicit operator Value(string str)
        {
            return new Value(Types.String, str);
        }
        /// <summary>
        /// Implictly creates a Value from a float
        /// </summary>
        public static implicit operator Value(float f)
        {
            return new Value(Types.Number, f);
        }
        /// <summary>
        /// Implictly creates a Value from a double
        /// </summary>
        public static implicit operator Value(double d)
        {
            return new Value(Types.Number, d);
        }
        /// <summary>
        /// Implictly creates a Value from a int
        /// </summary>
        public static implicit operator Value(int i)
        {
            return new Value(Types.Number, i);
        }
        /// <summary>
        /// Implictly creates a Value from a bool
        /// </summary>
        public static implicit operator Value(bool b)
        {
            return new Value(Types.Boolean, b);
        }

        /// <summary>
        /// Creates a new value from any IConvertible
        /// </summary>
        /// <param name="convertible">The IConvertible desired to be converted into a Yarn value</param>
        /// <returns>The Yarn value</returns>
        public static Value From(IConvertible convertible)
        {
            if (convertible is null)
            {
                throw new ArgumentNullException(nameof(convertible));
            }

            if (Types.TypeMappings.TryGetValue(convertible.GetType(), out var type))
            {
                return new Value(type, convertible);
            }
            else
            {
                throw new ArgumentException($"Can't convert value of type {convertible} to a Yarn value");
            }
        }

        /// <inheritdoc />
        public TypeCode GetTypeCode()
        {
            return this.InternalValue.GetTypeCode();
        }
        /// <inheritdoc />
        public bool ToBoolean(IFormatProvider provider)
        {
            return this.InternalValue.ToBoolean(provider);
        }
        /// <inheritdoc />
        public byte ToByte(IFormatProvider provider)
        {
            return this.InternalValue.ToByte(provider);
        }
        /// <inheritdoc />
        public char ToChar(IFormatProvider provider)
        {
            return this.InternalValue.ToChar(provider);
        }
        /// <inheritdoc />
        public DateTime ToDateTime(IFormatProvider provider)
        {
            return this.InternalValue.ToDateTime(provider);
        }
        /// <inheritdoc />
        public decimal ToDecimal(IFormatProvider provider)
        {
            return this.InternalValue.ToDecimal(provider);
        }
        /// <inheritdoc />
        public double ToDouble(IFormatProvider provider)
        {
            return this.InternalValue.ToDouble(provider);
        }
        /// <inheritdoc />
        public short ToInt16(IFormatProvider provider)
        {
            return this.InternalValue.ToInt16(provider);
        }
        /// <inheritdoc />
        public int ToInt32(IFormatProvider provider)
        {
            return this.InternalValue.ToInt32(provider);
        }
        /// <inheritdoc />
        public long ToInt64(IFormatProvider provider)
        {
            return this.InternalValue.ToInt64(provider);
        }
        /// <inheritdoc />
        public sbyte ToSByte(IFormatProvider provider)
        {
            return this.InternalValue.ToSByte(provider);
        }
        /// <inheritdoc />
        public float ToSingle(IFormatProvider provider)
        {
            return this.InternalValue.ToSingle(provider);
        }
        /// <inheritdoc />
        public string ToString(IFormatProvider provider)
        {
            return this.InternalValue.ToString(provider);
        }
        /// <inheritdoc />
        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return this.InternalValue.ToType(conversionType, provider);
        }
        /// <inheritdoc />
        public ushort ToUInt16(IFormatProvider provider)
        {
            return this.InternalValue.ToUInt16(provider);
        }
        /// <inheritdoc />
        public uint ToUInt32(IFormatProvider provider)
        {
            return this.InternalValue.ToUInt32(provider);
        }
        /// <inheritdoc />
        public ulong ToUInt64(IFormatProvider provider)
        {
            return this.InternalValue.ToUInt64(provider);
        }
    }
}
