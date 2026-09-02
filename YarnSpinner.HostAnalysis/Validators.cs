#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

public static class Validators
{
    private enum MethodType
    {
        Method, LocalMethod, Lambda,
    }
    public enum ActionValidation
    {
        /// <summary>
        /// Represent an action which based on it's method signature and how it was registered is able to be run through the compile time pipeline
        /// </summary>
        CompileTimeValid,
        /// <summary>
        /// Represent an action which based on it's method signature and how it was registered is able to be run through the runtime pipleine
        /// </summary>
        RunTimeValid,
        /// <summary>
        /// Represent an action which based on it's method signature and how it was registered is unable to be invoked.
        /// </summary>
        FailedValidation,
    }
    public static ActionValidation TryValidateMethodAsAction(IMethodSymbol methodSymbol, string yarnName, ActionType type, DeclarationType declarationType, out List<Diagnostic> diagnostics, Location? nameLocation = null, Location? invocationLocation = null, ILogger? logger = null)
    {
        diagnostics = new List<Diagnostic>();
        var location = methodSymbol.Locations.First();

        // these two are universal, all actions need these
        // so we can check them now ahead of time
        // we need a valid name
        var isValidName = !yarnName.Any(x => char.IsWhiteSpace(x));
        if (!isValidName)
        {
            logger?.WriteLine("Method name is invalid");
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1002ActionMethodsMustHaveAValidName, nameLocation ?? location, yarnName));
            return ActionValidation.FailedValidation;
        }
        // we need to be contained within a type
        var hasContainingType = methodSymbol.ContainingType != null;
        if (!hasContainingType)
        {
            logger?.WriteLine("Method has no containing type");
            diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, location, $"Was unable to resolve the containing type of the method {methodSymbol.Name}"));
            return ActionValidation.FailedValidation;
        }

        // determing what type of method symbol we are
        // this is necessary as different action types and different registrations handle this differently
        MethodType methodType;
        switch (methodSymbol.MethodKind)
        {
            case MethodKind.Ordinary:
                methodType = MethodType.Method;
                break;
            case MethodKind.AnonymousFunction:
                methodType = MethodType.Lambda;
                break;
            case MethodKind.LocalFunction:
                methodType = MethodType.LocalMethod;
                break;
            default:
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, location, $"Attempted to register an action that is not a method, lambda, or local function, it's a {methodSymbol.MethodKind}"));
                return ActionValidation.FailedValidation;
        }

        // what type the action returns
        var returnType = methodSymbol.ReturnType.UnityReturnType();

        // is the action publicly accessible?
        var methodIsPublic = methodSymbol.DeclaredAccessibility == Accessibility.Public;
        var classIsPublic = methodSymbol.ContainingType?.DeclaredAccessibility == Accessibility.Public;
        var actionIsPublic = methodIsPublic && classIsPublic;

        if (type == ActionType.Command && declarationType == DeclarationType.Attribute)
        {
            // we are an attributed command
            
            // we must be a method
            switch (methodType)
            {
                case MethodType.Method:
                    break;
                case MethodType.LocalMethod:
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1024ActionIsALocalFunction, invocationLocation ?? location)); // this needs to be upgraded to a warning here?
                    return ActionValidation.FailedValidation;
                case MethodType.Lambda:
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1021ActionIsALambda, invocationLocation ?? location)); // this needs to be upgraded to a warning here?
                    return ActionValidation.FailedValidation;
            }

            // we should be publicly accessible
            if (!actionIsPublic)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1001ActionMethodsMustBePublic, location, DiagnosticSeverity.Info, null, null, yarnName, methodSymbol.DeclaredAccessibility));
            }

            // we must return a void-alike
            switch (returnType)
            {
                case ReturnType.Void:
                case ReturnType.AsyncVoid:
                    return actionIsPublic ? ActionValidation.CompileTimeValid : ActionValidation.RunTimeValid;
                
                case ReturnType.CoroutineVoid:
                case ReturnType.IEnumeratorVoid:
                    logger?.WriteLine("Method is a coroutine, these can't be compile time invoked");
                    return ActionValidation.RunTimeValid;
                
                default:
                {
                    logger?.WriteLine("Method has an invalid return");
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1003CommandMethodsMustHaveAValidReturnType, location, yarnName, returnType));
                    return ActionValidation.FailedValidation;
                }
            }
        }
        else if (type == ActionType.Command && declarationType == DeclarationType.DirectRegistration)
        {
            // we are a direct registered command
            
            // we can be any type
            // but we do grumble about being a lambda or local function
            switch (methodType)
            {
                case MethodType.Method:
                    break;
                case MethodType.LocalMethod:
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1024ActionIsALocalFunction, invocationLocation ?? location));
                    break;
                case MethodType.Lambda:
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1021ActionIsALambda, invocationLocation ?? location));
                    break;
            }

            // we can be private
            // but do grumble about this
            if (!actionIsPublic)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1025DirectActionIsPrivate, invocationLocation));
            }

            // we must still return a valid void-alike
            switch (returnType)
            {
                case ReturnType.Void:
                case ReturnType.AsyncVoid:
                    if (actionIsPublic)
                    {
                        return ActionValidation.CompileTimeValid;
                    }
                    return ActionValidation.RunTimeValid;
                
                case ReturnType.CoroutineVoid:
                case ReturnType.IEnumeratorVoid:
                    logger?.WriteLine("Method is a coroutine, these can't be compile time invoked");
                    return ActionValidation.RunTimeValid;
                
                default:
                {
                    logger?.WriteLine("Method has an invalid return");
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1003CommandMethodsMustHaveAValidReturnType, location, yarnName, returnType));
                    return ActionValidation.FailedValidation;
                }
            }
        }
        else if (type == ActionType.Function && declarationType == DeclarationType.Attribute)
        {
            // we are an attributed function

            // we need a non-void return type
            // we need to be on a method
            // we need to be a public method

            // we must be a method
            switch (methodType)
            {
                case MethodType.Method:
                    break;
                case MethodType.LocalMethod:
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1024ActionIsALocalFunction, invocationLocation ?? location)); // this needs to be upgraded to a warning here?
                    return ActionValidation.FailedValidation;
                case MethodType.Lambda:
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1021ActionIsALambda, invocationLocation ?? location)); // this needs to be upgraded to a warning here?
                    return ActionValidation.FailedValidation;
            }

            // we can be private but we will grumble about it
            if (!actionIsPublic)
            {
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1001ActionMethodsMustBePublic, location, DiagnosticSeverity.Info, null, null, yarnName, methodSymbol.DeclaredAccessibility));
            }

            // we must return a value
            switch (returnType)
            {
                case ReturnType.String:
                case ReturnType.Number:
                case ReturnType.Boolean:
                case ReturnType.AsyncString:
                case ReturnType.AsyncNumber:
                case ReturnType.AsyncBoolean:
                    return actionIsPublic ? ActionValidation.CompileTimeValid : ActionValidation.RunTimeValid;

                default:
                {
                    logger?.WriteLine("Method has an invalid return");
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1004FunctionMethodsMustHaveAValidReturnType, location, yarnName, returnType));
                    return ActionValidation.FailedValidation;
                }
            }
        }
        else if (type == ActionType.Function && declarationType == DeclarationType.DirectRegistration)
        {
            // we are a direct registered function

            // we must return a value
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
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1004FunctionMethodsMustHaveAValidReturnType, location, yarnName, returnType));
                    return ActionValidation.FailedValidation;
                }
            }

            bool isRunTimeFunction = false;

            // we can be any type
            // but we do grumble about being a lambda or local function
            switch (methodType)
            {
                case MethodType.Method:
                    break;
                case MethodType.LocalMethod:
                    isRunTimeFunction = true;
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1024ActionIsALocalFunction, invocationLocation ?? location));
                    break;
                case MethodType.Lambda:
                    isRunTimeFunction = true;
                    diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1021ActionIsALambda, invocationLocation ?? location));
                    break;
            }

            // we can be private
            // but do grumble about this
            if (!actionIsPublic)
            {
                isRunTimeFunction = true;
                diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1025DirectActionIsPrivate, invocationLocation));
            }

            return isRunTimeFunction ? ActionValidation.RunTimeValid : ActionValidation.CompileTimeValid;
        }

        diagnostics.Add(Diagnostic.Create(ActionDiagnostics.YS1000InternalErrorProcessingAction, location, $"Attempted to validate an action that we couldn't determine enough information about. Please file a bug."));
        return ActionValidation.FailedValidation;
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