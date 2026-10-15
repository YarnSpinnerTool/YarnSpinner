// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.Shared;

/// <summary>
/// Represents a parameter that is a Unity Component subclass (often a MonoBehaviour).
/// </summary>
/// <remarks>
/// These will always be found at runtime through a combination of a <c>Find</c> of the named game object and then a <c>GetComponentInChildren</c> call of the specific component subtype.
/// </remarks>
public record ComponentParameter(string Name, NamedType NamedType, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
{
    /// <inheritdoc />
    public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
    {
        string format;
        if (IsArray)
        {
            format = """
            int length = parameters.Length - {2};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                var giN = parameters[i + {2}].ToString(System.Globalization.CultureInfo.InvariantCulture);
                var go = GameObject.Find(giN);
                if (go == null)
                {{
                    throw new System.ArgumentException($"Unable to identify a game object called '{{giN}}'");
                }}
                var pi = go.GetComponentInChildren<{1}>();
                if (pi == null)
                {{
                    throw new System.ArgumentException($"Unable to find a '{1}' on '{{giN}}'");
                }}
                p{0}[i] = pi;
            }}
            """;
        }
        else
        {
            format = """
            var g{0}N = parameters[{2}].ToString(System.Globalization.CultureInfo.InvariantCulture);
            var g{0} = GameObject.Find(g{0}N);
            if (g{0} == null)
            {{
                throw new System.ArgumentException($"Unable to identify a game object called '{{g{0}N}}'");
            }}
            var p{0} = g{0}.GetComponentInChildren<{1}>();
            if (p{0} == null)
            {{
                throw new System.ArgumentException($"Unable to find a '{1}' on '{{g{0}N}}'");
            }}
            """;
        }
        return string.Format(format, parameterIndex, NamedType.shortType, arrayIndex);
    }

    /// <inheritdoc />
    public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
    {
        string format;
        if (IsArray)
        {
            format = """
            int length = commandPieces.Count - {2};
            var p{0} = new {1}[length];

            for (int i = 0; i < length; i++)
            {{
                var giN = commandPieces[{2}];
                var go = GameObject.Find(giN);
                if (go == null)
                {{
                    throw new System.ArgumentException($"Unable to identify a game object called '{{giN}}'");
                }}
                var pi = go.GetComponentInChildren<{1}>();
                if (pi == null)
                {{
                    throw new System.ArgumentException($"Unable to find a '{1}' on '{{giN}}'");
                }}
                p{0}[i] = pi;
            }}
            """;
        }
        else
        {
            format = """
            var g{0}N = commandPieces[{2}];
            var g{0} = GameObject.Find(g{0}N);
            if (g{0} == null)
            {{
                throw new System.ArgumentException($"Unable to identify a game object called '{{g{0}N}}'");
            }}
            var p{0} = g{0}.GetComponentInChildren<{1}>();
            if (p{0} == null)
            {{
                throw new System.ArgumentException($"Unable to find a '{1}' on '{{g{0}N}}'");
            }}
            """;
        }
        return string.Format(format, parameterIndex, NamedType.shortType, arrayIndex);
    }

    /// <inheritdoc />
    public override string ShortFormType => NamedType.shortType;
}