#nullable enable

namespace Yarn.Shared;

public record BasicParameter(string Name, YarnSpecialType SpecialType, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
{
    public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
    {
        if (!IsArray)
        {
            string functionParameterIConverterTemplater = "var p{0} = parameters[{2}].To{1}(System.Globalization.CultureInfo.InvariantCulture);";
            return SpecialType switch
            {
                YarnSpecialType.Boolean => string.Format(functionParameterIConverterTemplater, parameterIndex, "Boolean", arrayIndex),
                YarnSpecialType.String => string.Format(functionParameterIConverterTemplater, parameterIndex, "String", arrayIndex),
                YarnSpecialType.SByte => string.Format(functionParameterIConverterTemplater, parameterIndex, "SByte", arrayIndex),
                YarnSpecialType.Byte => string.Format(functionParameterIConverterTemplater, parameterIndex, "Byte", arrayIndex),
                YarnSpecialType.Int16 => string.Format(functionParameterIConverterTemplater, parameterIndex, "Int16", arrayIndex),
                YarnSpecialType.UInt16 => string.Format(functionParameterIConverterTemplater, parameterIndex, "UInt16", arrayIndex),
                YarnSpecialType.Int32 => string.Format(functionParameterIConverterTemplater, parameterIndex, "Int32", arrayIndex),
                YarnSpecialType.UInt32 => string.Format(functionParameterIConverterTemplater, parameterIndex, "UInt32", arrayIndex),
                YarnSpecialType.Int64 => string.Format(functionParameterIConverterTemplater, parameterIndex, "Int64", arrayIndex),
                YarnSpecialType.UInt64 => string.Format(functionParameterIConverterTemplater, parameterIndex, "UInt64", arrayIndex),
                YarnSpecialType.Decimal => string.Format(functionParameterIConverterTemplater, parameterIndex, "Decimal", arrayIndex),
                YarnSpecialType.Single => string.Format(functionParameterIConverterTemplater, parameterIndex, "Single", arrayIndex),
                YarnSpecialType.Double => string.Format(functionParameterIConverterTemplater, parameterIndex, "Double", arrayIndex),
                _ => throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is a basic parameter but is of type {SpecialType}, this is unsupported"),
            };
        }
        else
        {
            var format = """
            int length = parameters.Length - {3};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                var pi = parameters[i + {3}].To{2}(System.Globalization.CultureInfo.InvariantCulture);
                p{0}[i] = pi;
            }}
            """;

            return SpecialType switch
            {
                YarnSpecialType.Boolean => string.Format(format, parameterIndex, "bool", "Boolean", arrayIndex),
                YarnSpecialType.String => string.Format(format, parameterIndex, "string", "String", arrayIndex),
                YarnSpecialType.SByte => string.Format(format, parameterIndex, "sbyte", "SByte", arrayIndex),
                YarnSpecialType.Byte => string.Format(format, parameterIndex, "byte", "Byte", arrayIndex),
                YarnSpecialType.Int16 => string.Format(format, parameterIndex, "short", "Int16", arrayIndex),
                YarnSpecialType.UInt16 => string.Format(format, parameterIndex, "ushort", "UInt16", arrayIndex),
                YarnSpecialType.Int32 => string.Format(format, parameterIndex, "int", "Int32", arrayIndex),
                YarnSpecialType.UInt32 => string.Format(format, parameterIndex, "uint", "UInt32", arrayIndex),
                YarnSpecialType.Int64 => string.Format(format, parameterIndex, "long", "Int64", arrayIndex),
                YarnSpecialType.UInt64 => string.Format(format, parameterIndex, "ulong", "UInt64", arrayIndex),
                YarnSpecialType.Decimal => string.Format(format, parameterIndex, "decimal", "Decimal", arrayIndex),
                YarnSpecialType.Single => string.Format(format, parameterIndex, "float", "Single", arrayIndex),
                YarnSpecialType.Double => string.Format(format, parameterIndex, "double", "Double", arrayIndex),
                _ => throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is a basic parameter but is of type {SpecialType}, this is unsupported"),
            };
        }
    }

    public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
    {
        if (!IsArray)
        {
            return CommandString(parameterIndex, arrayIndex);
        }
        else
        {
            return CommandArrayString(parameterIndex, arrayIndex);
        }
    }
    private string CommandArrayString(int parameterIndex, int commandPieceIndex)
    {
        if (SpecialType == YarnSpecialType.Boolean)
        {
            var boolFormat = """
            var length = commandPieces.Count - {2};
            var p{0} = new bool[length];
            for (int i = 0; i < length; i++)
            {{
                if (commandPieces[i + {2}] == "{1}")
                {{
                    p{0}[i] = true;
                }}
                else if (bool.TryParse(commandPieces[i + {2}], out var pi))
                {{
                    p{0}[i] = pi;
                }}
                else
                {{
                    throw new System.ArgumentException($"Unable to make a bool from {{commandPieces[i + {2}]}}");
                }}
            }}
            """;
            return string.Format(boolFormat, parameterIndex, Name, commandPieceIndex);
        }
        else if (SpecialType == YarnSpecialType.String)
        {
            var stringFormat = """
            var length = commandPieces.Count - {1};
            var p{0} = new string[length];
            for (int i = 0; i < length; i++)
            {{
                p{0}[i] = commandPieces[i + {1}];
            }}
            """;
            return string.Format(stringFormat, parameterIndex, commandPieceIndex);
        }

        var format = """
        var length = commandPieces.Count - {2};
        var p{0} = new {1}[length];
        for (int i = 0; i < length; i++)
        {{
            if ({1}.TryParse(commandPieces[i + {2}], out var pi))
            {{
                p{0}[i] = pi;
            }}
            else
            {{
                throw new System.ArgumentException($"Unable to make a {1} from {{commandPieces[i + {2}]}}");
            }}
        }}
        """;

        return SpecialType switch
        {
            YarnSpecialType.SByte => string.Format(format, parameterIndex, "sbyte", commandPieceIndex),
            YarnSpecialType.Byte => string.Format(format, parameterIndex, "byte", commandPieceIndex),
            YarnSpecialType.Int16 => string.Format(format, parameterIndex, "short", commandPieceIndex),
            YarnSpecialType.UInt16 => string.Format(format, parameterIndex, "ushort", commandPieceIndex),
            YarnSpecialType.Int32 => string.Format(format, parameterIndex, "int", commandPieceIndex),
            YarnSpecialType.UInt32 => string.Format(format, parameterIndex, "uint", commandPieceIndex),
            YarnSpecialType.Int64 => string.Format(format, parameterIndex, "long", commandPieceIndex),
            YarnSpecialType.UInt64 => string.Format(format, parameterIndex, "ulong", commandPieceIndex),
            YarnSpecialType.Decimal => string.Format(format, parameterIndex, "decimal", commandPieceIndex),
            YarnSpecialType.Single => string.Format(format, parameterIndex, "float", commandPieceIndex),
            YarnSpecialType.Double => string.Format(format, parameterIndex, "double", commandPieceIndex),
            _ => throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is a basic parameter but is of type {SpecialType}, this is unsupported"),
        };
    }
    private string CommandString(int parameterIndex, int arrayIndex)
    {
        if (SpecialType == YarnSpecialType.Boolean)
        {
            var format = """
            bool p{0};
            if (commandPieces[{2}] == "{1}")
            {{
                p{0} = true;
            }}
            else if (!bool.TryParse(commandPieces[{2}], out p{0}))
            {{
                throw new System.ArgumentException($"Unable to make a bool from {{commandPieces[{2}]}}");
            }}
            """;
            return string.Format(format, parameterIndex, Name, arrayIndex);
        }
        else if (SpecialType == YarnSpecialType.String)
        {
            return $"string p{parameterIndex} = commandPieces[{arrayIndex}];";
        }

        string top;
        string type;
        switch (SpecialType)
        {
            case YarnSpecialType.SByte:
                top = $"if (!sbyte.TryParse(commandPieces[{arrayIndex}], out sbyte p{parameterIndex}))";
                type = "sbyte";
                break;
            case YarnSpecialType.Byte:
                top = $"if (!byte.TryParse(commandPieces[{arrayIndex}], out byte p{parameterIndex}))";
                type = "byte";
                break;
            case YarnSpecialType.Int16:
                top = $"if (!Int16.TryParse(commandPieces[{arrayIndex}], out Int16 p{parameterIndex}))";
                type = "short";
                break;
            case YarnSpecialType.UInt16:
                top = $"if (!UInt16.TryParse(commandPieces[{arrayIndex}], out UInt16 p{parameterIndex}))";
                type = "ushort";
                break;
            case YarnSpecialType.Int32:
                top = $"if (!int.TryParse(commandPieces[{arrayIndex}], out int p{parameterIndex}))";
                type = "int";
                break;
            case YarnSpecialType.UInt32:
                top = $"if (!uint.TryParse(commandPieces[{arrayIndex}], out uint p{parameterIndex}))";
                type = "uint";
                break;
            case YarnSpecialType.Int64:
                top = $"if (!long.TryParse(commandPieces[{arrayIndex}], out long p{parameterIndex}))";
                type = "long";
                break;
            case YarnSpecialType.UInt64:
                top = $"if (!ulong.TryParse(commandPieces[{arrayIndex}], out ulong p{parameterIndex}))";
                type = "ulong";
                break;
            case YarnSpecialType.Decimal:
                top = $"if (!decimal.TryParse(commandPieces[{arrayIndex}], out decimal p{parameterIndex}))";
                type = "decimal";
                break;
            case YarnSpecialType.Single:
                top = $"if (!float.TryParse(commandPieces[{arrayIndex}], out float p{parameterIndex}))";
                type = "float";
                break;
            case YarnSpecialType.Double:
                top = $"if (!double.TryParse(commandPieces[{arrayIndex}], out double p{parameterIndex}))";
                type = "double";
                break;

            default:
                throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is a basic parameter but is of type {SpecialType}, this is unsupported");
        }

        var tail = """
        {0}
        {{
            throw new System.ArgumentException($"Unable to make a {1} from {{commandPieces[{2}]}}");
        }}
        """;
        return string.Format(tail, top, type, arrayIndex);
    }

    public override string ShortFormType
    {
        get
        {
            return SpecialType switch
            {
                YarnSpecialType.Boolean => "bool",
                YarnSpecialType.String => "string",
                YarnSpecialType.SByte => "sbyte",
                YarnSpecialType.Byte => "byte",
                YarnSpecialType.Int16 => "short",
                YarnSpecialType.UInt16 => "ushort",
                YarnSpecialType.Int32 => "int",
                YarnSpecialType.UInt32 => "uint",
                YarnSpecialType.Int64 => "long",
                YarnSpecialType.UInt64 => "ulong",
                YarnSpecialType.Decimal => "decimal",
                YarnSpecialType.Single => "float",
                YarnSpecialType.Double => "double",
                _ => throw new System.ArgumentException($"{Name} parameter at is a basic parameter but is of type {SpecialType}, this is unsupported"),
            };
        }
    }
}