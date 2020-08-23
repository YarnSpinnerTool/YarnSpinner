
using System;
using System.Globalization;

namespace Yarn
{
    /// <summary>
    /// A value from inside Yarn.
    /// </summary>
    public class Value : IComparable, IComparable<Value> {
        private static readonly System.Type[] NumberTypes = new System.Type[] 
        {
            typeof(sbyte),
            typeof(byte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
        };
        
        /// <summary>
        /// The shared Null value.
        /// </summary>
        public static readonly Value NULL = new Value();

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

            /// <summary>The null value.</summary>
            Null,

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

        private object BackingValue
        {
            get
            {
                switch (this.type)
                {
                    case Type.Null: return null;
                    case Type.String: return this.StringValue;
                    case Type.Number: return this.NumberValue;
                    case Type.Bool: return this.BoolValue;
                }
                throw new InvalidOperationException(
                    string.Format(CultureInfo.CurrentCulture, "Can't get good backing type for {0}", this.type)
                );
            }
        }

        /// <summary>
        /// Gets the numeric representation of this value.
        /// </summary>
        /// <remarks>
        /// This method will attempt to convert the value to a number, if
        /// it isn't already. The conversion is done in the following ways:
        ///
        /// * If the value is a string, the value attempts to parse it as a
        /// number and returns that; if this fails, 0 is returned.
        ///
        /// * If the value is a boolean, it will return 1 if `true`, and 0 if `false`.
        ///
        /// * If the value is `null`, it will return `0`.
        ///
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the
        /// underlying value cannot be converted to a <see
        /// cref="float"/>.</exception>
        public float AsNumber
        {
            get
            {
                switch (this.type)
                {
                    case Type.Number:
                        return this.NumberValue;
                    case Type.String:
                        try
                        {
                            return float.Parse(this.StringValue, CultureInfo.InvariantCulture);
                        }
                        catch (FormatException)
                        {
                            return 0.0f;
                        }
                    case Type.Bool:
                        return this.BoolValue ? 1.0f : 0.0f;
                    case Type.Null:
                        return 0.0f;
                    default:
                        throw new InvalidOperationException ("Cannot cast to number from " + type.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the boolean representation of this value.
        /// </summary>
        /// <remarks>
        /// This method will attempt to convert the value to a number, if
        /// it isn't already. The conversion is done in the following ways:
        ///
        /// * If the value is a string, it will return `true` if the string
        /// is not empty.
        ///
        /// * If the value is a number, it will return `true` if the value
        /// is non-zero, and `false` otherwise.
        ///
        /// * If the value is `null`, it will return `false`.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the
        /// underlying value cannot be converted to a <see
        /// cref="bool"/>.</exception>
        public bool AsBool
        {
            get
            {
                switch (type)
                {
                    case Type.Number:
                        return !float.IsNaN(this.NumberValue) && this.NumberValue != 0.0f;
                    case Type.String:
                        return !String.IsNullOrEmpty(this.StringValue);
                    case Type.Bool:
                        return this.BoolValue;
                    case Type.Null:
                        return false;
                    default:
                        throw new InvalidOperationException("Cannot cast to bool from " + type.ToString());
                }
            }
        }

        /// <summary>
        /// Gets the string representation of this value.
        /// </summary>
        /// <remarks>
        /// This method will attempt to convert the value to a string, if
        /// it isn't already. Conversions are done using the <see
        /// cref="CultureInfo"/> class's <see
        /// cref="CultureInfo.InvariantCulture"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the
        /// underlying value cannot be converted to a <see
        /// cref="string"/>.</exception>
        public string AsString
        {
            get
            {
                switch (type)
                {
                    case Type.Number:
                        if (float.IsNaN(this.NumberValue))
                        {
                            return "NaN";
                        }
                        return this.NumberValue.ToString(CultureInfo.InvariantCulture);
                    case Type.String:
                        return this.StringValue;
                    case Type.Bool:
                        return this.BoolValue.ToString(CultureInfo.InvariantCulture);
                    case Type.Null:
                        return "null";
                    default:
                        throw new InvalidOperationException("Cannot cast to string from " + type.ToString());
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Value"/> class.
        /// The value will be `null`.
        /// </summary>
        /// <returns>A <see cref="Value"/>, containing `null`.</returns>
        public Value () : this(null) { }

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
        public Value (object value)
        {
            // Copy an existing value
            if (typeof(Value).IsInstanceOfType(value)) {
                var otherValue = value as Value;
                type = otherValue.type;
                switch (type) {
                case Type.Number:
                    NumberValue = otherValue.NumberValue;
                    break;
                case Type.String:
                    StringValue = otherValue.StringValue;
                    break;
                case Type.Bool:
                    BoolValue = otherValue.BoolValue;
                    break;
                case Type.Null:
                    break;
                default:
                    throw new ArgumentOutOfRangeException ();
                }
                return;
            }
            if (value == null) {
                type = Type.Null;
                return;
            }
            var checkType = value.GetType();
            if (checkType == typeof(string)) {
                type = Type.String;
                StringValue = System.Convert.ToString(value, CultureInfo.InvariantCulture);
                return;
            }
            if (Array.IndexOf(NumberTypes, checkType) >= 0) {
                type = Type.Number;
                NumberValue = System.Convert.ToSingle(value, CultureInfo.InvariantCulture);
                return;
            }
            if (checkType == typeof(bool) ) {
                type = Type.Bool;
                BoolValue = System.Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                return;
            }
            var error = string.Format(CultureInfo.CurrentCulture, "Attempted to create a Value using a {0}; currently, " +
                "Values can only be numbers, strings, bools or null.", value.GetType().Name);
            throw new ArgumentException(error);
        }

        /// <summary>
        /// Compares this <see cref="Value"/> to another object. The other object must either be another instance of <see cref="Value"/>, or `null`.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>Returns the same results as <see cref="CompareTo(Value)"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when `obj` is not a <see cref="Value"/>.</exception>
        public int CompareTo(object obj) {
            if (obj == null)
            {
                return 1;
            }

            // not a value
            if (!(obj is Value other))
            {
                throw new ArgumentException("Object is not a Value");
            }

            // it is a value!
            return ((IComparable<Value>)this).CompareTo(other);
        }

        /// <summary>
        /// Compares this <see cref="Value"/> to another <see cref="Value"/>.
        /// </summary>
        /// <param name="other">The other  <see cref="Value"/> to compare to.</param>
        /// <remarks>The method of comparison depends upon the value's <see cref="BackingValue"/>. 
        ///
        /// * If this value is <see cref="Type.String"/>, then the String class's <see cref="string.Compare(string, string, StringComparison)"/> method is used.
        ///
        /// * If this value is <see cref="Type.Number"/>, then the float type's <see cref="float.CompareTo(float)"/> method is used.
        ///
        /// * If this value is <see cref="Type.Bool"/>, then the bool type's <see cref="bool.CompareTo(bool)"/> method is used.
        ///
        /// * If this value is `null`, the result will be the value 0.
        ///
        /// * If `other` is `null`, the result will be the value 1.
        /// </remarks>
        /// <returns>Returns the result of comparing this <see cref="Value"/> against `other`.</returns>
        public int CompareTo(Value other)
        {
            if (other == null)
            {
                return 1;
            }

            if (other.type == this.type)
            {
                switch (this.type)
                {
                    case Type.Null:
                        return 0;
                    case Type.String:
                        return string.Compare(this.StringValue, other.StringValue, StringComparison.InvariantCulture);
                    case Type.Number:
                        return this.NumberValue.CompareTo(other.NumberValue);
                    case Type.Bool:
                        return this.BoolValue.CompareTo(other.BoolValue);
                }
            }

            // try to do a string test at that point!
            return string.Compare(this.AsString, other.AsString, StringComparison.InvariantCulture);
        }

        /// <summary>
        /// Compares to see if this <see cref="Value"/> is the same as another.
        /// </summary>
        /// <remarks>
        /// `obj` is converted to the same type as this value, using <see cref="AsNumber"/>, <see cref="AsString"/>, and <see cref="AsBool"/>.
        ///
        /// If this value is `null`, this method returns `true` if any of the following are true:
        ///
        /// * `obj` is null
        ///
        /// * `obj.AsNumber` is 0
        ///
        /// * `obj.AsBool` is `false`.
        /// </remarks>
        /// <param name="obj">The other <see cref="Value"/> to compare against.</param>
        /// <returns>`true` if the objects represent the same value, `false` otherwise.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || this.GetType() != obj.GetType()) {
                return false;
            }

            var other = (Value)obj;

            switch (this.type) {
            case Type.Number:
                return this.AsNumber == other.AsNumber;
            case Type.String:
                return this.AsString == other.AsString;
            case Type.Bool:
                return this.AsBool == other.AsBool;
            case Type.Null:
                return other.type == Type.Null || other.AsNumber == 0 || other.AsBool == false;
            default:
                throw new ArgumentOutOfRangeException ();
            }

        }

        // override object.GetHashCode
        /// <summary>
        /// Returns the hash code for this value.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            var backing = this.BackingValue;

            // TODO: yeah hay maybe fix this
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
        public override string ToString ()
        {
            return string.Format (CultureInfo.CurrentCulture,
                "[Value: type={0}, AsNumber={1}, AsBool={2}, AsString={3}]",
                type,
                AsNumber,
                AsBool,
                AsString);
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
        public static Value operator+ (Value a, Value b) {
            // catches:
            // undefined + string
            // number + string
            // string + string
            // bool + string
            // null + string
            if (a.type == Type.String || b.type == Type.String ) {
                // we're headed for string town!
                return new Value( a.AsString + b.AsString );
            }

            // catches:
            // number + number
            // bool (=> 0 or 1) + number
            // null (=> 0) + number
            // bool (=> 0 or 1) + bool (=> 0 or 1)
            // null (=> 0) + null (=> 0)
            if ((a.type == Type.Number || b.type == Type.Number) ||
                (a.type == Type.Bool && b.type == Type.Bool) ||
                (a.type == Type.Null && b.type == Type.Null)
            ) {
                return new Value( a.AsNumber + b.AsNumber );
            }

            throw new System.ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "Cannot add types {0} and {1}.", a.type, b.type )
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
        public static Value operator- (Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Null)
            ) {
                return new Value( a.AsNumber - b.AsNumber );
            }

            throw new System.ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "Cannot subtract types {0} and {1}.", a.type, b.type )
            );
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
        public static Value operator* (Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Null)
            ) {
                return new Value( a.AsNumber * b.AsNumber );
            }

            throw new System.ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "Cannot multiply types {0} and {1}.", a.type, b.type )
            );
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
        public static Value operator/ (Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Null)
            ) {
                return new Value( a.AsNumber / b.AsNumber );
            }

