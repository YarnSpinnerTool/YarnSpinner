namespace Yarn.Analyser;

#nullable enable

using System.Collections.Immutable;
using Yarn.Shared;

public class SyntaxBuilder
{
    private const string usings = """
    #pragma warning disable CS1998
    using System;
    using System.Threading;
    using System.Collections.Generic;
    using UnityEngine;
    using Yarn;
    using Yarn.Unity;
    """;

    private const string declaration = "public static class {0}_{1}Invoker";

    private const string invalidActionEncounteredAtRuntimeTemplate = """
    throw new System.ArgumentException("'{0}' is an invalid {1} and cannot be called");
    """;

    private const string defaultedParameterboundCheckTemplate = """
    if (commandPieces.Count > {0})
    {{
        {1}
        defaultValues.{2} = {2};
    }}
    """;

    private const string defaultFunctionParameterCheckTemplate = """
    if (parameters.Length > {0})
    {{
        {1}
        defaultValues.{2} = {2};
    }}
    """;

    public static string? BuildSyntaxStringForFunctions(string name, string version, ImmutableArray<Action> functions, ImmutableArray<YarnConverter> converters, ILogger? logger = null)
    {
        if (functions.Length == 0)
        {
            logger?.WriteLine("we have no actions, so no code gen is necessary");
            return null;
        }

        logger?.WriteLine("Beginning source gen");
        var className = name.Replace('-','_').Replace('.','_');

        IndentingStringBuilder builder = new();
        builder.AppendLine(usings);
        builder.AppendLine();

        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"YarnSpinner\", \"{version}\")]");
        builder.AppendFormat(declaration, className, "Functions");

        using (builder.EnterBlock())
        {
            BuildFunctionsDictionary(functions, builder, logger);

            builder.AppendLine();
            builder.AppendLine("[RuntimeInitializeOnLoadMethod]");
            using (builder.EnterBlock("static void Register()"))
            {
                builder.AppendLine("ActionInvoker.RegisterFunctionHandler(CanHandle, InvokeFunction);");
            }
            builder.AppendLine();

            using(builder.EnterBlock("public static bool CanHandle(string functionName)"))
            {
                builder.AppendLine("return functions.ContainsKey(functionName);");
            }

            BuildFunctionInvoker(functions, converters, builder, logger);
        }

