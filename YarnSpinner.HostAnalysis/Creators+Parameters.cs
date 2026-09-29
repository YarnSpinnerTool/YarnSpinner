#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

public static partial class Creators
{
    // parameters need to be
        // string
        // numeric type
        // boolean
        // a cancellation token
        // something we know how to convert
    // parameter cannot be:
        // an out
        // null
    // parameter can be:
        // an array
            // as long as it's the last parameter
            // the array is made up of yarnable types
        // a default valued type
    public static bool TryCreateNewParameters(ImmutableArray<IParameterSymbol> parameterSymbols, string methodName, Location methodLocation, AnalysisConfiguration? configuration, out Parameter[] parameters, out List<Diagnostic> diagnostics, bool earlyOut = false, ILogger? logger = null)
    {
        parameters = new Parameter[parameterSymbols.Length];
        diagnostics = new();

        if (configuration == null)
        {
            configuration = new AnalysisConfiguration(false, false, false);
        }

        // ok gonna flip this so I calculate the last token first
        // then if a parameter is an array but has a token param after it it is still ok
        for (int i = parameterSymbols.Length -1; i > -1; i--)
        {
            ILogger perParamLogger;
            if (logger == null)
            {
                perParamLogger = NullLogger.Default;
            }
            else
            {
                perParamLogger = logger;
            }

            var p = parameterSymbols[i];
            if (p == null)
            {
                perParamLogger.WriteLine($"{i}th parameter is null?!");
                if (earlyOut)
                {
                    return false;
                }
                var diag = configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1000InternalErrorProcessingAction, methodLocation, $"{methodName} parameter at {i}is null somehow.");
                diagnostics.Add(diag);
                continue;
            }

            var name = p.Name;
            perParamLogger.WriteLine($"Processing: {name} as a parameter");
            perParamLogger.Inc();

            var location = p.Locations.First();
            var isOut = p.RefKind == RefKind.Out;

            // there is one special case we care about, if we are an array
            // in which case we need the element type
            // otherwise we just want the type as it is
            INamedTypeSymbol? typeSymbol;
            bool isArray = false;
            if (p.Type is IArrayTypeSymbol arrayTypeSymbol)
            {
                isArray = true;
                typeSymbol = arrayTypeSymbol.ElementType as INamedTypeSymbol;
                perParamLogger.WriteLine($"{name} is an array of type: {typeSymbol?.ToDisplayString() ?? "(NULL)"}");
            }
            else
            {
                typeSymbol = p.Type as INamedTypeSymbol;
                perParamLogger.WriteLine($"{name} is not an array, it's: {typeSymbol?.ToDisplayString() ?? "(NULL)"}");
            }

            perParamLogger.Inc();
            perParamLogger.WriteLine($"Original type: {p.Type.ToDisplayString() ?? "(UNKNOWN)"}");
            perParamLogger.Dec();

            if (typeSymbol == null)
            {
                if (earlyOut)
                {
                    return false;
                }
                var diag = configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1008ActionsParameterIsAnIncompatibleType, location, name, "null");
                diagnostics.Add(diag);
                continue;
            }

            // does this parameter have a default value
            // if it does we need to capture that for later
            perParamLogger.WriteLine("checking for default values");
            string? defaultDisplay = null;
            if (p.HasExplicitDefaultValue)
            {
                perParamLogger.WriteLine($"{name} has a default value: {p.ExplicitDefaultValue?.ToString() ?? "null"}");

                // there is one special case we need to handle here
                // bools when ToString-ed come out as True/False and not true/false
                // so we want to fix that right now
                if (typeSymbol.SpecialType == SpecialType.System_Boolean)
                {
                    var value = (bool)(p.ExplicitDefaultValue ?? false);
                    defaultDisplay = value ? "true" : "false";
                }
                else
                {
                    defaultDisplay = p.ExplicitDefaultValue?.ToString();
                }
            }

            bool isNodeAttributed = false;
            string? enumSubtype = null;
            foreach (var attribute in p.GetAttributes())
            {
                // this attribute is an enum parameter
                if (attribute.AttributeClass?.Name == "YarnEnumParameterAttribute")
                {
                    // we don't bother handling the situation where there are invalid number or types of attribute parameters because there c# has our back and will have already complained
                    if (attribute.ConstructorArguments.Count() > 0)
                    {
                        var enumType = attribute.ConstructorArguments[0];
                        if (enumType.Type?.SpecialType == SpecialType.System_String)
                        {
                            enumSubtype = enumType.Value as string;
                        }
                    }
                }
                // this attribute is a node parameter
                if (attribute.AttributeClass?.Name == "YarnNodeParameterAttribute")
                {
                    isNodeAttributed = true;
                }
            }

            Parameter param;

            perParamLogger.WriteLine($"First check for {typeSymbol.ToDisplayString()} {name} is against the special type: {typeSymbol.SpecialType}");