            throw new System.ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "Cannot divide types {0} and {1}.", a.type, b.type )
            );
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
        public static Value operator %(Value a, Value b) {
            if (a.type == Type.Number && (b.type == Type.Number || b.type == Type.Null) ||
                b.type == Type.Number && (a.type == Type.Number || a.type == Type.Null)) {
                return new Value (a.AsNumber % b.AsNumber);
            }
            throw new System.ArgumentException(
                string.Format(CultureInfo.CurrentCulture, "Cannot modulo types {0} and {1}.", a.type, b.type )
            );
        }

        /// <summary>
        /// Converts this <see cref="Value"/> to the type specified by
        /// <paramref name="parameterType"/>.
        /// </summary>
        /// <remarks>parameterType must be one of the following:
        ///
        /// * <see cref="string"/> 
        ///
        /// * <see cref="int"/>
        ///
        /// * <see cref="float"/>
        ///
        /// * <see cref="double"/>
        ///
        /// * <see cref="bool"/>
        ///
        /// * <see cref="Value"/>
        ///
        /// If any other type is provided, this method will throw an
        /// exception.
        /// </remarks>
        /// <param name="parameterType">The type to convert this <see
        /// cref="Value"/> to.</param>
        /// <throws cref="InvalidCastException">Thrown when <paramref
        /// name="parameterType"/> is a type that instances of the <see
        /// cref="Value"/> class cannot be converted to.</throws>
        /// <returns>This Value, converted to the specified type.</returns>
        internal object ConvertTo(System.Type parameterType)
        {
            if (parameterType.IsAssignableFrom(typeof(Value)))
            {
                return this;
            }

            if (parameterType.IsAssignableFrom(typeof(string)))
            {
                return this.AsString;
            }

            if (parameterType.IsAssignableFrom(typeof(int)))
            {
                return (int)this.AsNumber;
            }

            if (parameterType.IsAssignableFrom(typeof(float)))
            {
                return (float)this.AsNumber;
            }

            if (parameterType.IsAssignableFrom(typeof(double)))
            {
                return (double)this.AsNumber;
            }

            if (parameterType.IsAssignableFrom(typeof(bool)))
            {
                return this.AsBool;
            }

            throw new InvalidCastException($"Cannot convert value {this} ({type}) to {parameterType}");
        }

        /// <summary>
        /// Computes the negative of a value.
        /// </summary>
        /// <remarks>
        /// If the value is a number, the negative of that number is
        /// returned.
        ///
        /// If the value is `null` or a string, the number `-0` (negative
        /// zero) is returned.
        /// 
        /// Otherwise, a number containing the floating point value `NaN` (not a number) is returned.
        /// </remarks>        
        /// <param name="a">The first value.</param>
        /// <param name="b">The second value.</param>
        /// <returns>A new <see cref="Value"/>, containing the remainder of
        /// dividing two values .</returns>
        /// <throws cref="ArgumentException">Thrown when the two values
        /// cannot be divided.</throws>
        public static Value operator - (Value a) {
            if (a.type == Type.Number)
            {
                return new Value(-a.AsNumber);
            }
            if (a.type == Type.Null &&
                a.type == Type.String &&
               (a.AsString == null || a.AsString.Trim() == string.Empty)
            )
            {
                return new Value(-0);
            }
            return new Value(float.NaN);
        }

        /// <summary>
        /// Compares two values, and returns `true` if the first is greater than the second.
        /// </summary>
        /// <param name="operand1">The first value.</param>
        /// <param name="operand2">The second value.</param>
        /// <returns>`true` if `operand1` is greater than `operand2`, false otherwise.</returns>
        public static bool operator >(Value operand1, Value operand2)
        {
            return ((IComparable<Value>)operand1).CompareTo(operand2) == 1;
        }

        /// <summary>
        /// Compares two values, and returns `true` if the first is less than the second.
        /// </summary>
        /// <param name="operand1">The first value.</param>
        /// <param name="operand2">The second value.</param>
        /// <returns>`true` if `operand1` is less than `operand2`, false otherwise.</returns>
        public static bool operator <(Value operand1, Value operand2)
        {
            return ((IComparable<Value>)operand1).CompareTo(operand2) == -1;
        }

        /// <summary>
        /// Compares two values, and returns `true` if the first is greater than or equal to the second.
        /// </summary>
        /// <param name="operand1">The first value.</param>
        /// <param name="operand2">The second value.</param>
        /// <returns>`true` if `operand1` is greater than or equal to `operand2`, false otherwise.</returns>
        public static bool operator >=(Value operand1, Value operand2)
        {
            return ((IComparable<Value>)operand1).CompareTo(operand2) >= 0;
        }

        /// <summary>
        /// Compares two values, and returns `true` if the first is less than or equal to the second.
        /// </summary>
        /// <param name="operand1">The first value.</param>
        /// <param name="operand2">The second value.</param>
        /// <returns>`true` if `operand1` is less than or equal to `operand2`, false otherwise.</returns>
        // Define the is less than or equal to operator.
        public static bool operator <=(Value operand1, Value operand2)
        {
            return ((IComparable<Value>)operand1).CompareTo(operand2) <= 0;
        }
    }
}
