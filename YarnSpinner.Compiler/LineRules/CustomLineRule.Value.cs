#nullable enable

namespace Yarn.Compiler
{
    partial class CustomLineRule
    {
        enum ValueType { Bool, Int, String }

        readonly struct Value
        {
            private readonly bool boolValue;
            private readonly int intValue;
            private readonly string? stringValue;
            private readonly ValueType type;

            public ValueType Type => type;

            public override string ToString()
            {
                return type switch
                {
                    ValueType.Bool => boolValue.ToString(),
                    ValueType.Int => intValue.ToString(),
                    ValueType.String => stringValue!.ToString(),
                    _ => "?",
                };
            }

            public Value(string b) { type = ValueType.String; stringValue = b.Trim().Trim('"'); boolValue = default; intValue = default; }
            public Value(bool b) { type = ValueType.Bool; boolValue = b; intValue = default; stringValue = default; }
            public Value(int i) { type = ValueType.Int; intValue = i; boolValue = default; stringValue = default; }
            private readonly void EnsureString() { if (type != ValueType.String) { throw new System.InvalidOperationException("Value must be a string"); } }
            private readonly void EnsureBool() { if (type != ValueType.Bool) { throw new System.InvalidOperationException("Value must be a bool"); } }
            private readonly void EnsureInt() { if (type != ValueType.Int) { throw new System.InvalidOperationException("Value must be a int"); } }
            public static implicit operator Value(int a) { return new(a); }
            public static implicit operator Value(bool a) { return new(a); }
            public static implicit operator Value(string a) { return new(a); }

            public static Value operator >(Value a, Value b)
            {
                a.EnsureInt(); b.EnsureInt();
                return a.intValue > b.intValue;
            }
            public static Value operator <(Value a, Value b)
            {
                a.EnsureInt(); b.EnsureInt();
                return a.intValue < b.intValue;
            }
            public static Value operator <=(Value a, Value b)
            {
                a.EnsureInt(); b.EnsureInt();
                return a.intValue <= b.intValue;
            }
            public static Value operator >=(Value a, Value b)
            {
                a.EnsureInt(); b.EnsureInt();
                return a.intValue <= b.intValue;
            }
            public static Value operator ==(Value a, Value b)
            {
                if (a.type == ValueType.Int)
                {
                    b.EnsureInt();
                    return a.intValue == b.intValue;
                }
                else if (a.type == ValueType.String)
                {
                    b.EnsureString();
                    return a.stringValue!.Equals(b.stringValue, System.StringComparison.InvariantCulture);
                }
                {
                    a.EnsureBool();
                    b.EnsureBool();
                    return a.boolValue == b.boolValue;
                }
            }
            public static Value operator !=(Value a, Value b)
            {
                if (a.type == ValueType.Int)
                {
                    b.EnsureInt();
                    return a.intValue != b.intValue;
                }
                else if (a.type == ValueType.String)
                {
                    b.EnsureString();
                    return !a.stringValue!.Equals(b.stringValue, System.StringComparison.InvariantCulture);
                }
                else
                {
                    a.EnsureBool();
                    b.EnsureBool();
                    return a.boolValue != b.boolValue;
                }
            }
            public static Value operator !(Value a)
            {
                a.EnsureBool();
                return !a.boolValue;
            }
            public static bool operator true(Value a)
            {
                a.EnsureBool();
                return a.boolValue == true;
            }
            public static bool operator false(Value a)
            {
                a.EnsureBool();
                return a.boolValue == false;
            }
            public static Value operator &(Value a, Value b)
            {
                a.EnsureBool(); b.EnsureBool();
                return a.boolValue && b.boolValue;
            }
            public static Value operator |(Value a, Value b)
            {
                a.EnsureBool(); b.EnsureBool();
                return a.boolValue || b.boolValue;
            }
            public static Value operator ^(Value a, Value b)
            {
                a.EnsureBool(); b.EnsureBool();
                return a.boolValue ^ b.boolValue;
            }
            public static implicit operator bool(Value a)
            {
                a.EnsureBool();
                return a.boolValue;
            }
            public static implicit operator int(Value a)
            {
                a.EnsureInt();
                return a.intValue;
            }
            public static implicit operator string(Value a)
            {
                a.EnsureString();
                return a.stringValue!;
            }
            public override bool Equals(object? other)
            {
                if (other == null) { return false; }

                if (!(other is Value otherValue))
                {
                    return false;
                }
                if (otherValue.type != this.type)
                {
                    return false;
                }
                return type switch
                {
                    ValueType.Bool => boolValue == otherValue.boolValue,
                    ValueType.Int => intValue == otherValue.intValue,
                    ValueType.String => stringValue?.Equals(otherValue.stringValue, System.StringComparison.InvariantCulture) ?? false,
                    _ => false,
                };
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
    }
}