        logger?.WriteLine("Source gen complete");
        return builder.ToString();
    }

    public static string? BuildSyntaxStringForCommands(string name, string version, ImmutableArray<Action> commands, ImmutableArray<YarnConverter> converters, ILogger? logger = null)
    {
        if (commands.Length == 0)
        {
            logger?.WriteLine("we have no actions, so no code gen is necessary");
            return null;
        }

        logger?.WriteLine("Beginning source gen");
        var className = name.Replace('-','_').Replace('.','_');

        IndentingStringBuilder builder = new();
        builder.AppendLine(usings);
        builder.AppendLine();

        builder.AppendLine($"[System.CodeDom.Compiler.GeneratedCode(\"YarnSpinner\", \"{version}\")]");
        builder.AppendFormat(declaration, className, "Commands");

        using(builder.EnterBlock())
        {
            BuildCommandsDictionary(commands, builder, logger);

            builder.AppendLine();
            builder.AppendLine("[RuntimeInitializeOnLoadMethod]");
            using (builder.EnterBlock("static void Register()"))
            {
                builder.AppendLine("ActionInvoker.RegisterCommandHandler(CanHandle, Invoke);");
            }
            builder.AppendLine();

            using(builder.EnterBlock("public static bool CanHandle(string commandName)"))
            {
                builder.AppendLine("return commands.ContainsKey(commandName);");
            }

            BuildCommandInvoker(commands, converters, builder, logger);
        }

        logger?.WriteLine("Source gen complete");
        return builder.ToString();
    }

    private static void BuildCommandsDictionary(ImmutableArray<Action> actions, IndentingStringBuilder builder, ILogger? logger)
    {
        if (actions.Length == 0)
        {
            return;
        }
        string template = "{{ \"{0}\", {1} }}";
        string[] values = new string[actions.Length];

        builder.Append("private static Dictionary<string, int> commands = new(){");
        for (int i = 0; i < actions.Length; i++)
        {
            values[i] = string.Format(template, actions[i].Name, i);
        }
        builder.Append(string.Join(",", values));
        builder.Append("};");
        builder.AppendLine();
    }
    private static void BuildFunctionsDictionary(ImmutableArray<Action> actions, IndentingStringBuilder builder, ILogger? logger)
    {
        if (actions.Length == 0)
        {
            return;
        }
        string template = "{{ \"{0}\", {1} }}";
        string[] values = new string[actions.Length];

        builder.Append("private static Dictionary<string, int> functions = new(){");
        for (int i = 0; i < actions.Length; i++)
        {
            values[i] = string.Format(template, actions[i].Name, i);
        }
        builder.Append(string.Join(",", values));
        builder.Append("};");
        builder.AppendLine();
    }

    private static void BuildFunctionInvoker(ImmutableArray<Action> actions, ImmutableArray<YarnConverter> converters, IndentingStringBuilder builder, ILogger? logger)
    {
        if (actions.Length == 0)
        {
            return;
        }

        using(builder.EnterBlock("public static async YarnTask<IConvertible> InvokeFunction(string functionName, IConvertible[] parameters, CancellationToken token)"))
        {
            using (builder.EnterBlock("switch (functions[functionName])"))
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    var action = actions[i];
                    var (min, max) = action.NumberOfParameters;
                    var hasDefaultParameters = action.HasDefaultParameters;

                    using (builder.EnterBlock($"case {i}:"))
                    {
                        if (action is InvalidAction)
                        {
                            builder.AppendFormat(invalidActionEncounteredAtRuntimeTemplate, action.Name, "function");

                            continue;
                        }

                        if (action.MethodName == null)
                        {
                            continue;
                        }

                        // instance methods have a slightly different check around number of expected parameters in the function
                        // the first parameter in the function is the name of the target we will do a lookup upon, so need to skip over it
                        // once we've determined that if the min and max are the same size this means that there are no arrays or optional parameters
                        // so we can do the simpler check, otherwise we need to do a full min and max check
                        int skip = action.IsStatic ? 0: 1;
                        if (min == max)
                        {
                            using(builder.EnterBlock($"if (parameters.Length != {min + skip})"))
                            {
                                builder.AppendFormat("""throw new System.ArgumentException($"Invalid number of parameters {{parameters.Length}} for '{0}'");""", action.Name);
                            }
                        }
                        else
                        {
                            using(builder.EnterBlock($"if (parameters.Length < {min + skip} || parameters.Length > {max + skip})"))
                            {
                                builder.AppendFormat("""throw new System.ArgumentException($"Invalid number of parameters {{parameters.Length}} for '{0}'");""", action.Name);
                            }
                        }

                        if (!action.IsStatic)
                        {
                            // then we need to convert the first parameter of the command into it's appropriate form
                            // this is different depending on if it's a component or converted type
                            var t = Action.isValidYarnableTypeForUnity(action.containingNamedType, converters, logger);
                            switch (t)
                            {
                                case ParameterTypes.UnityComponent:

                                    builder.AppendLine("var gameObjectTargetName = parameters[0].ToString(System.Globalization.CultureInfo.InvariantCulture);");
                                    builder.AppendLine("var gameObjectTarget = GameObject.Find(gameObjectTargetName);");
                                    using(builder.EnterBlock("if (gameObjectTarget == null)"))
                                    {
                                        builder.AppendLine("""throw new System.ArgumentException($"Unable to identify a game object called '{{gameObjectTargetName}}'");""");
                                    }
                                    builder.AppendLine($"var target = gameObjectTarget.GetComponentInChildren<{action.containingTypeShortName}>();");
                                    using(builder.EnterBlock("if (target == null)"))
                                    {
                                        builder.AppendFormat("""throw new System.ArgumentException($"Unable to find a '{0}' on '{{gameObjectTargetName}}'");""", action.containingTypeShortName);
                                    }
                                    break;

                                case ParameterTypes.Converter:

                                    var converterCall = Action.ConverterCallingString(action.containingTypeFullName, converters, logger);
                                    if (converterCall == null)
                                    {
                                        logger?.WriteLine($"unable to find the converter for {action.containingTypeFullName}");
                                        throw new System.ArgumentException($"Instance method is a converter but has no converter method for {action.containingTypeFullName}");
                                    }

                                    builder.AppendLine("var targetName = parameters[0].ToString(System.Globalization.CultureInfo.InvariantCulture);");
                                    using(builder.EnterBlock($"if (!{converterCall}(targetName, out var target))"))
                                    {
                                        builder.AppendFormat("""throw new System.ArgumentException($"Unable to make a '{0}' from '{{targetName}}'");""", action.containingTypeShortName);
                                    }
                                    break;

                                default:
                                    logger?.WriteLine($"Instance method is not a component or custom converter: {t}");
                                    throw new System.ArgumentException($"Instance method is not a component or custom converter: {t}");
                            }
                        }

                        string[] parametersToJoin = new string[action.Parameters.Length];

                        // ok if we have optional parameters we need to first build up our tuple of default parameters
                        if (hasDefaultParameters)
                        {
                            builder.AppendLine(BuildDefaultParameterTuple(action, logger));
                        }

                        // now to create the test for each param
                        for (int j = 0; j < action.Parameters.Length; j++)
                        {
                            var param = action.Parameters[j];

                            string paramString;
                            int paramIndex = j;

                            // because command and instance methods have extra parameters at the start we need to includ the skip into the array
                            // otherwise we'll be off by the number of extra parameters (at max 2)
                            int indexIntoParameterArray = j + skip;

                            if (hasDefaultParameters)
                            {
                                parametersToJoin[j] = $"defaultValues.p{paramIndex}";
                            }
                            else
                            {
                                parametersToJoin[j] = $"p{paramIndex}";
                            }

                            paramString = param.CreateFunctionParameterString(j, indexIntoParameterArray);

                            if (param.HasDefaultValue)
                            {
                                // we are a defaulted parameter
                                // which means we need to do some bounds checking before we can be added in
                                builder.AppendMultilineFormat(defaultFunctionParameterCheckTemplate, paramIndex, paramString, $"p{paramIndex}");
                            }
                            else
                            {
                                // we aren't a defaulted parameter
                                builder.AppendLine(paramString);
                                // but we might be part of a function that is
                                if (hasDefaultParameters)
                                {
                                    builder.AppendLine($"defaultValues.p{paramIndex} = p{paramIndex};", true);
                                }
                            }
                        }

                        builder.AppendLine();
                        builder.Append("return ");
                        // and then call the method
                        // making sure to await it if necessary
                        if (action.IsAsync)
                        {
                            builder.Append("await ");
                        }

                        // if it's a static method we call it via it's fully qualified name
                        // otherwise we call it on our target we found earlier
                        if (action.IsStatic)
                        {
                            builder.Append(action.MethodName);
                        }
                        else
                        {
                            builder.Append($"target.{action.ShortMethodName}");
                        }
                        builder.Append("(");
                        builder.Append(string.Join(",", parametersToJoin));
                        builder.Append(");\n");
                    }
                }
            }
            builder.AppendLine("return null;"); // the fallback situation
        }
    }

    private static void BuildCommandInvoker(ImmutableArray<Action> actions, ImmutableArray<YarnConverter> converters, IndentingStringBuilder builder, ILogger? logger)
    {
        if (actions.Length == 0)
        {
            logger?.WriteLine("Skipping this due to having not actions");
            return;
        }

        using(builder.EnterBlock("public static async YarnTask Invoke(Command command, LineCancellationToken token)"))
        {
            builder.AppendLine("var commandPieces = new List<string>(DialogueRunner.SplitCommandText(command.Text));");
            builder.AppendLine("var index = commands[commandPieces[0]];");
            
            using(builder.EnterBlock("switch (index)"))
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    var action = actions[i];
                    var (min, max) = action.NumberOfParameters;
                    var hasDefaultParameters = action.HasDefaultParameters;

                    using(builder.EnterBlock($"case {i}:"))
                    {
                        if (action is InvalidAction)
                        {
                            builder.AppendFormat(invalidActionEncounteredAtRuntimeTemplate, action.Name, "command");
                            continue;
                        }

                        if (action.MethodName == null)
                        {
                            continue;
                        }

                        int skip = action.IsStatic ? 1 : 2;
                        // if the min and max are the same size this means that there are no arrays or optional parameters
                        // so we can do the simpler check
                        if (min == max)
                        {
                            using(builder.EnterBlock($"if (commandPieces.Count != {min + skip})"))
                            {
                                builder.AppendFormat("""throw new System.ArgumentException($"Invalid number of parameters {{commandPieces.Count}} for '{0}'");""", action.Name);
                            }
                        }
                        else
                        {
                            using(builder.EnterBlock($"if (commandPieces.Count < {min + skip} || commandPieces.Count > {max + skip})"))
                            {
                                builder.AppendFormat("""throw new System.ArgumentException($"Invalid number of parameters {{commandPieces.Count}} for '{0}'");""", action.Name);
                            }
                        }

                        if (!action.IsStatic)
                        {
                            // then we need to convert the first parameter of the command into it's appropriate form
                            // this is different depending on if it's a component or converted type
                            var t = Action.isValidYarnableTypeForUnity(action.containingNamedType, converters, logger);
                            switch (t)
                            {
                                case ParameterTypes.UnityComponent:
                                    builder.AppendLine("var gameObjectTargetName = commandPieces[1];");
                                    builder.AppendLine("var gameObjectTarget = GameObject.Find(gameObjectTargetName);");
                                    using(builder.EnterBlock("if (gameObjectTarget == null)"))
                                    {
                                        builder.AppendLine("""throw new System.ArgumentException($"Unable to identify a game object called '{{gameObjectTargetName}}'");""");
                                    }
                                    builder.AppendLine($"var target = gameObjectTarget.GetComponentInChildren<{action.containingTypeShortName}>();");
                                    using(builder.EnterBlock("if (target == null)"))
                                    {
                                        builder.AppendFormat("""throw new System.ArgumentException($"Unable to find a '{0}' on '{{gameObjectTargetName}}'");""", action.containingTypeShortName);
                                    }
                                    break;

                                case ParameterTypes.Converter:
                                    var converterCall = Action.ConverterCallingString(action.containingTypeFullName, converters, logger);
                                    if (converterCall == null)
                                    {
                                        logger?.WriteLine($"unable to find the converter for {action.containingTypeFullName}");
                                        throw new System.ArgumentException($"Instance method is a converter but has no converter method for {action.containingTypeFullName}");
                                    }

                                    builder.AppendLine("var targetName = commandPieces[1];");
                                    using(builder.EnterBlock($"if (!{converterCall}(targetName, out var target))"))
                                    {
                                        builder.AppendFormat("""throw new System.ArgumentException($"Unable to make a '{0}' from '{{targetName}}'");""", action.containingTypeShortName);
                                    }
                                    break;

                                default:
                                    logger?.WriteLine($"Instance method is not a component or custom converter: {t}");
                                    throw new System.ArgumentException($"Instance method is not a component or custom converter: {t}");
                            }
                        }

                        string[] parametersToJoin = new string[action.Parameters.Length];

                        // ok if we have optional parameters we need to first build up our tuple of default parameters
                        if (hasDefaultParameters)
                        {
                            builder.AppendLine(BuildDefaultParameterTuple(action, logger));
                        }

                        // now to create the test for each param
                        for (int j = 0; j < action.Parameters.Length; j++)
                        {
                            var param = action.Parameters[j];

                            int commandPieceIndex = j + skip;
                            int paramIndex = j;

                            parametersToJoin[j] = $"p{j}";

                            string paramString = param.CreateCommandParameterString(paramIndex, commandPieceIndex);

                            if (hasDefaultParameters)
                            {
                                parametersToJoin[j] = $"defaultValues.p{paramIndex}";
                            }
                            else
                            {
                                parametersToJoin[j] = $"p{paramIndex}";
                            }

                            if (param.HasDefaultValue)
                            {
                                // we are a defaulted parameter
                                // which means we need to do some bounds checking before we can be added in
                                builder.AppendMultilineFormat(defaultedParameterboundCheckTemplate, commandPieceIndex, paramString, $"p{paramIndex}");
                            }
                            else
                            {
                                // we aren't a defaulted parameter
                                builder.AppendLine(paramString);
                                // but we might be part of a command that is
                                if (hasDefaultParameters)
                                {
                                    builder.AppendLine($"defaultValues.p{paramIndex} = p{paramIndex};", true);
                                }
                            }
                        }

                        // and then call the method
                        // making sure to await it if necessary
                        if (action.IsAsync)
                        {
                            builder.Append("await ");
                        }

                        // if it's a static method we call it via it's fully qualified name
                        // otherwise we call it on our target we found earlier
                        if (action.IsStatic)
                        {
                            builder.Append(action.MethodName);
                        }
                        else
                        {
                            builder.Append($"target.{action.ShortMethodName}");
                        }
                        builder.Append("(");
                        builder.Append(string.Join(",", parametersToJoin));
                        builder.Append(");\n");

                        builder.AppendLine("return;");
                    }
                }
            }
        }
    }

    private static string BuildDefaultParameterTuple(Action action, ILogger? logger)
    {
        // ok so the first step is I need to build up the tuple
        // which means going through the parameters

        string[] tupleParams;
        string[] tupleValues;

        // single element tuples aren't allowed
        // so we add one extra dummy element to get around this
        // could also rewrite everything to use ValueTuple but that feels like more work
        if (action.Parameters.Length == 1)
        {
            tupleParams = new string[action.Parameters.Length + 1];
            tupleValues = new string[action.Parameters.Length + 1];
            
            tupleParams[action.Parameters.Length] = "byte dummy";
            tupleValues[action.Parameters.Length] = "0";
        }
        else
        {
            tupleParams = new string[action.Parameters.Length];
            tupleValues = new string[action.Parameters.Length];
        }

        for (int i = 0; i < action.Parameters.Length; i++)
        {
            var param = action.Parameters[i];

            if (param.IsArray)
            {
                tupleParams[i] = $"{param.ShortFormType}[] p{i}";
            }
            else
            {
                tupleParams[i] = $"{param.ShortFormType} p{i}";
            }

            if (param.HasDefaultValue)
            {
                // there is one special case: strings
                // their default value doesn't have the quotation marks
                // which would cause all sorts of issues, so we add those back in
                if (param is BasicParameter bp && bp.SpecialType == YarnSpecialType.String)
                {
                    if (param.DefaultValueDisplay == null)
                    {
                        tupleValues[i] = "null";
                    }
                    else
                    {
                        tupleValues[i] = $"\"{param.DefaultValueDisplay}\"";
                    }
                }
                else
                {
                    tupleValues[i] = param.DefaultValueDisplay?.ToString() ?? "null";
                }
            }
            else
            {
                tupleValues[i] = "default";
            }
        }

        logger?.WriteLine("tuple result: ");
        var tuple = $"({string.Join(", ", tupleParams)}) defaultValues = ({string.Join(", ", tupleValues)});";
        logger?.WriteLine(tuple);
        return tuple;
    }
}