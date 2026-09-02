namespace Yarn.Analyser;

#nullable enable

using System.Linq;
using System.Collections.Immutable;
using Yarn.Shared;

public class RuntimeSyntaxBuilder
{
    private const string usings = """
    using System;
    using UnityEngine;
    using Yarn.Unity;
    """;

    internal static string? BuildSyntax(ImmutableArray<Generator.YarnEnumPayload?> payload, ImmutableArray<YarnConverter?> converters, string name, string version)
    {
        // if we have none we don't run
        if (payload.Length == 0 && converters.Length == 0)
        {
            return null;
        }
        // likewise if we have enums or converters but they are only null we don't run
        if (payload.Count(p => p is not null) + converters.Count(c => c is not null) == 0)
        {
            return null;
        }

        IndentingStringBuilder builder = new();
        builder.AppendLine(usings);
        builder.AppendLine();
        
        var className = name.Replace('-','_').Replace('.','_');

        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"YarnSpinner\", \"{version}\")]");
        builder.AppendLine($"public static class {className}_DynamicConverter");

        using (builder.EnterBlock())
        {
            builder.AppendLine("[RuntimeInitializeOnLoadMethod]");
            using (builder.EnterBlock("static void Register()"))
            {
                builder.AppendLine("ActionInvoker.RegisterRuntimeStringConverter(TryConvertParameter);");
                builder.AppendLine("ActionInvoker.RegisterRuntimeIConvertibleConverter(TryConvertParameter);");
            }
            builder.AppendLine();

            using (builder.EnterBlock("private static bool TryConvertParameter(string input, Type type, out object? value)"))
            {
                foreach (var yarnEnum in payload)
                {
                    if (yarnEnum is null)
                    {
                        continue;
                    }

                    using (builder.EnterBlock($"if (type == typeof({yarnEnum.NamedType.shortType}))"))
                    {
                        builder.AppendLine($"var result = {yarnEnum.NamedType.shortType}EnumHelper.TryGet{yarnEnum.NamedType.shortType}ByName(input, out var temp);");
                        builder.AppendLine("value = temp;");
                        builder.AppendLine("return result;");
                    }
                }
                foreach (var converter in converters)
                {
                    if (converter is null)
                    {
                        continue;
                    }

                    using (builder.EnterBlock($"if (type == typeof({converter.FullyQualifiedTypeName}))"))
                    {
                        using (builder.EnterBlock($"if ({converter.StaticCallingString}(input, out var temp))"))
                        {
                            builder.AppendLine("value = temp;");
                            builder.AppendLine("return true;");
                        }
                    }
                }

                builder.AppendLine();
                builder.AppendLine("value = null;");
                builder.AppendLine("return false;");
            }

            builder.AppendLine();

            using (builder.EnterBlock("private static bool TryConvertParameter(IConvertible input, Type type, out object? value)"))
            {
                foreach (var yarnEnum in payload)
                {
                    if (yarnEnum is null)
                    {
                        continue;
                    }

                    using (builder.EnterBlock($"if (type == typeof({yarnEnum.NamedType.shortType}))"))
                    {
                        if (yarnEnum.Backing == YarnEnum.BackingType.Int)
                        {
                            builder.AppendLine("var backing = input.ToInt32(System.Globalization.CultureInfo.InvariantCulture);");
                        }
                        else
                        {
                            builder.AppendLine("var backing = input.ToString(System.Globalization.CultureInfo.InvariantCulture);");
                        }

                        builder.AppendLine($"var result = {yarnEnum.NamedType.shortType}EnumHelper.TryGet{yarnEnum.NamedType.shortType}FromBacking(backing, out var temp);");
                        builder.AppendLine("value = temp;");
                        builder.AppendLine("return result;");
                    }
                }

                foreach (var converter in converters)
                {
                    if (converter is null)
                    {
                        continue;
                    }

                    using (builder.EnterBlock($"if (type == typeof({converter.FullyQualifiedTypeName}))"))
                    {
                        builder.AppendLine("var convertedInput = input.ToString(System.Globalization.CultureInfo.InvariantCulture);");
                        using (builder.EnterBlock($"if ({converter.StaticCallingString}(convertedInput, out var temp))"))
                        {
                            builder.AppendLine("value = temp;");
                            builder.AppendLine("return true;");
                        }
                    }
                }

                builder.AppendLine();
                builder.AppendLine("value = null;");
                builder.AppendLine("return false;");
            }
        }

        return builder.ToString();
    }
}