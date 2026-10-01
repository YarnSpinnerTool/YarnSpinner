using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Yarn.Shared;
using Yarn.HostAnalysis;

#nullable enable

namespace Yarn.Analyser
{
    [Generator]
    public class Generator : IIncrementalGenerator
    {
        // I want:
        // - anything attributed [YarnCommand]
        // - any calls to register command
        // ---------
        // - anything attributed [YarnFunction]
        // - any call to add function
        // ---------
        // - anything attributed [Yarnconverter]

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            ILogger? logger = null;

            try
            {
                // logger = new BetterLogger("generator");

                // the two pieces everything uses are the assembly name
                // and the configuration file
                var configPair = context.CompilationProvider.Select(static (c, _) => c.AssemblyName).Combine(context.AnalyzerConfigOptionsProvider.Select(static (options, cancellationToken) => {return new AnalysisConfiguration(options.GlobalOptions);}));

                // grabbing any converters
                var attributedconverters = context.SyntaxProvider.ForAttributeWithMetadataName("Yarn.Unity.YarnConverterAttribute",
                        predicate: (_,_) => true,
                        transform: (ctx, _) => GetAttributedConverters(ctx, logger)
                ).Collect();

                // attempting to find converters inside the referenced assembly
                var otherConverters = context.CompilationProvider.SelectMany((ctx, _) => Creators.CollectAssemblyConverters(ctx, logger)).Collect();
                var allConverters = otherConverters.Combine(attributedconverters);

                // collecting all attributed commands
                var attributedCommands = context.SyntaxProvider.ForAttributeWithMetadataName("Yarn.Unity.YarnCommandAttribute",
                        predicate: (_,_) => true,
                        transform: (ctx, _) => CreateCompileTimeActionsFromAttribute(ctx, ActionType.Command, logger)
                ).Collect();

                // collecting all non-attributed commands
                var nonAttributedCommands = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) => node is InvocationExpressionSyntax,
                    transform: (context, _) => CreateActionFromSyntax(context, ActionType.Command, logger)
                ).Where(static symbol => symbol is not null).Collect();

                // merging the command info together
                var commandInfo = configPair.Combine(allConverters.Combine(attributedCommands.Combine(nonAttributedCommands)));
                context.RegisterSourceOutput(commandInfo, (spc, value) =>
                {
                    var assemblyName = value.Left.Left;
                    var config = value.Left.Right;
                    if (assemblyName is null)
                    {
                        return;
                    }

                    if (!config.CanGenerateCode(assemblyName))
                    {
                        return;
                    }

                    var converters = Merge(value.Right.Left.Left, value.Right.Left.Right).ToImmutableArray();
                    var mergedCommands = Merge(value.Right.Right.Left, value.Right.Right.Right);
                    RunCommands(spc, assemblyName, this.GetType().Assembly.GetName().Version.ToString(), config, converters, mergedCommands, logger);
                });

                // getting all the attributed functions
                var attributedFunctions = context.SyntaxProvider.ForAttributeWithMetadataName("Yarn.Unity.YarnFunctionAttribute",
                        predicate: (_,_) => true,
                        transform: (ctx, _) => CreateCompileTimeActionsFromAttribute(ctx, ActionType.Function, logger)
                ).Collect();
                
