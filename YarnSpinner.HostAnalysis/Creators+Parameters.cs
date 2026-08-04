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
    public static bool TryCreateNewParameters(ImmutableArray<IParameterSymbol> parameterSymbols, string methodName, Location methodLocation, out Parameter[] parameters, out List<Diagnostic> diagnostics, bool earlyOut = false, ILogger? logger = null)
    {
        parameters = new Parameter[parameterSymbols.Length];
        diagnostics = new();

        for (int i = 0; i < parameterSymbols.Length; i++)
        {
            var p = parameterSymbols[i];
            if (p == null)
            {
                logger?.WriteLine($"{i}th parameter is null?!");
                if (earlyOut)
                {
                    return false;
                }
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, methodLocation, $"{methodName} parameter at {i}is null somehow."));
                continue;
            }

            var name = p.Name;
            logger?.WriteLine($"Processing: {name}");
            logger?.Inc();

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
                logger?.Write($"{name} is an array");
                typeSymbol = arrayTypeSymbol.ElementType as INamedTypeSymbol;
                logger?.WriteLine($" of type: {typeSymbol?.ToDisplayString() ?? "(NULL)"}");
            }
            else
            {
                typeSymbol = p.Type as INamedTypeSymbol;
                logger?.WriteLine($"{name} is not an array, it's: {typeSymbol?.ToDisplayString() ?? "(NULL)"}");
            }

            logger?.Inc();
            logger?.WriteLine($"Original type: {p.Type.ToDisplayString() ?? "(UNKNOWN)"}");
            logger?.Dec();

            if (typeSymbol == null)
            {
                if (earlyOut)
                {
                    return false;
                }
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1008ActionsParameterIsAnIncompatibleType, location, name, "null"));
                continue;
            }

            // does this parameter have a default value
            // if it does we need to capture that for later
            logger?.WriteLine("checking for default values");
            string? defaultDisplay = null;
            if (p.HasExplicitDefaultValue)
            {
                logger?.WriteLine($"{name} has a default value: {p.ExplicitDefaultValue?.ToString() ?? "null"}");

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

            logger?.WriteLine($"First check for {typeSymbol.ToDisplayString()} {name} is against the special type: {typeSymbol.SpecialType}");

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
                        logger?.WriteLine("Its lacking the attribute");
                        goto default;
                    }

                    var backingObject = attribute.ConstructorArguments.First().Value;
                    if (backingObject == null)
                    {
                        logger?.WriteLine("It's lacking the backing type");
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
                        logger?.WriteLine("It's a valid yarn enum!");
                        param = new EnumParameter(name, typeSymbol.ToDisplayString(), backing, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }
                    else
                    {
                        logger?.WriteLine($"It has a backing type but it isn't an int and as such not an enum: {backingObject.GetType()}");
                        param = new UnknownParameter(name, typeSymbol.YarnNamedType(), isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }
                }

                default:
                {
                    var nt = typeSymbol.YarnNamedType();

                    logger?.WriteLine($"short check {p.Name}: {nt.shortType}:{nt.fullyQualifiedType}:{nt.specialType}");

                    if (nt.shortType == "CancellationToken" || nt.shortType == "LineCancellationToken")
                    {
                        logger?.WriteLine($"{name} is a cancellation token!");
                        param = new TokenParameter(name, nt.shortType == "LineCancellationToken", isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }

                    if (nt.fullyQualifiedType == "global::UnityEngine.GameObject")
                    {
                        logger?.WriteLine($"{name} is a game object!");
                        param = new GameObjectParameter(name, nt, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }

                    if (nt.isUnityComponentType)
                    {
                        logger?.WriteLine($"{name} is a component!");
                        param = new ComponentParameter(name, nt, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                        break;
                    }
                    
                    // so at this stage we are either something we can't handle
                    // or a converted type
                    // and we don't know which yet
                    // so for now we will be kept around as an unknown and when we have converters we can clean it up
                    logger?.WriteLine($"{name} is unknown!");
                    param = new UnknownParameter(name, nt, isArray, isOut, p.HasExplicitDefaultValue, defaultDisplay, isNodeAttributed, enumSubtype);
                    break;
                }
            }

            // param is now either good to go, or is flagged as unknown for later when we have converters
            // either way we can run more validation over it

            // if we are attributed as a node we must be on a a string parameter
            if (param.IsNodeAttributed && (param is not BasicParameter || (param is BasicParameter bp && bp.SpecialType != YarnSpecialType.String)))
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1022ActionsEnumAttributedParameterIsOfIncompatibleType, location, name, typeSymbol.ToDisplayString()));
            }

            // if we are attributed as an enum we must be on a basic parameter
            if (param.AttributedEnumSubtype != null && param is not BasicParameter)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1022ActionsEnumAttributedParameterIsOfIncompatibleType, location, name, typeSymbol.ToDisplayString()));
            }

            // if we are an array we need to be the last element
            // and can't be an array of tokens
            if (param.IsArray)
            {
                if (i != parameters.Length - 1)
                {
                    if (earlyOut)
                    {
                        return false;
                    }
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1007ArrayInWrongLocation, location, param.Name, i));
                }

                if (param is TokenParameter)
                {
                    if (earlyOut)
                    {
                        return false;
                    }
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1005ActionsParamsArraysMustBeOfYarnTypes, location, param.Name, "Cancellation Token"));
                }
            }

            // if we are a token we must be the last parameter
            if (param is TokenParameter && i != parameters.Length - 1)
            {
                if (earlyOut)
                {
                    return false;
                }
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1006CancellationTokenInWrongLocation, location, param.Name, i));
            }

            // parameters can't be an out value
            if (param.IsOut)
            {
                if (earlyOut)
                {
                    return false;
                }
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1010ParameterIsAnOut, location, param.Name));
            }
            parameters[i] = param;
        }

        logger?.WriteLine("Finished validating and creating the parameters");

        return diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error) == 0;
    }
}