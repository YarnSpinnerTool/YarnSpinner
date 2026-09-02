#nullable enable

namespace Yarn.Shared;

public record GameObjectParameter(string Name, NamedType NamedType, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
{
    public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
    {
        string format;
        if (IsArray)
        {
            format = """
            int length = parameters.Length - {1};
            var p{0} = new UnityEngine.GameObject[length];

            for (int i = 0; i < length; i++)
            {{
                var giN = parameters[i + {1}].ToString(System.Globalization.CultureInfo.InvariantCulture);
                var pi = GameObject.Find(giN);

                if (pi == null)
                {{
                    throw new System.ArgumentException($"Unable to identify a game object called '{{giN}}'");
                }}
                p{0}[i] = pi;
            }}
            """;
        }
        else
        {
            format = """
            var g{0}N = parameters[{1}].ToString(System.Globalization.CultureInfo.InvariantCulture);
            var p{0} = GameObject.Find(g{0}N);
            if (p{0} == null)
            {{
                throw new System.ArgumentException($"Unable to identify a game object called '{{g{0}N}}'");
            }}
            """;
        }
        return string.Format(format, parameterIndex, arrayIndex);
    }

    public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
    {
        string format;
        if (IsArray)
        {
            format = """
            int length = commandPieces.Count - {1};
            var p{0} = new UnityEngine.GameObject[length];

            for (int i = 0; i < length; i++)
            {{
                var giN = commandPieces[i + {1}];
                var pi = GameObject.Find(giN);

                if (pi == null)
                {{
                    throw new System.ArgumentException($"Unable to identify a game object called '{{giN}}'");
                }}
                p{0}[i] = pi;
            }}
            """;
        }
        else
        {
            format = """
            var g{0}N = commandPieces[{1}];
            var p{0} = GameObject.Find(g{0}N);
            if (p{0} == null)
            {{
                throw new System.ArgumentException($"Unable to identify a game object called '{{g{0}N}}'");
            }}
            """;
        }

        return string.Format(format, parameterIndex, arrayIndex);
    }

    public override string ShortFormType => "GameObject";
}