                // collecting all non-attributed functions
                var nonAttributedFunctions = context.SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) => node is InvocationExpressionSyntax,
                    transform: (context, _) => CreateActionFromSyntax(context, ActionType.Function, logger)
                ).Where(static symbol => symbol is not null).Collect();

                // merging the command info together
                var functionInfo = configPair.Combine(allConverters.Combine(attributedFunctions.Combine(nonAttributedFunctions)));
                context.RegisterSourceOutput(functionInfo, (spc, value) =>
                {
                    var assemblyName = value.Left.Left;
                    var config = value.Left.Right;

                    if (assemblyName == null)
                    {
                        return;
                    }

                    if (!config.CanGenerateCode(assemblyName))
                    {
                        return;
                    }

                    var converters = Merge(value.Right.Left.Left, value.Right.Left.Right).ToImmutableArray();
                    var mergedFunctions = Merge(value.Right.Right.Left, value.Right.Right.Right);
                    RunFunctions(spc, assemblyName!, this.GetType().Assembly.GetName().Version.ToString(), config, converters, mergedFunctions, logger);
                });

                // collecting all runtime attributed commands
                var runtimeAttributedCommands = context.SyntaxProvider.ForAttributeWithMetadataName("Yarn.Unity.YarnCommandAttribute",
                        predicate: (_,_) => true,
                        transform: (ctx, _) => CreateRunTimeActionFromAttribute(ctx, ActionType.Command, logger)
                ).Collect();
                // collecting all runtime attributed functions
                var runtimeAttributedFunctions = context.SyntaxProvider.ForAttributeWithMetadataName("Yarn.Unity.YarnFunctionAttribute",
                        predicate: (_,_) => true,
                        transform: (ctx, _) => CreateRunTimeActionFromAttribute(ctx, ActionType.Function, logger)
                ).Collect();
                // merging the two runtime attributed collection together
                var runtimeAttributedActions = runtimeAttributedCommands.Combine(runtimeAttributedFunctions);
                
                // generating the reflection based linking code for these
                context.RegisterSourceOutput(configPair.Combine(runtimeAttributedActions), (spc, value) =>
                {
                    var assemblyName = value.Left.Left;
                    if (assemblyName == null)
                    {
                        return;
                    }
                    var config = value.Left.Right;

                    if (!config.CanGenerateCode(assemblyName))
                    {
                        return;
                    }

                    // we have no actions so no need to do any more work
                    if (value.Right.Left.Length + value.Right.Right.Length == 0)
                    {
                        return;
                    }

                    var allActions = Merge(value.Right.Left, value.Right.Right);
                    var code = RuntimeLinkerSyntaxBuilder.BuildSyntax(allActions, assemblyName, this.GetType().Assembly.GetName().Version.ToString());
                    if (code != null)
                    {
                        FileDebugWriter.WriteGeneratedFile(code, $"{assemblyName}.runtime.linker.g.cs", config);
                        spc.AddSource($"{assemblyName}.runtime.linker.g.cs", code);
                    }
                });

                // gobbling up any yarnenum attributed enums
                var attributedYarnEnums = context.SyntaxProvider.ForAttributeWithMetadataName("Yarn.Unity.Attributes.YarnGeneratedEnumAttribute",
                        predicate: (node,_) => node is EnumDeclarationSyntax,
                        transform: (ctx, _) => GetAttributedEnums(ctx, logger)
                ).Collect();
                
                var enumInfo = configPair.Combine(attributedYarnEnums.Combine(attributedconverters));
                // now we generate the code for them
                context.RegisterSourceOutput(enumInfo, (spc, value) =>
                {
                    var assemblyName = value.Left.Left;
                    var config = value.Left.Right;
                    if (assemblyName is null)
                    {
                        return;
                    }

                    if (!config.CanGenerateCode(assemblyName))
                    {
                        return;
                    }

                    var code = RuntimeSyntaxBuilder.BuildSyntax(value.Right.Left, value.Right.Right, assemblyName, this.GetType().Assembly.GetName().Version.ToString());
                    if (code != null)
                    {
                        FileDebugWriter.WriteGeneratedFile(code, $"{assemblyName}.runtime.converter.g.cs", config);
                        spc.AddSource($"{assemblyName}.runtime.converter.g.cs", code);
                    }
                });
            }
            catch (System.Exception ex)
            {
                EmergencyLogger.ExceptionLog(ex, null, true);
                logger?.WriteException(ex);
            }
        }

        internal record class YarnEnumPayload(Shared.YarnEnum.BackingType Backing, Shared.NamedType NamedType){}

        private YarnEnumPayload? GetAttributedEnums(GeneratorAttributeSyntaxContext context, ILogger? logger)
        {
            logger ??= NullLogger.Default;

            // getting the bakcing type of the enum
            // if any of these steps fail we bail out as we won't be able to resolve the enum
            var attribute = context.Attributes.Where(a => a.ConstructorArguments.Length == 1).FirstOrDefault();
            if (attribute == null)
            {
                logger.WriteLine("The enum attribute is somehow null");
                return null;
            }

            var backingObject = attribute.ConstructorArguments.First().Value;
            if (backingObject == null)
            {
                logger.WriteLine("It's lacking the backing type");
                return null;
            }

            var backing = YarnEnum.BackingType.Int;
            if (backingObject.GetType() == typeof(int))
            {
                var backingValue = (int)backingObject;
                
                if (backingValue == 1)
                {
                    backing = YarnEnum.BackingType.String;
                }
                logger.WriteLine($"It's a valid yarn enum with a backing of : {backing}");
            }
            else
            {
                logger.WriteLine($"It has a backing type but it isn't an int and as such not an enum: {backingObject.GetType()}");
                return null;
            }
            
            // this will always pass because an earlier filter step has ensured that we are operating only on EnumDeclarationSyntax nodes
            // but the compiler can't know that this is the case so we just force it to be so
            if (context.TargetSymbol is not INamedTypeSymbol nt)
            {
                logger.WriteLine("attribute is somehow not attached to a named type!");
                return null;
            }
            var ynt = nt.YarnNamedType();
            if (ynt is null)
            {
                logger.WriteLine("Was unable to create a yarn named type for this enum!");
                return null;
            }

            return new YarnEnumPayload(backing, ynt);
        }

        private static List<T> Merge<T>(ImmutableArray<T?> right, ImmutableArray<T?> left)
        {
            List<T> merges = [];
            foreach (var item in right)
            {
                if (item is not null)
                {
                    merges.Add(item);
                }
            }
            foreach (var item in left)
            {
                if (item is not null)
                {
                    merges.Add(item);
                }
            }
            return merges;
        }

        private static void RunCommands(SourceProductionContext context, string assembly, string version, AnalysisConfiguration configuration, ImmutableArray<YarnConverter> converters, List<Action> commands, ILogger? logger)
        {
            logger ??= NullLogger.Default;

            try
            {
                var validConverters = Validators.ValidateConverters(converters, logger);
                logger.WriteLine($"after validation have {validConverters.Length} valid converters remaining");
                var validCommands = ValidateActions(commands, validConverters, logger);
                logger.WriteLine($"after validation have {validCommands.Length} valid commands remaining");

                if (validCommands.Length == 0)
                {
                    return;
                }
                var code = CompileTimeSyntaxBuilder.BuildSyntaxStringForCommands(assembly, version, validCommands, validConverters, logger);
                if (code != null)
                {
                    logger.WriteLine("generating command code");
                    FileDebugWriter.WriteGeneratedFile(code, $"{assembly}.commands.invoker.g.cs", configuration);
                    context.AddSource($"{assembly}.commands.invoker.g.cs", code);
                }
            }
            catch (System.Exception ex)
            {
                EmergencyLogger.ExceptionLog(ex, null, true);
            }
        }
        private static void RunFunctions(SourceProductionContext context, string assembly, string version, AnalysisConfiguration configuration, ImmutableArray<YarnConverter> converters, List<Action> functions, ILogger? logger)
        {
            logger ??= NullLogger.Default;

            try
            {
                logger.WriteLine("Validating converters");
                var validconverters = Validators.ValidateConverters(converters, logger);
                logger.WriteLine("Validated converters");
                logger.WriteLine("Validating actions");
                var validFunctions = ValidateActions(functions, validconverters);
                logger.WriteLine("Validated actions");

                if (validFunctions.Length == 0)
                {
                    logger.WriteLine("Aborting due to no actions");
                    return;
                }

                logger.WriteLine("beginning building code");
                var code = CompileTimeSyntaxBuilder.BuildSyntaxStringForFunctions(assembly, version, validFunctions, validconverters, logger);

                logger.WriteLine("finished building code");
                if (code != null)
                {
                    logger.WriteLine("generating function code");
                    FileDebugWriter.WriteGeneratedFile(code, $"{assembly}.functions.invoker.g.cs", configuration);
                    context.AddSource($"{assembly}.functions.invoker.g.cs", code);
                }
                logger.Dec();
            }
            catch (System.Exception ex)
            {
                EmergencyLogger.ExceptionLog(ex, null, true);
            }
        }

        private static Action? CreateActionFromSyntax(GeneratorSyntaxContext context, ActionType actionType, ILogger? logger)
        {
            logger ??= NullLogger.Default;

            if (context.Node is not InvocationExpressionSyntax invocation)
            {
                return null;
            }
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return null;
            }
            
            switch (actionType)
            {
                case ActionType.Command:
                    if (methodSymbol.Name != "AddCommandHandler")
                    {
                        return null;
                    }
                    break;
                case ActionType.Function:
                    if (methodSymbol.Name != "AddFunction")
                    {
                        return null;
                    }
                    break;
                default:
                    return null;
            }

            if (!(methodSymbol.ReceiverType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Yarn.Unity.DialogueRunner" ||
                methodSymbol.ReceiverType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Yarn.Unity.ActionRegistrationExtension" || 
                methodSymbol.ReceiverType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::Yarn.Unity.IActionRegistration"))
            {
                return null;
            }

            logger.WriteLine($"registration has {invocation.ArgumentList.Arguments.Count} args");
            // just quickly logging these out
            logger.Inc();
            foreach (var arg in invocation.ArgumentList.Arguments)
            {
                logger.WriteLine($"- {arg}");
            }
            logger.Dec();

            if (context.SemanticModel.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression).Value is not string yarnName)
            {
                logger.WriteLine($"Unable to get the name of the method: {invocation.ToFullString()}");
                return null;
            }

            // ok so once I have the yarn name I need to get the methodsymbol of the second parameter
            if (context.SemanticModel.GetSymbolInfo(invocation.ArgumentList.Arguments[1].Expression).Symbol is not IMethodSymbol actionSymbol)
            {
                logger.WriteLine($"Unable to get the method itself: {invocation.ToFullString()}");
                return null;
            }

            var nameLocation = invocation.ArgumentList.Arguments[0].GetLocation();
            var invocationLocation = invocation.ArgumentList.Arguments[1].GetLocation();

            var action = Creators.ActionFromMethodSymbol(actionSymbol, yarnName, actionType, DeclarationType.DirectRegistration, out _, true, false, null, nameLocation, invocationLocation, logger);
            return action;
        }

        private static (Action? action, Validators.ActionValidation validation) CreateActionFromAttribute(GeneratorAttributeSyntaxContext context, ActionType actionType, bool earlyOut, ILogger? logger)
        {
            logger ??= NullLogger.Default;

            try
            {
                if (context.TargetSymbol is not IMethodSymbol method)
                {
                    logger.WriteLine("the method is null?!");
                    return (null, Validators.ActionValidation.FailedValidation);
                }
                var methodName = method.Name;

                logger.WriteLine($"Collecting {methodName} as a {actionType}");

                // we are an attributed method with means we must have a YarnCommand attribute
                // but we might have multiple
                // regardless we find the first one that has a constructer value (aka a command name)
                // and assume that one to be the name we want
                // if none have a name we use the name of the method itself.
                if (context.Attributes.Where(a => a.ConstructorArguments.Length == 1).First().ConstructorArguments.First().Value is not string yarnName)
                {
                    yarnName = methodName;
                }

                // need to get the location of the attribute
                Location? attributeLocation = context.Attributes.Where(a => a.ConstructorArguments.Length == 1).FirstOrDefault(a => a.ConstructorArguments.FirstOrDefault().Value is string)?.ApplicationSyntaxReference?.GetSyntax().GetLocation();
                var result = Creators.ValidActionFromMethodSymbol(method, yarnName, actionType, DeclarationType.Attribute, null, out _, earlyOut, false, attributeLocation, null, logger);
                logger.WriteLine($"{methodName} finished validation: {result.validation}");
                return result;
            }
            catch (System.Exception ex)
            {
                EmergencyLogger.ExceptionLog(ex, null, true);
                logger.WriteException(ex);
                throw;
            }
            finally
            {
                logger.Dec();
                logger.WriteLine("done creating actions from attribute");
            }
        }

        private static Action? CreateCompileTimeActionsFromAttribute(GeneratorAttributeSyntaxContext context, ActionType actionType, ILogger? logger)
        {
            var result = CreateActionFromAttribute(context, actionType, true, logger);
            if (result.validation == Validators.ActionValidation.CompileTimeValid)
            {
                return result.action;
            }
            else
            {
                return null;
            }
        }
        private static Action? CreateRunTimeActionFromAttribute(GeneratorAttributeSyntaxContext context, ActionType actionType, ILogger? logger)
        {
            var (action, validation) = CreateActionFromAttribute(context, actionType, false, logger);
            if (validation == Validators.ActionValidation.RunTimeValid)
            {
                return action;
            }
            else
            {
                return null;
            }
        }

        // ok so what is a converter?
        // it's basically an attribute that has a type T
        // and a method to call
        // that method needs to be public in a public class
        // return a bool
        // have two parameters
            // a string
            // a type that is the same as T
        // the second parameter needs to be an out
        private static YarnConverter? GetAttributedConverters(GeneratorAttributeSyntaxContext context, ILogger? logger)
        {
            logger ??= NullLogger.Default;

            List<INamedTypeSymbol?> resolvedTypeSymbols = [];

            var attributeConstructor = context.Attributes.Where(a => a.ConstructorArguments.Length == 1).First().ConstructorArguments.First();

            var attributeValue = attributeConstructor.Value;
            if (attributeValue == null)
            {
                logger.WriteLine("attribute constructor null");
                return null;
            }
            var attributeType = attributeConstructor.Type;
            if (attributeType == null)
            {
                logger.WriteLine("attribute has no type?!");
                return null;
            }

            if (attributeType.Kind == SymbolKind.NamedType)
            {
                logger.WriteLine($"Resolved the attribute as type value: {attributeValue}");
                var resolvedTypeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(attributeValue.ToString());
                if (resolvedTypeSymbol == null)
                {
                    logger.WriteLine("despite identifying the symbol was unable to get a type from the compilation");
                }

                var method = context.TargetSymbol as IMethodSymbol;
                if (resolvedTypeSymbol != null && method != null)
                {
                    var converter = Creators.ConverterFromMethodAndType(resolvedTypeSymbol, method);
                    if (Validators.TryValidateConverter(method, resolvedTypeSymbol, null, out _, true, logger))
                    {
                        return converter;
                    }
                }
            }

            logger.WriteLine($"Failed to resolve the attribute: {attributeType.Kind}:{attributeType}:{attributeValue}");
            return null;
        }

        private static ImmutableArray<Action> ValidateActions(List<Action> actions, ImmutableArray<YarnConverter> converters, ILogger? logger = null)
        {
            logger?.WriteLine($"Beginning Validation, checking {actions.Count} commands");
            
            var validActions = new List<Action>();
            HashSet<string> namedActions = new();

            foreach (var action in actions)
            {
                if (action == null)
                {
                    logger?.WriteLine("action is null?!");
                    continue;
                }

                if (action is InvalidAction)
                {
                    logger?.WriteLine($"{action.Name} is an invalid action, skipping validation but keeping it in the list");
                    validActions.Add(action);
                    continue;
                }

                // ok now that we have converters available am able to run the converter aware validation
                if (Action.TryValidateAction(action, converters, logger))
                {
                    if (namedActions.Add(action.Name))
                    {
                        validActions.Add(action);
                    }
                    else
                    {
                        logger?.WriteLine($"{action.Name} is a duplicate");
                    }
                }
            }

            return validActions.ToImmutableArray();
        }
    }
}
namespace System.Runtime.CompilerServices { internal static class IsExternalInit {} }