            // if we are an enum or a basic type
            switch (typeSymbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String:
                {
                    param = new BasicParameter(name, typeSymbol.SpecialType.ToSpecialType(), isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                    break;
                }

                case SpecialType.None: // enums can sometimes appear as a none? Dunno why
                case SpecialType.System_Enum:
                {
                    // we are an enum
                    // so if we are a yarn generated enum we get special treatment
                    // otherwise we are just another unknown who might get a converter later on

                    // this means we are an enum or some other type (such as a game object...)
                    // so if we have the attribute we can assume we are actually an enum
                    // otherwise we waill just move on

                    logger?.WriteLine($"Processing {typeSymbol.ToDisplayString()} {name} as a an enum");

                    var attribute = typeSymbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "Yarn.Unity.Attributes.YarnGeneratedEnumAttribute").FirstOrDefault();
                    if (attribute == null)
                    {
                        // because we are lacking the attribute this means we could be many things
                        // we could be:
                        // - malformed yarn enum
                        // - a gameobject
                        // - a component
                        // - a converted object
                        // - something we don't know how to handle
                        // in all cases we goto the default case which can handle at least some of these situations
                        // for the rest we need to wait until we have converters to see what is what
                        perParamLogger.WriteLine("Its lacking the attribute");
                        goto default;
                    }

                    var backingObject = attribute.ConstructorArguments.First().Value;
                    if (backingObject == null)
                    {
                        perParamLogger.WriteLine("It's lacking the backing type");
                        param = new UnknownParameter(name, typeSymbol.YarnNamedType(), isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }

                    if (backingObject.GetType() == typeof(int))
                    {
                        var backingValue = (int)backingObject;
                        
                        var backing = YarnEnum.BackingType.Int;
                        if (backingValue == 1)
                        {
                            backing = YarnEnum.BackingType.String;
                        }
                        perParamLogger.WriteLine("It's a valid yarn enum!");
                        param = new EnumParameter(name, typeSymbol.ToDisplayString(), backing, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }
                    else
                    {
                        perParamLogger.WriteLine($"It has a backing type but it isn't an int and as such not an enum: {backingObject.GetType()}");
                        param = new UnknownParameter(name, typeSymbol.YarnNamedType(), isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }
                }

                default:
                {
                    var nt = typeSymbol.YarnNamedType();

                    perParamLogger.WriteLine($"short check {p.Name}: {nt.shortType}:{nt.fullyQualifiedType}:{nt.specialType}");

                    if (nt.shortType == "CancellationToken" || nt.shortType == "LineCancellationToken")
                    {
                        perParamLogger.WriteLine($"{name} is a cancellation token!");
                        param = new TokenParameter(name, nt.shortType == "LineCancellationToken", isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }

                    if (nt.fullyQualifiedType == "global::UnityEngine.GameObject")
                    {
                        perParamLogger.WriteLine($"{name} is a game object!");
                        param = new GameObjectParameter(name, nt, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }

                    if (nt.isUnityComponentType)
                    {
                        perParamLogger.WriteLine($"{name} is a component!");
                        param = new ComponentParameter(name, nt, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }
                    
                    // so at this stage we are either something we can't handle
                    // or a converted type
                    // and we don't know which yet
                    // so for now we will be kept around as an unknown and when we have converters we can clean it up
                    perParamLogger.WriteLine($"{name} is unknown!");
                    param = new UnknownParameter(name, nt, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                    break;
                }
            }

            // param is now either good to go, or is flagged as unknown for later when we have converters
            // either way we can run more validation over it

            // if we are attributed as a node we must be on a a string parameter
            if (param.IsNodeAttributed && (param is not BasicParameter || (param is BasicParameter bp && bp.SpecialType != YarnSpecialType.String)))
            {
                var diag = configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1022ActionsEnumAttributedParameterIsOfIncompatibleType, location, name, typeSymbol.ToDisplayString());
                diagnostics.Add(diag);
            }

            // if we are attributed as an enum we must be on a basic parameter
            if (param.AttributedEnumSubtype != null && param is not BasicParameter)
            {
                var diag = configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1022ActionsEnumAttributedParameterIsOfIncompatibleType, location, name, typeSymbol.ToDisplayString());
                diagnostics.Add(diag);
            }

            // if we are an array we need to be the last element
            // and can't be an array of tokens
            if (param.IsArray)
            {
                if (i != parameters.Length - 1)
                {
                    // there is one exception to arrays being the last parameter
                    // if they are second last and the last param is a token, then that is fine
                    var isAllowedToBeNotLast = (i == parameters.Length - 2) && (parameters.Length == i + 2) && (parameters[i + 1] is TokenParameter);
                    if (!isAllowedToBeNotLast)
                    {    
                        if (earlyOut)
                        {
                            logger?.Dec();
                            return false;
                        }
                        var diag = configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1007ArrayInWrongLocation, location, param.Name, i);
                        diagnostics.Add(diag);
                    }
                }

                if (param is TokenParameter)
                {
                    if (earlyOut)
                    {
                        logger?.Dec();
                        return false;
                    }
                    diagnostics.Add(configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1005ActionsParamsArraysMustBeOfYarnTypes, location, param.Name, "Cancellation Token"));
                }
            }

            // if we are a token we must be the last parameter
            if (param is TokenParameter && i != parameters.Length - 1)
            {
                if (earlyOut)
                {
                    logger?.Dec();
                    return false;
                }
                diagnostics.Add(configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1006CancellationTokenInWrongLocation, location, param.Name, i));
            }

            // parameters can't be an out value
            if (param.IsOut)
            {
                if (earlyOut)
                {
                    logger?.Dec();
                    return false;
                }
                diagnostics.Add(configuration.CreateElevatedDiagnostic(ActionDiagnostics.YS1010ParameterIsAnOut, location, param.Name));
            }
            parameters[i] = param;
        }

        return diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error) == 0;
    }
}