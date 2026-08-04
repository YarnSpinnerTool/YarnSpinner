#nullable enable

namespace Yarn.Shared
{
    public abstract record Parameter(string Name, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype)
    {
        public abstract string CreateFunctionParameterString(int parameterIndex, int arrayIndex);
        public abstract string CreateCommandParameterString(int parameterIndex, int arrayIndex);
        public abstract string ShortFormType { get; }
    }
    public record UnknownParameter(string Name, NamedType Type, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
    {
        public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
        {
            throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is still of an unresolved type, this should be impossible at this point!");
        }
        public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
        {
            throw new System.ArgumentException($"{Name} parameter at {parameterIndex} is still of an unresolved type, this should be impossible at this point!");
        }
        public override string ShortFormType => Type.shortType;
    }

    public record TokenParameter(string Name, bool IsYarnToken, bool IsArray, bool IsOut, bool HasDefaultValue, string? DefaultValueDisplay, bool IsNodeAttributed, string? AttributedEnumSubtype): Parameter(Name, IsArray, IsOut, HasDefaultValue, DefaultValueDisplay, IsNodeAttributed, AttributedEnumSubtype)
    {
        public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
        {
            if (IsArray)
            {
                return "throw new System.ArgumentException(\"Asked to create a token array but this is forbidden, encountering this should be impossible at this point.\");";
            }

            if (IsYarnToken)
            {
                return $"var p{parameterIndex} = token;";
            }
            else
            {
                return $"var p{parameterIndex} = token.HurryUpToken;";
            }
        }
        public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
        {
            if (IsArray)
            {
                return "throw new System.ArgumentException(\"Asked to create a token array but this is forbidden, encountering this should be impossible at this point.\");";
            }
            return CreateFunctionParameterString(parameterIndex, 0);
        }

        public override string ShortFormType => IsYarnToken ? "LineCancellationToken" : "CancellationToken";
    }
}
namespace System.Runtime.CompilerServices { class IsExternalInit { } }