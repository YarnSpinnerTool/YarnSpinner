
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Yarn
{
    /// <summary>
    /// A value from inside Yarn.
    /// </summary>
    public class Value : IComparable, IComparable<Value>
    {


        public static readonly new Dictionary<System.Type, Value.Type> TypeMappings = new Dictionary<System.Type, Value.Type>
        {
            { typeof(string), Type.String },
            { typeof(bool), Type.Bool },
            { typeof(int), Type.Number },
            { typeof(float), Type.Number },
            { typeof(double), Type.Number },
            { typeof(sbyte), Type.Number },
            { typeof(byte), Type.Number },
            { typeof(short), Type.Number },
            { typeof(ushort), Type.Number },
            { typeof(uint), Type.Number },
            { typeof(long), Type.Number },
            { typeof(ulong), Type.Number },
            { typeof(decimal), Type.Number },
        };

        /// <summary>
        /// The type of a <see cref="Value"/>.
        /// </summary>
        public enum Type
        {
            /// <summary>A number.</summary>
            Number,

#pragma warning disable CA1720 // Identifier contains type name
            /// <summary>A string.</summary>
            String,
#pragma warning restore CA1720 // Identifier contains type name

            /// <summary>A boolean value.</summary>
            Bool,

            /// <summary>A value of undefined type.</summary>
            Undefined,

        }

        /// <summary>
        /// Gets the underlying type of this value.
        /// </summary>
        /// <remarks>
        /// Yarn values of one underlying type can always been converted to
        /// other types. This property allows you to access the actual type of value
        /// that this value contains.
        /// </remarks>
        public Value.Type type { get; internal set; }

        // The underlying values for this object
        private float NumberValue { get; set; }

        private string StringValue { get; set; }

        private bool BoolValue { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Value"/> class,
        /// using the specified object.
        /// </summary>
        /// <remarks>
        /// If the `value` is another <see cref="Value"/>, its contents
        /// will be copied into the new instance.
        /// </remarks>
        /// <throws cref="ArgumentException">
        /// Thrown when the `value` is not a <see cref="Value"/>, string,
        /// int, float, double, bool, or null.
        /// </throws>
        /// <param name="value">The value that this <see cref="Value"/>
        /// should contain.</param>
        public Value(Value value)
        {
            this.ConstructFromValue(value);
        }

        public Value(object obj)
        {
            var incomingType = obj.GetType();

            if (incomingType == typeof(Yarn.Value))
            {
                this.ConstructFromValue((Value)obj);
                return;
            }

            if (TypeMappings.ContainsKey(incomingType) == false)
            {
                throw new InvalidCastException($"Cannot create a {nameof(Value)} with a value of type {incomingType}");
            }

            // Decide our type based on the incoming type
            type = TypeMappings[incomingType];

            switch (type)
            {
                case Type.Number:
                    NumberValue = Convert.ToSingle(obj);
                    return;
                case Type.Bool:
                    BoolValue = Convert.ToBoolean(obj);
                    return;
                case Type.String:
                    StringValue = Convert.ToString(obj);
                    return;
                default:
                    throw new InvalidOperationException($"Invalid destination type {type}");
            }


        }

        private void ConstructFromValue(Value value)
        {
            this.type = value.type;
            switch (type)
            {
                case Type.Number:
                    this.NumberValue = value.NumberValue;
                    break;
                case Type.String:
                    this.StringValue = value.StringValue;
                    break;
                case Type.Bool:
                    this.BoolValue = value.BoolValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Compares this <see cref="Value"/> to another object. The other object must either be another instance of <see cref="Value"/>, or `null`.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>Returns the same results as <see cref="CompareTo(Value)"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when `obj` is not a <see cref="Value"/>.</exception>
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

        public int CompareTo(Value other)
        {
            if (type != other.type)
            {
                throw new ArgumentException($"Cannot compare values of differing types {type} and {other.type}");
            }

            switch (type)
            {
                case Type.Number:
                    return this.NumberValue.CompareTo(other.NumberValue);
                case Type.String:
                    return this.StringValue.CompareTo(other.NumberValue);
                case Type.Bool:
                    return this.BoolValue.CompareTo(other.NumberValue);
                default:
                    throw new ArgumentException($"Cannot compare values of type {type}");
            }
        }

        public T ConvertTo<T>()
            where T : IConvertible
        {
            System.Type targetType = typeof(T);

            return (T)this.ConvertTo(targetType);
        }

        public object ConvertTo(System.Type targetType)
        {
            if (targetType == typeof(Yarn.Value)) {
                return this;
            }
            
            if (TypeMappings.ContainsKey(targetType) == false)
            {
                throw new InvalidOperationException($"{nameof(Value)} instances cannot be converted to {targetType}.");
            }

            var compatibleYarnType = TypeMappings[targetType];

            switch (this.type)
            {
                case Type.Number:
                    return Convert.ChangeType(this.NumberValue, targetType);
                case Type.String:
                    return Convert.ChangeType(this.StringValue, targetType);
                case Type.Bool:
                    return Convert.ChangeType(this.BoolValue, targetType);
                default:
                    throw new InvalidOperationException($"Invalid type for conversion {this.type}");
            }
        }

        /// <summary>
        /// Compares to see if this <see cref="Value"/> is the same value
        /// as another.
        /// </summary>
        /// <remarks>
        /// This method returns <see langword="true"/> if this instance has
        /// the same type as <paramref name="obj"/>, and their
        /// corresponding backing values are the same value.
        /// </remarks>
        /// <param name="obj">The other <see cref="Value"/> to compare
        /// against.</param>
        /// <returns><see langword="true"/> if the objects represent the
        /// same value, `false` otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType())
            {
                return false;
            }

            var other = (Value)obj;

            if (this.type != other.type)
            {
                throw new ArgumentException($"Cannot convert between values of different types: {this} and {other}");
            }

            switch (this.type)
            {
                case Type.Number:
                    return this.NumberValue == other.NumberValue;
                case Type.String:
                    return string.Compare(this.StringValue, other.StringValue) == 0;
                case Type.Bool:
                    return this.BoolValue == other.BoolValue;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown value type {this.type}");
            }

        }

        // override object.GetHashCode
        /// <summary>
        /// Returns the hash code for this value.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            object backing;
            switch (this.type)
            {
                case Type.Number:
                    backing = this.NumberValue;
                    break;
                case Type.String:
                    backing = this.StringValue;
                    break;
                case Type.Bool:
                    backing = this.BoolValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Cannot get hash code for value of type {type}");
            }

            if (backing != null)
            {
                return backing.GetHashCode();
            }

            return 0;
        }

        /// <summary>
        /// Converts this value to a string.
        /// </summary>
        /// <returns>The string representation of this value</returns>
        public override string ToString()
        {

            return string.Format(CultureInfo.CurrentCulture,
                "[Value: type={0}, value={1}]",
                type,
                this.ConvertTo<string>());
        }

        /// <summary>
        /// Adds two values together.
        /// </summary>
        /// <remarks>
        /// The specific method by which two values of different types are
        /// added together depends upon the type of each of the values.
        /// </remarks>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>A new <see cref="Value"/>, containing the result of
        /// adding the two values together.</returns>
        /// <throws cref="ArgumentException">Thrown when the two values
        /// cannot be added together.</throws>
        public static Value operator +(Value a, Value b)
        {

            if (a.type != b.type)
            {
                throw new ArgumentException($"Cannot equate {a.type} and {b.type}: must be of the same type");
            }

            if (a.type == Type.String)
            {
                // we're headed for string town!
                return new Value(a.StringValue + b.StringValue);
            }

            if (a.type == Type.Number)
            {
                return new Value(a.NumberValue + b.NumberValue);
            }

            throw new ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "Cannot add types {0} and {1}.", a.type, b.type)
            );
        }

        /// <summary>
        /// Subtracts two values from each other.
        /// </summary>
        /// <remarks>
        /// Both values must be either a number or `null`.
        /// </remarks>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>A new <see cref="Value"/>, containing the result of
        /// subtracting the two values from each other.</returns>
        /// <throws cref="ArgumentException">Thrown when the two values
        /// cannot be subtracted from each other together.</throws>
        public static Value operator -(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot subtract types {0} and {1}.", a.type, b.type));
            }

            return new Value(a.NumberValue - b.NumberValue);
        }

        /// <summary>
        /// Multiplies two values together.
        /// </summary>
        /// <remarks>
        /// Both values must be either a number or `null`.
        /// </remarks>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>A new <see cref="Value"/>, containing the result of
        /// multiplying the two values together.</returns>
        /// <throws cref="ArgumentException">Thrown when the two values
        /// cannot be multiplied together.</throws>
        public static Value operator *(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot multiply types {0} and {1}.", a.type, b.type));
            }

            return new Value(a.NumberValue * b.NumberValue);
        }

        /// <summary>
        /// Divides two values.
        /// </summary>
        /// <remarks>
        /// Both values must be either a number or `null`.
        /// </remarks>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>A new <see cref="Value"/>, containing the result of
        /// dividing two values.</returns>
        /// <throws cref="ArgumentException">Thrown when the two values
        /// cannot be divided.</throws>
        public static Value operator /(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot divide types {0} and {1}.", a.type, b.type));
            }

            return new Value(a.NumberValue / b.NumberValue);
        }

        /// <summary>
        /// Calculates the remainder when dividing two values.
        /// </summary>
        /// <remarks>
        /// Both values must be either a number or `null`.
        /// </remarks>        
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>A new <see cref="Value"/>, containing the remainder of
        /// dividing two values .</returns>
        /// <throws cref="ArgumentException">Thrown when the two values
        /// cannot be divided.</throws>
        public static Value operator %(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot modulo types {0} and {1}.", a.type, b.type));
            }

            return new Value(a.NumberValue % b.NumberValue);
        }

        /// <summary>
        /// Computes the negative of a value.
        /// </summary>
        /// <remarks>
        /// If the value is a number, the negative of that number is
        /// returned.
        /// </remarks>        
        /// <param name="a">The first value.</param>
        /// <returns>A new <see cref="Value"/>, containing the negative of
        /// this <see cref="Value"/>.</returns>
        /// <throws cref="ArgumentException">Thrown when <paramref
        /// name="a"/> is not a <see cref="Number"/>.</throws>
        public static Value operator -(Value a)
        {
            if (a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot take the negative of type {0}.", a.type));
            }

            return new Value(-a.NumberValue);
        }

        /// <summary>
        /// Compares two values, and returns <see langword="true"/> if the first is greater
        /// than the second.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is greater than
        /// <paramref name="b"/>, false otherwise.</returns>
        public static bool operator >(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot compare types {0} and {1}.", a.type, b.type));
            }

            return a.NumberValue > b.NumberValue;
        }

        /// <summary>
        /// Compares two values, and returns <see langword="true"/> if the first is less
        /// than the second.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is less than <paramref
        /// name="b"/>, false otherwise.</returns>
        public static bool operator <(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot compare types {0} and {1}.", a.type, b.type));
            }

            return a.NumberValue < b.NumberValue;
        }

        /// <summary>
        /// Compares two values, and returns <see langword="true"/> if the first is greater
        /// than or equal to the second.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is greater than or
        /// equal to <paramref name="b"/>, false otherwise.</returns>
        public static bool operator >=(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot compare types {0} and {1}.", a.type, b.type));
            }

            return a.NumberValue >= b.NumberValue;
        }

        /// <summary>
        /// Compares two values, and returns <see langword="true"/> if the first is less
        /// than or equal to the second.
        /// </summary>
        /// <param name="a">The first value.</param>
        /// /// <param name="b">The second value.</param>
        /// <returns><see langword="true"/> if <paramref name="a"/> is kess than or equal
        /// to <paramref name="b"/>, false otherwise.</returns>
        public static bool operator <=(Value a, Value b)
        {
            if (a.type != b.type || a.type != Type.Number)
            {
                throw new System.ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, "Cannot compare types {0} and {1}.", a.type, b.type));
            }

            return a.NumberValue <= b.NumberValue;
        }
    }
}
