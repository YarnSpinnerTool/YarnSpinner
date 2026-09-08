namespace Yarn.Analyser;

#nullable enable

using System.Linq;
using System.Collections.Generic;
using Yarn.Shared;

public class RuntimeLinkerSyntaxBuilder
{
    private const string usings = """
    using System;
    using UnityEngine;
    using Yarn.Unity;
    using System.Reflection;
    """;

    internal static string? BuildSyntax(List<Action> actions, string name, string version)
    {
        // if we only have invalid actions we abort
        if (actions.Count(a => a is not InvalidAction) == 0)
        {
            return null;
        }

        IndentingStringBuilder builder = new();
        builder.AppendLine(usings);
        builder.AppendLine();
        
        var className = name.Replace('-','_').Replace('.','_');
        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"YarnSpinner\", \"{version}\")]");
        builder.AppendLine($"public static class {className}_RuntimeLinker");

        using (builder.EnterBlock())
        {
            builder.AppendLine("[RuntimeInitializeOnLoadMethod]");
            using (builder.EnterBlock("static void Register()"))
            {
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    if (action is InvalidAction)
                    {
                        continue;
                    }

                    // getting the method info itself
                    if (action.IsInstance)
                    {
                        builder.AppendLine($"MethodInfo methodInfo{i} = typeof({action.containingTypeShortName}).GetMethod(\"{action.ShortMethodName}\", BindingFlags.NonPublic | BindingFlags.Instance);");
                    }
                    else
                    {
                        builder.AppendLine($"MethodInfo methodInfo{i} = typeof({action.containingTypeShortName}).GetMethod(\"{action.ShortMethodName}\", BindingFlags.NonPublic | BindingFlags.Static);");
                    }

                    using (builder.EnterBlock($"if (methodInfo{i} == null)"))
                    {
                        builder.AppendLine($"throw new System.InvalidOperationException($\"Unable to find a method named {action.ShortMethodName} on {action.containingTypeShortName}\");");
                    }

                    if (action.Type == ActionType.Command)
                    {
                        builder.AppendLine($"ActionInvoker.AddCommandHandler(\"{action.Name}\", methodInfo{i});");
                    }
                    else
                    {
                        builder.AppendLine($"ActionInvoker.AddFunction(\"{action.Name}\", methodInfo{i});");
                    }

                    // don't add a new line for the last one
                    if (i != actions.Count() - 1)
                    {
                        builder.AppendLine();
                    }
                }
            }
        }

        return builder.ToString();
    }
}