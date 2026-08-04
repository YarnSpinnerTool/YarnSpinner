#nullable enable

namespace Yarn.Shared;

public record EnumParameter(string Name, string EnumTypeName, YarnEnum.BackingType Backing, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
{
    public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
    {
        if (IsArray)
        {
            var format = """
            int length = parameters.Length - {3};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                var pCi = parameters[i + {3}].To{2}(System.Globalization.CultureInfo.InvariantCulture);

                if (!{1}.TryGet{1}FromBacking(pCi, out var pi))
                {{
                    throw new System.ArgumentException($"was unable to create a {1} enum from {{pCi}}");
                }}

                p{0}[i] = pi;
            }}
            """;
            return string.Format(format, parameterIndex, EnumTypeName, Backing == YarnEnum.BackingType.Int ? "Int32" : "String", arrayIndex);
        }
        else
        {
            var format = """
            var pC{0} = parameters[{3}].To{1}(System.Globalization.CultureInfo.InvariantCulture);
            if (!{2}.TryGet{2}FromBacking(pC{0}, out var p{0}))
            {{
                throw new System.ArgumentException($"was unable to create a {2} enum from {{pC{0}}}");
            }}
            """;
            return string.Format(format, parameterIndex, Backing == YarnEnum.BackingType.Int ? "Int32" : "String", EnumTypeName, arrayIndex);
        }
    }

    public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
    {
        if (IsArray)
        {
            var format = """
            int length = commandPieces.Count - {2};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                if (!{1}EnumHelper.TryGet{1}ByName(commandPieces[{2}], out var pi))
                {{
                    throw new System.ArgumentException($"Was unable to create a {1} from {{commandPieces[{2}]}}");
                }}

                p{0}[i] = pi;
            }}
            """;
            return string.Format(format, parameterIndex, EnumTypeName, arrayIndex);
        }
        else
        {
            var format = """
            if (!{0}EnumHelper.TryGet{0}ByName(commandPieces[{2}], out var p{1}))
            {{
                throw new System.ArgumentException($"Was unable to create a {0} from {{commandPieces[{2}]}}");
            }}
            """;
            return string.Format(format, EnumTypeName, parameterIndex, arrayIndex);
        }
    }

    public override string ShortFormType => EnumTypeName;
}
