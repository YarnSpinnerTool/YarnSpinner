using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Yarn.Shared;
using Yarn.HostAnalysis;

#nullable enable

namespace Yarn.Analyser
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class Analyser: DiagnosticAnalyzer
    {
        public override void Initialize(AnalysisContext context)
        {
            // what are these and why did vscode say to add them?
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var analyser = new NestedAnalyser(compilationContext.Compilation.AssemblyName ?? "ERROR", compilationContext.Compilation.ReferencedAssemblyNames);

                compilationContext.RegisterAdditionalFileAction(analyser.AdditionalFileProcess);
                compilationContext.RegisterSymbolAction(analyser.AnalyseConverterMethodSymbols, SymbolKind.Method);
                compilationContext.RegisterSymbolAction(analyser.AnalyseAttributedActionMethodSymbols, SymbolKind.Method);
                compilationContext.RegisterSyntaxNodeAction(analyser.AnalyseActionsByInvoke, SyntaxKind.InvocationExpression);

                compilationContext.RegisterCompilationEndAction(analyser.CompilationEndAction);
            });
        }

        private class NestedAnalyser
        {
            private const string YarnConverterLongType = "Yarn.Unity.YarnConverterAttribute";
            private const string YarnCommandAttributeLongType = "Yarn.Unity.YarnCommandAttribute";
            private const string YarnFunctionAttributeLongType = "Yarn.Unity.YarnFunctionAttribute";
            private const string YarnAddCommandShortInvoke = "AddCommandHandler";
            private const string YarnAddFunctionShortInvoke = "AddFunction";
            
            private readonly List<(YarnConverter, Location)> allConverters = [];
            private readonly List<(string yarnName, Action? action, IMethodSymbol symbol, Location? nameLocation, HashSet<string> codes)> allCommands = [];
            private readonly List<(string yarnName, Action? action, IMethodSymbol symbol, Location? nameLocation, HashSet<string> codes)> allFunctions = [];

            private readonly bool referencesYS;

            public NestedAnalyser(string assemblyName, IEnumerable<AssemblyIdentity> referencedAssemblies)
            {
                referencesYS = referencedAssemblies.Any(a => a.Name == "YarnSpinner.Unity");
                logger = new BetterLogger($"{assemblyName}-analyser");
            }

            private ILogger logger;

            private string? projectRoot;

            public void AdditionalFileProcess(AdditionalFileAnalysisContext context)
            {
                if (!referencesYS)
                {
                    return;
                }

                logger.WriteLine("additional file:");
                logger.Inc();
                logger.WriteLine(context.AdditionalFile.Path);
                logger.WriteLine(context.AdditionalFile.GetText()?.ToString() ?? "NULL ADDITIONAL FILE");

                if (context.AdditionalFile.Path.EndsWith("AdditionalFile.txt"))
                {
                    projectRoot = context.AdditionalFile.GetText()?.ToString();
                }

                logger.Dec();
            }

            public void CompilationEndAction(CompilationAnalysisContext context)
            {
                if (!referencesYS)
                {
                    return;
                }

                logger.WriteLine($"beginning final step for {context.Compilation.AssemblyName}");

                // checking for duplicate converters
                HashSet<string> converterNames = new();
                foreach (var (converter, location) in allConverters)
                {
                    if (!converterNames.Add(converter.FullyQualifiedTypeName))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(ActionDiagnostics.YS1018DuplicateConverter, location, converter.StaticCallingString, converter.FullyQualifiedTypeName));
                    }
                }

                // now to also collect any referenced converters
                // we don't log any issues for these, just get access to them
                // because these will be processed by another step
                // we can't really do it any other way as far as I can see
                var a = new List<YarnConverter>(allConverters.Select(pair => pair.Item1));
                var b = Creators.CollectAssemblyConverters(context.Compilation, logger);
                a.AddRange(b);
                var converters = a.ToImmutableArray();
                
                // doing one last check of all the converters for duplicates
                // as we've added more converters into the mix it is possible we've now got duplicates that weren't previously handled
                // we can't report errors on referenced converters but we can report on our own
                foreach (var converter in b)
                {
                    if (converter == null)
                    {
                        continue;
                    }
                    if (!converterNames.Contains(converter.FullyQualifiedTypeName))
                    {
                        continue;
                    }
                    logger.WriteLine("there is a duplicate");

                    // so one of our existing converters also handles this
                    // so we find which one and then report a diagnostic on it
                    foreach (var pair in allConverters)
                    {
                        if (pair.Item1.FullyQualifiedTypeName != converter.FullyQualifiedTypeName)
                        {
                            continue;
                        }
                        logger.WriteLine("found the duplicate converter!");
                        context.ReportDiagnostic(Diagnostic.Create(ActionDiagnostics.YS1018DuplicateConverter, pair.Item2, pair.Item1.StaticCallingString, pair.Item1.FullyQualifiedTypeName));
                        break;
                    }
                }

                logger.WriteLine($"Converters for {context.Compilation.AssemblyName}");
                logger.Inc();
                foreach (var converter in converters)
                {
                    logger.WriteLine($"- {converter.FullyQualifiedTypeName}");
                }
                logger.Dec();

                List<string> commandJSON = [];
                List<string> functionJSON = [];

                ValidateActions(allCommands, converters, context);
                ValidateActions(allFunctions, converters, context);

                logger.WriteLine("done validating the actions");

                // finally we add them to the json
                foreach (var (yarnName, action, symbol, nameLocation, codes) in allCommands)
                {
                    if (action == null)
                    {
                        // if the action is null that means it failed creation in a critical way
                        // like couldn't get a valid method symbol sorta way
                        // I still need to report it, but like how?
                        logger.WriteLine("found a null command action somehow");
                    }
                    else
                    {
                        try
                        {
                            commandJSON.Add(action.ToJSON(symbol.Locations.First(), projectRoot, codes));
                        }
                        catch (System.Exception ex)
                        {
                            EmergencyLogger.ExceptionLog(ex);
                        }
                    }
                }
                logger.WriteLine("created command json");

                foreach (var (yarnName, action, symbol, nameLocation, codes) in allFunctions)
                {
                    if (action == null)
                    {
                        // if the action is null that means it failed creation in a critical way
                        // like couldn't get a valid method symbol sorta way
                        // I still need to report it, but like how?
                        logger.WriteLine("found a null function action somehow");
                    }
                    else
                    {
                        functionJSON.Add(action.ToJSON(symbol.Locations.First(), projectRoot, codes));
                    }
                }
                logger.WriteLine("created function json");

                // if we don't have any we don't need to write anything to disk
                // actually can probably early out even earlier...
                if (commandJSON.Count + functionJSON.Count == 0)
                {
                    logger.WriteLine($"{context.Compilation.AssemblyName} has no actions so skipping ysls generation");
                    return;
                }

                if (projectRoot != null)
                {
                    var ysls = "{" +
                    @"""version"":3," +
                    $@"""commands"":[{string.Join(",", commandJSON)}]," +
                    $@"""functions"":[{string.Join(",", functionJSON)}]" +
                    "}";
                    JSONWriter.WriteJSON(context.Compilation.AssemblyName ?? "ERROR", ysls, projectRoot, true);
                }
                else
                {
                    logger.WriteLine("Unable to write ysls, project root not found");
                }

                logger.WriteLine($"done with {context.Compilation.AssemblyName}");
            }

            private void ValidateActions(List<(string yarnName, Yarn.Shared.Action? action, IMethodSymbol symbol, Location? nameLocation, HashSet<string> codes)> actions, ImmutableArray<YarnConverter> converters, CompilationAnalysisContext context)
            {
                HashSet<string> duplicateIDs = [];
                foreach (var (yarnName, action, symbol, nameLocation, codes) in actions)
                {
                    if (action == null)
                    {
                        logger.WriteLine($"aborting more validation on {yarnName} as it did not create an action");

                        continue;
                    }

                    if (action is InvalidAction)
                    {
                        logger.WriteLine("action is an invalid form, this will have already reported its diagnostics so we can skip over it");
                        continue;
                    }

                    // checking for duplicates
                    if (!duplicateIDs.Add(yarnName))
                    {
                        logger.WriteLine($"found a duplicate of {yarnName} {(action.Type == ActionType.Command ? "command" : "function")}");
                        context.ReportDiagnostic(Diagnostic.Create(ActionDiagnostics.YS1019DuplicateAction, nameLocation ?? symbol.Locations.First(), yarnName, action.Type == ActionType.Command ? "command" : "function"));
                        codes.Add(ActionDiagnostics.YS1019DuplicateAction.Id);
                    }

                    // checking if the parameter is a type we can actually handle
                    logger.WriteLine($"performing validation of parameters for {yarnName}");
                    logger.Inc();
                    for (int i = 0; i < action.Parameters.Length; i++)
                    {
                        var parameter = action.Parameters[i];

                        // if we aren't unknown it means we are either already resolved, a basic type, or a token
                        // regardless we can just skip clean over this one, it's fine as an earlier step did any issue generation
                        if (parameter is not UnknownParameter up)
                        {
                            continue;
                        }

                        logger.WriteLine($"parameter {i} ({parameter.Name}:{up.Type.shortType}) failed intial validation");

                        // first we see if we are a converter
                        YarnConverter? converter = null;
                        foreach (var c in converters)
                        {
                            if (c.FullyQualifiedTypeName == up.Type.fullyQualifiedType)
                            {
                                converter = c;
                                break;
                            }
                        }

                        // we failed conversion but we might still end up being a component
                        if (converter == null)
                        {
                            logger.WriteLine($"{i} doesn't have a converter");
                            if (up.Type.isUnityComponentType)
                            {
                                logger.WriteLine("but it is a component!");
                                action.Parameters[i] = new ComponentParameter(up.Name, up.Type, up.IsArray, up.IsOut, up.HasDefaultValue, up.DefaultValueDisplay, up.IsNodeAttributed, up.AttributedEnumSubtype);
                            }
                            else
                            {
                                context.ReportDiagnostic(Diagnostic.Create(ActionDiagnostics.YS1008ActionsParameterIsAnIncompatibleType, symbol.Parameters[i].Locations.First(), parameter.Name, parameter.ShortFormType));
                            }
                        }
                        else
                        {
                            logger.WriteLine($"{i} got a converter");
                            action.Parameters[i] = new ConverterParameter(up.Name, converter, up.IsArray, up.IsOut, up.HasDefaultValue, up.DefaultValueDisplay, up.IsNodeAttributed, up.AttributedEnumSubtype);
                        }
                    }
                    logger.Dec();

                    // checking that if the method is an instance method it is one we can return a value for
                    if (action.IsInstance)
                    {
                        var containingType = Yarn.Shared.Action.isValidYarnableTypeForUnity(action.containingNamedType, converters, logger);
                        if (containingType == ParameterTypes.UnityComponent || containingType == ParameterTypes.Converter)
                        {
                            logger.WriteLine($"containing type is a valid one");
                        }
                        else
                        {
                            context.ReportDiagnostic(Diagnostic.Create(ActionDiagnostics.YS1009InstanceActionIsOnAnIncompatibleType, symbol.Locations.First(), yarnName, action.containingTypeShortName));
                            codes.Add(ActionDiagnostics.YS1009InstanceActionIsOnAnIncompatibleType.Id);
                        }
                    }
                }
            }

            public void AnalyseActionsByInvoke(SyntaxNodeAnalysisContext context)
            {
                if (!referencesYS)
                {
                    return;
                }

                if (context.Node is not InvocationExpressionSyntax syntax)
                {
                    return;
                }

                var actionType = ActionType.NotAnAction;
                if (syntax.Expression is SimpleNameSyntax sn)
                {
                    if (!(sn.Identifier.ValueText == YarnAddCommandShortInvoke || sn.Identifier.ValueText == YarnAddFunctionShortInvoke))
                    {
                        return;
                    }
                    actionType = sn.Identifier.ValueText == YarnAddCommandShortInvoke ? ActionType.Command : ActionType.Function;
                }
                else if (syntax.Expression is MemberAccessExpressionSyntax ma)
                {
                    if (!(ma.Name.Identifier.ValueText == YarnAddCommandShortInvoke || ma.Name.Identifier.ValueText == YarnAddFunctionShortInvoke))
                    {
                        return;
                    }
                    actionType = ma.Name.Identifier.ValueText == YarnAddCommandShortInvoke ? ActionType.Command : ActionType.Function;
                }

                // ok at this stage it is worth getting the methodSymbol and going further
                if (context.SemanticModel.GetSymbolInfo(syntax).Symbol is not IMethodSymbol invocationSymbol)
                {
                    return;
                }

                // is this called on a runner/register?
                var receiver = invocationSymbol.ReceiverType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!(receiver == "global::Yarn.Unity.DialogueRunner" || receiver == "global::Yarn.Unity.ActionRegistrationExtension" || receiver == "global::Yarn.Unity.IActionRegistration"))
                {
                    logger.WriteLine($"{syntax} is not a a runner");
                    return;
                }

                logger.WriteLine($"{syntax.ToString()} is worth investigating further as {actionType}");

                // we need two parameters, right?
                // we don't report this because the compiler will catch it, so we can assume it's a fine invocation on our end otherwise
                if (syntax.ArgumentList.Arguments.Count() != 2)
                {
                    logger.WriteLine($"call to {invocationSymbol.Name} has incorrect number of parameter: {syntax.ArgumentList.Arguments.Count()}");
                    return;
                }

                // the second is a method
                if (context.SemanticModel.GetSymbolInfo(syntax.ArgumentList.Arguments[1].Expression).Symbol is not IMethodSymbol methodSymbol)
                {
                    logger?.WriteLine($"Unable to get the method itself: {syntax.ToFullString()}");
                    return;
                }

                logger.WriteLine($"invocation is a {methodSymbol.MethodKind}");

                HashSet<string> diagnosticCodes = [];

                // the first is a constant string
                Location nameLocation = syntax.ArgumentList.Arguments[0].GetLocation();
                if (context.SemanticModel.GetConstantValue(syntax.ArgumentList.Arguments[0].Expression).Value is not string yarnName)
                {
                    context.ReportDiagnostic(Diagnostic.Create(ActionDiagnostics.YS1020UnableToResolveActionName, nameLocation));
                    diagnosticCodes.Add(ActionDiagnostics.YS1020UnableToResolveActionName.Id);
                    yarnName = methodSymbol.Name;
                }

                var invocationLocation = syntax.ArgumentList.Arguments[1].GetLocation();

                // this generates our action and also makes the diganostics for it
                // excluding converter diagnostics which need to happen at a later stage
                var action = Creators.ActionFromMethodSymbol(methodSymbol, yarnName, actionType, DeclarationType.DirectRegistration, out var diagnostics, false, nameLocation, invocationLocation, logger);

                foreach (var diag in diagnostics)
                {
                    context.ReportDiagnostic(diag);
                    diagnosticCodes.Add(diag.Id);
                }

                if (actionType == ActionType.Command)
                {    
                    lock (allCommands)
                    {
                        allCommands.Add((yarnName, action, methodSymbol, nameLocation, diagnosticCodes));
                    }
                }
                else
                {
                    lock (allFunctions)
                    {
                        allFunctions.Add((yarnName, action, methodSymbol, nameLocation, diagnosticCodes));
                    }
                }
            }

            public void AnalyseAttributedActionMethodSymbols(SymbolAnalysisContext context)
            {
                if (!referencesYS)
                {
                    return;
                }

                logger.WriteLine($"Beginning action checking in {context.Compilation.AssemblyName}");
                logger.Inc();

                if (context.Symbol is not IMethodSymbol methodSymbol)
                {
                    logger.WriteLine("method is null?!");
                    logger.Dec();
                    return;
                }

                var actionAttributed = methodSymbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == YarnCommandAttributeLongType || a.AttributeClass?.ToDisplayString() == YarnFunctionAttributeLongType);
                if(!actionAttributed.Any())
                {
                    logger.WriteLine($"method: {methodSymbol.Name} is not action attributed");
                    logger.Dec();
                    return;
                }

                var methodName = methodSymbol.Name;
                logger.WriteLine($"Collecting {methodName}");

                // we are an attributed method with means we must have a YarnCommand attribute
                // but we might have multiple
                // regardless we find the first one that has a constructer value (aka a command name)
                // and assume that one to be the name we want
                // if none have a name we use the name of the method itself.

                Location? nameLocation = null;
                if (actionAttributed.First(a => a.ConstructorArguments.Length == 1).ConstructorArguments.First().Value is string yarnName)
                {
                    logger.WriteLine($"{methodName} has a custom label");
                    nameLocation = actionAttributed.First(a => a.ConstructorArguments.Length == 1)?.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? methodSymbol.Locations.First();
                }
                else
                {
                    logger.WriteLine($"{methodName} does not has a custom label");
                    yarnName = methodName;
                }

                var actionType = actionAttributed.Any(a => a.AttributeClass?.ToDisplayString() == YarnCommandAttributeLongType) ? ActionType.Command : ActionType.Function;

                HashSet<string> diagnosticCodes = [];
                var action = Creators.ActionFromMethodSymbol(methodSymbol, yarnName, actionType, DeclarationType.Attribute, out var diagnostics, false, nameLocation, null, logger);
                foreach (var diag in diagnostics)
                {
                    context.ReportDiagnostic(diag);
                    diagnosticCodes.Add(diag.Id);
                }

                if (actionType == ActionType.Command)
                {
                    logger.WriteLine($"{methodName} added to commands");
                    lock (allCommands)
                    {
                        allCommands.Add((yarnName, action, methodSymbol, nameLocation, diagnosticCodes));
                    }
                }
                else
                {
                    logger.WriteLine($"{methodName} added to functions");
                    lock (allFunctions)
                    {
                        allFunctions.Add((yarnName, action, methodSymbol, nameLocation, diagnosticCodes));
                    }
                }

                logger?.Dec();
            }

            public void AnalyseConverterMethodSymbols(SymbolAnalysisContext context)
            {
                if (!referencesYS)
                {
                    return;
                }

                logger.WriteLine($"Beginning converter in {context.Compilation.AssemblyName}");
                logger.Inc();

                if (context.Symbol is not IMethodSymbol methodSymbol)
                {
                    logger.WriteLine("method is null?!");
                    logger.Dec();
                    return;
                }

                if (methodSymbol.GetAttributes().Length == 0)
                {
                    logger.WriteLine($"{methodSymbol.Name} has no attributes");
                    logger.Dec();
                    return;
                }

                if (!methodSymbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == YarnConverterLongType))
                {
                    logger.WriteLine($"{methodSymbol.Name} doesn't have the converter attribute");
                    logger.Dec();
                    return;
                }

                var attributeConstructor = methodSymbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == YarnConverterLongType).First(a => a.ConstructorArguments.Length == 1).ConstructorArguments.First();
                var attributeValue = attributeConstructor.Value;
                if (attributeValue == null)
                {
                    logger.WriteLine("attribute constructor null");
                    logger.Dec();
                    return;
                }
                var attributeType = attributeConstructor.Type;
                if (attributeType == null)
                {
                    logger.WriteLine("attribute has no type?!");
                    logger.Dec();
                    return;
                }

                var resolvedTypeSymbol = context.Compilation.GetTypeByMetadataName(attributeValue.ToString());
                if (resolvedTypeSymbol == null)
                {
                    logger.WriteLine("despite identifying the symbol was unable to get a type from the compilation");
                    logger.Dec();
                    return;
                }

                logger.WriteLine("found a converter, beginning validation");
                Validators.TryValidateConverter(methodSymbol, resolvedTypeSymbol, out var diags, false, logger);
                
                var converter = Creators.ConverterFromMethodAndType(resolvedTypeSymbol, methodSymbol);
                lock (allConverters)
                {
                    allConverters.Add((converter, methodSymbol.Locations.First()));
                }
                foreach (var diag in diags)
                {
                    context.ReportDiagnostic(diag);
                }

                logger.Dec();
            }
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        {
            get
            {
                return ImmutableArray.Create(
                    ActionDiagnostics.YS1000InternalErrorProcessingAction,
                    ActionDiagnostics.YS1001ActionMethodsMustBePublic,
                    ActionDiagnostics.YS1002ActionMethodsMustHaveAValidName,
                    ActionDiagnostics.YS1003CommandMethodsMustHaveAValidReturnType,
                    ActionDiagnostics.YS1004FunctionMethodsMustHaveAValidReturnType,
                    ActionDiagnostics.YS1005ActionsParamsArraysMustBeOfYarnTypes,
                    ActionDiagnostics.YS1006CancellationTokenInWrongLocation,
                    ActionDiagnostics.YS1007ArrayInWrongLocation,
                    ActionDiagnostics.YS1008ActionsParameterIsAnIncompatibleType,
                    ActionDiagnostics.YS1009InstanceActionIsOnAnIncompatibleType,
                    ActionDiagnostics.YS1010ParameterIsAnOut,
                    ActionDiagnostics.YS1011ConverterMethodIsNotStatic,
                    ActionDiagnostics.YS1012ConverterMethodIsNotPublic,
                    ActionDiagnostics.YS1013ConverterReturnsInvalidType,
                    ActionDiagnostics.YS1014IncorrectNumberOfConverterParameters,
                    ActionDiagnostics.YS1015ConverterHasInvalidInputParameter,
                    ActionDiagnostics.YS1016ConverterMissingOutParam,
                    ActionDiagnostics.YS1017ConverterTypeMismatch,
                    ActionDiagnostics.YS1018DuplicateConverter,
                    ActionDiagnostics.YS1019DuplicateAction,
                    ActionDiagnostics.YS1020UnableToResolveActionName,
                    ActionDiagnostics.YS1021ActionIsALambda,
                    ActionDiagnostics.YS1022ActionsEnumAttributedParameterIsOfIncompatibleType,
                    ActionDiagnostics.YS1023ActionsNodeAttributedParameterIsOfIncompatibleType,
                    ActionDiagnostics.YS1024ActionIsALocalFunction,
                    ActionDiagnostics.YS1025DirectActionIsPrivate,
                    ActionDiagnostics.YS1026FunctionUsesMetaToken
                );
            }
        }
    }
}
