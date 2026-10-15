// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Analyser;

#nullable enable

using System.Linq;
using System.Collections.Generic;
using Yarn.Shared;

/// <summary>
/// Creates the necessary code to find and register any runtime actions with Unity.
/// This is essentially the otherside of registering actions, covering the situations (mostly private methods) that can't be done via compiletime invocation
/// </summary>
public class RuntimeLinkerSyntaxBuilder
{
    private const string usings = """
    using System;
    using UnityEngine;
    using Yarn.Unity;
    using System.Reflection;
    """;

    /// <summary>
    /// Creates a valid C# string that registers all the actions provided as well as does the reflection necessary to actually have a reference to them.
    /// </summary>
    /// <param name="actions">the actions to be registered</param>
    /// <param name="name">As each assembly gets its ownregistration file the assembly name is the only way we have to uniquely identify them</param>
    /// <param name="version">The version of the generated file</param>
    /// <returns></returns>
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
                builder.AppendLine($"// we have {actions.Count} actions to add");
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    if (action is InvalidAction)
                    {
                        builder.AppendLine($"// {action.Name} is an invalid {action.Type}");
                        continue;
                    }
                    else
                    {
                        builder.AppendLine($"// {action.Name} is a {action.Type}");
                    }

                    // getting the method info itself
                    if (action.IsInstance)
                    {
                        builder.AppendLine($"MethodInfo methodInfo{i} = typeof({action.containingTypeShortName}).GetMethod(\"{action.ShortMethodName}\", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);");
                    }
                    else
                    {
                        builder.AppendLine($"MethodInfo methodInfo{i} = typeof({action.containingTypeShortName}).GetMethod(\"{action.ShortMethodName}\", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);");
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