#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

public static class Validators
{
    public enum ActionValidation
    {
        // failed in a way where it doesn't matter, there is nothing more we can from it
        FailedUnrecoverably,
        // Failed but in a way where we want the invoker to intercept the call
        FailedStatically,
        // Failed in a way where we don't want the invoker to intercept the call
        FailedDynamically,
        // Passed validation
        Succeeded,
    }

    public static ActionValidation TryValidateMethodAsAction(IMethodSymbol methodSymbol, string yarnName, ActionType type, DeclarationType declarationType, out List<Diagnostic> diagnostics, Location? nameLocation = null, Location? invocationLocation = null, bool earlyOut = false, ILogger? logger = null)
    {
        diagnostics = new List<Diagnostic>();
        var location = methodSymbol.Locations.First();
        // ok so basically I need to do the validation NOW

        if (yarnName.Any(x => char.IsWhiteSpace(x)))
        {
            logger?.WriteLine("Method name is invalid");
            if (earlyOut)
            {
                return ActionValidation.FailedStatically;
            }
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1002ActionMethodsMustHaveAValidName, nameLocation ?? location, yarnName));
        }

        if (methodSymbol.ContainingType == null)
        {
            logger?.WriteLine("Method has no containing type");
            if (earlyOut)
            {
                return ActionValidation.FailedStatically;
            }
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, location, $"Was unable to resolve the containing type of the method {methodSymbol.Name}"));
        }

        // if it is a lambda that is fine (mostly) but generates it's own diagnostic
        if (methodSymbol.MethodKind == MethodKind.AnonymousFunction)
        {
            logger?.WriteLine($"{yarnName} on at {location.GetLineSpan().StartLinePosition} is a lambda, this is also dodge");
            // we can't process lamdbas in the source gen as we can't call them
            // so we can early out here and prevent any more processing
            if (earlyOut)
            {
                return ActionValidation.FailedDynamically;
            }
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1021ActionIsALambda, invocationLocation ?? location));
        }
        // likewise if it is a local method that is mostly fine, but also generates it's own diagnostic
        if (methodSymbol.MethodKind == MethodKind.LocalFunction)
        {
            logger?.WriteLine($"{yarnName} on {methodSymbol.Name} at {location.GetLineSpan().StartLinePosition} is a local method, this is dodge");
            // we can't process local methods in the source gen as we can't call them
            // so we can early out here and prevent any more processing
            if (earlyOut)
            {
                return ActionValidation.FailedDynamically;
            }
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1024ActionIsALocalFunction, invocationLocation ?? location));
        }

        var returnType = methodSymbol.ReturnType.UnityReturnType();
        if (type == ActionType.Command)
        {
            switch (returnType)
            {
                case ReturnType.Void:
                case ReturnType.AsyncVoid:
                case ReturnType.CoroutineVoid:
                case ReturnType.IEnumeratorVoid:
                    break;
                
                default:
                {
                    logger?.WriteLine("Method has an invalid return");
                    if (earlyOut)
                    {
                        return ActionValidation.FailedStatically;
                    }
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1003CommandMethodsMustHaveAValidReturnType, location, yarnName, returnType));
                    break;
                }
            }
        }
        else if (type == ActionType.Function)
        {
            // functions must return a yarn type or one of the async yarn types
            switch (returnType)
            {
                case ReturnType.String:
                case ReturnType.Number:
                case ReturnType.Boolean:
                case ReturnType.AsyncString:
                case ReturnType.AsyncNumber:
                case ReturnType.AsyncBoolean:
                    break;

                default:
                {
                    logger?.WriteLine("Method has an invalid return");
                    if (earlyOut)
                    {
                        return ActionValidation.FailedStatically;
                    }
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1004FunctionMethodsMustHaveAValidReturnType, location, yarnName, returnType));
                    break;
                }
            }
        }
        else
        {
            logger?.WriteLine("Method isn't a function or command");
            if (earlyOut)
            {
                return ActionValidation.FailedUnrecoverably;
            }
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, location, $"{yarnName} is an invalid form of action. This isn't allowed."));
        }

        // if an action is registered via attribute it must be public
        var methodIsPublic = methodSymbol.DeclaredAccessibility == Accessibility.Public;
        var classIsPublic = methodSymbol.ContainingType?.DeclaredAccessibility == Accessibility.Public;
        if (!(methodIsPublic && classIsPublic))
        {
            // the method is private
            // this may or may not be an issue depending on if we are an attributed method
            // if we are then it's a diag
            // if we aren't it's not
            // in both cases it's an early out
            if (declarationType == DeclarationType.Attribute)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1001ActionMethodsMustBePublic, location, yarnName, methodSymbol.DeclaredAccessibility));
                if (earlyOut)
                {
                    return ActionValidation.FailedStatically;
                }
            }
            else
            {
                if (earlyOut)
                {
                    return ActionValidation.FailedDynamically;
                }
                // we do however still issue an issue level diagnostic if this is an instance method
                // because there are some quirks around doing this that aren't immediately obvious
                if (!methodSymbol.IsStatic && methodSymbol.MethodKind == MethodKind.Ordinary)
                {
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1025DirectActionIsPrivate, invocationLocation));
                }
            }

            logger?.WriteLine("Method isn't public");
            if (earlyOut)
            {
                // this isn't a critical error as it can still be called as a delegate it is still something that prevents it being used in the direct invocation approach
                // so if early out is set we still leave at this point because we can't do any more in the source generator with it at this point
                return ActionValidation.FailedDynamically;
            }

            if (declarationType == DeclarationType.Attribute)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1001ActionMethodsMustBePublic, location, yarnName, methodSymbol.DeclaredAccessibility));
            }
        }

        if (diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error) == 0)
        {
            return ActionValidation.Succeeded;
        }
        else
        {
            return ActionValidation.FailedStatically;
        }
    }

    public static bool TryValidateConverter(IMethodSymbol? conversionMethodSymbol, INamedTypeSymbol? attributedConversionType, out List<Diagnostic> diagnostics, bool earlyOut = false, ILogger? logger = null)
    {
        diagnostics = new();
        if (conversionMethodSymbol == null)
        {
            logger?.WriteLine("converter has no method, this is impossible?");

            if (!earlyOut)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, null, $"Method symbol for a converter is null, this should be impossible and prevents further validation."));
            }

            return false;
        }
        var methodName = conversionMethodSymbol.Name;
        logger?.WriteLine($"Performing converter validation on {methodName}");

        // method must be static
        if (!conversionMethodSymbol.IsStatic)
        {
            logger?.WriteLine("converter method isn't static");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1011ConverterMethodIsNotStatic, conversionMethodSymbol.Locations.First(), methodName));
            }
        }

        // method must be public
        if (conversionMethodSymbol.DeclaredAccessibility != Accessibility.Public && conversionMethodSymbol.ContainingType.DeclaredAccessibility != Accessibility.Public)
        {
            logger?.WriteLine("converter method isn't accessible");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1012ConverterMethodIsNotPublic, conversionMethodSymbol.Locations.First(), methodName));
            }
        }

        // method must return bool
        if (conversionMethodSymbol.ReturnType.SpecialType != SpecialType.System_Boolean)
        {
            logger?.WriteLine($"converter doesn't return bool: {conversionMethodSymbol.ReturnType.ToDisplayString()}");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1013ConverterReturnsInvalidType, conversionMethodSymbol.ReturnType.Locations.First(), methodName, conversionMethodSymbol.ReturnType.ToDisplayString()));
            }
        }

        // method needs exactly two parameters
        if (conversionMethodSymbol.Parameters.Length != 2)
        {
            logger?.WriteLine($"converter doesn't have two parameters {conversionMethodSymbol.Parameters.Length}");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1014IncorrectNumberOfConverterParameters, conversionMethodSymbol.Locations.First(), methodName, conversionMethodSymbol.Parameters.Length));
            }
        }

        // method must have a string as the first parameter
        if (conversionMethodSymbol.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            logger?.WriteLine($"converter's first parameter isn't a string");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1015ConverterHasInvalidInputParameter, conversionMethodSymbol.Parameters[0].Locations.First(), methodName, conversionMethodSymbol.Parameters[0].Type.ToDisplayString()));
            }
        }

        var secondParameter = conversionMethodSymbol.Parameters[1];
        // method must have an out as the second parameter
        if (secondParameter.RefKind != RefKind.Out)
        {
            logger?.WriteLine($"converters second parameter isn't an out param");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1016ConverterMissingOutParam, secondParameter.Locations.First()));
            }
        }

        // type of the second parameter must match the type of the attribute
        if (attributedConversionType == null)
        {
            logger?.WriteLine($"converter has no conversion type?!");

            if (!earlyOut)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, conversionMethodSymbol.Locations.First(), $"{methodName} has no matching attributed type."));
            }
            return false;
        }

        if (!SymbolEqualityComparer.Default.Equals(secondParameter.Type, attributedConversionType))
        {
            logger?.WriteLine($"converters conversion type {secondParameter.Type.Name} doesn't match the attribute {attributedConversionType.Name}");

            if (earlyOut)
            {
                return false;
            }
            else
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1017ConverterTypeMismatch, secondParameter.Locations.First(), attributedConversionType.Name, secondParameter.Type.Name));
            }
        }

        if (diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error) == 0)
        {
            logger?.WriteLine($"converter validated");
        }
        else
        {
            logger?.WriteLine($"converter failed validation");
        }
        return diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning || d.Severity == DiagnosticSeverity.Error) == 0;
    }

    // ok so I probably want to beef up this method a bunch
    // I also want to check that the param being created are allowed to be created
    // mostly around their position in the list and any other similar stuff
    // for now though I think just call it and not worry about it?
    // actually does this live in the wrong spot?

    public static ImmutableArray<YarnConverter> ValidateConverters(ImmutableArray<YarnConverter> converters, ILogger? logger = null)
    {
        logger?.WriteLine($"Validating {converters.Length} converters");
        logger?.Inc();

        var validConverter = new List<YarnConverter>();
        HashSet<string> alreadyIdentifiedConverters = [];

        foreach (var converter in converters)
        {
            if (alreadyIdentifiedConverters.Add(converter.FullyQualifiedTypeName))
            {
                validConverter.Add(converter);
            }
            else
            {
                logger?.WriteLine($"Already have a converter for {converter.FullyQualifiedTypeName}, skipping {converter.StaticCallingString}");
            }
        }

        return validConverter.ToImmutableArray();
    }
}