// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.Shared;

/// <summary>
/// Represents a parameter who's type and value comes from being run through a Yarn Converter attributed method.
/// </summary>
public record ConverterParameter(string Name, YarnConverter Converter, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
{
    /// <inheritdoc />
    public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
    {
        if (IsArray)
        {
            var format = """
            int length = parameters.Length - {3};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                var piN = parameters[i + {3}].ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (!{2}(piN, out var pi))
                {{
                    throw new System.ArgumentException($"was unable to create a '{1}' from {{piN}}");
                }}

                p{0}[i] = pi;
            }}
            """;
            return string.Format(format, parameterIndex, Converter.FullyQualifiedTypeName, Converter.StaticCallingString, arrayIndex);
        }
        else
        {
            var format = """
            var p{0}N = parameters[{3}].ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!{1}(p{0}N, out var p{0}))
            {{
                throw new System.ArgumentException($"Unable to make a '{2}' from '{{p{0}N}}'");
            }}
            """;
            return string.Format(format, parameterIndex, Converter.StaticCallingString, Converter.FullyQualifiedTypeName, arrayIndex);
        }
    }

    /// <inheritdoc />
    public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
    {
        if (IsArray)
        {
            var format = """
            int length = commandPieces.Count - {3};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                var piN = commandPieces[i + {3}];

                if (!{2}(piN, out var pi))
                {{
                    throw new System.ArgumentException($"was unable to create a '{1}' from {{piN}}");
                }}

                p{0}[i] = pi;
            }}
            """;
            return string.Format(format, parameterIndex, Converter.FullyQualifiedTypeName, Converter.StaticCallingString, arrayIndex);
        }
        else
        {
            var format = """
            var p{0}N = commandPieces[{3}];
            if (!{1}(p{0}N, out var p{0}))
            {{
                throw new System.ArgumentException($"Unable to make a '{2}' from '{{p{0}N}}'");
            }}
            """;
            return string.Format(format, parameterIndex, Converter.StaticCallingString, Converter.FullyQualifiedTypeName, arrayIndex);
        }
    }

    /// <inheritdoc />
    public override string ShortFormType => Converter.FullyQualifiedTypeName;
}