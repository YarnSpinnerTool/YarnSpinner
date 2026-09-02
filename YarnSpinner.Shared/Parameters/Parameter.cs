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
        private string CreateParameterString(int parameterIndex, int arrayIndex, bool isFunction)
        {
            if (IsArray)
            {
                return "throw new System.ArgumentException(\"Asked to create a token array but this is forbidden, encountering this should be impossible at this point.\");";
            }

            // functions can't take a wombo cancellation token, just a normal token
            if (isFunction)
            {
                return $"var p{parameterIndex} = token;";
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
        public override string CreateFunctionParameterString(int parameterIndex, int arrayIndex)
        {
            return CreateParameterString(parameterIndex, arrayIndex, true);
        }
        public override string CreateCommandParameterString(int parameterIndex, int arrayIndex)
        {
            if (IsArray)
            {
                return "throw new System.ArgumentException(\"Asked to create a token array but this is forbidden, encountering this should be impossible at this point.\");";
            }
            return CreateParameterString(parameterIndex, arrayIndex, false);
        }

        public override string ShortFormType => IsYarnToken ? "LineCancellationToken" : "CancellationToken";
    }
}
namespace System.Runtime.CompilerServices { class IsExternalInit { } }