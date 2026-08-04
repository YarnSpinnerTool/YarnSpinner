#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

public static partial class Creators
{
    public static NamedType YarnNamedType(this INamedTypeSymbol namedTypeSymbol)
    {
        var fullyQualifiedType = namedTypeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var shortType = namedTypeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var longType = namedTypeSymbol.ToDisplayString();
        var specialType = namedTypeSymbol.SpecialType.ToSpecialType();
        var isUnityComponentType = false;

        var baseSymbol = namedTypeSymbol.BaseType;
        while (baseSymbol != null)
        {
            if (baseSymbol.Name == "MonoBehaviour" || baseSymbol.Name == "Component")
            {
                if (baseSymbol.ContainingNamespace.Name == "UnityEngine")
                {
                    isUnityComponentType = true;
                }
            }
            baseSymbol = baseSymbol.BaseType;
        }

        return new NamedType(fullyQualifiedType, shortType, longType, specialType, isUnityComponentType);
    }

    public static ReturnType UnityReturnType(this ITypeSymbol returnType, ILogger? logger = null)
    {
        if (returnType.SpecialType == SpecialType.System_Void)
        {
            return ReturnType.Void;
        }

        // any of the common types
        switch (returnType.SpecialType)
        {
            case SpecialType.System_Boolean: return ReturnType.Boolean;
            case SpecialType.System_SByte: return ReturnType.Number;
            case SpecialType.System_Byte: return ReturnType.Number;
            case SpecialType.System_Int16: return ReturnType.Number;
            case SpecialType.System_UInt16: return ReturnType.Number;
            case SpecialType.System_Int32: return ReturnType.Number;
            case SpecialType.System_UInt32: return ReturnType.Number;
            case SpecialType.System_Int64: return ReturnType.Number;
            case SpecialType.System_UInt64: return ReturnType.Number;
            case SpecialType.System_Decimal: return ReturnType.Number;
            case SpecialType.System_Single: return ReturnType.Number;
            case SpecialType.System_Double: return ReturnType.Number;
            case SpecialType.System_String: return ReturnType.String;
        }

        // the two unity specfic types
        if (returnType.SpecialType == SpecialType.System_Collections_IEnumerator)
        {
            return ReturnType.IEnumeratorVoid;
        }
        if (returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::UnityEngine.Coroutine")
        {
            return ReturnType.CoroutineVoid;
        }

        // the async types
        switch (returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
        {
            case "global::Yarn.Unity.YarnTask":
            case "global::System.Threading.Tasks.Task":
            case "global::Cysharp.Threading.Tasks.UniTask":
            case "global::UnityEngine.Awaitable":
                return ReturnType.AsyncVoid;
        };

        // the generic async types
        logger?.Inc();
        logger?.WriteLine($"checking deeper against: {returnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
        logger?.Dec();
        if (returnType is INamedTypeSymbol namedType)
        {
            logger?.WriteLine("it is a named symbol, phew");
            if (namedType.IsGenericType)
            {
                string[] allowedBases = [ "global::Yarn.Unity.YarnTask<>", "global::System.Threading.Tasks.Task<>", "global::Cysharp.Threading.Tasks.UniTask<>", "global::UnityEngine.Awaitable<>"];
                var baseType = namedType.ConstructUnboundGenericType();
                logger?.WriteLine($"checking the base of the generic: {baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");

                if (allowedBases.Contains(baseType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))
                {
                    if (namedType.TypeArguments.Length == 1)
                    {
                        switch (namedType.TypeArguments.First().SpecialType)
                        {
                            case SpecialType.System_Boolean: return ReturnType.AsyncBoolean;
                            case SpecialType.System_SByte: return ReturnType.AsyncNumber;
                            case SpecialType.System_Byte: return ReturnType.AsyncNumber;
                            case SpecialType.System_Int16: return ReturnType.AsyncNumber;
                            case SpecialType.System_UInt16: return ReturnType.AsyncNumber;
                            case SpecialType.System_Int32: return ReturnType.AsyncNumber;
                            case SpecialType.System_UInt32: return ReturnType.AsyncNumber;
                            case SpecialType.System_Int64: return ReturnType.AsyncNumber;
                            case SpecialType.System_UInt64: return ReturnType.AsyncNumber;
                            case SpecialType.System_Decimal: return ReturnType.AsyncNumber;
                            case SpecialType.System_Single: return ReturnType.AsyncNumber;
                            case SpecialType.System_Double: return ReturnType.AsyncNumber;
                            case SpecialType.System_String: return ReturnType.AsyncString;
                        }
                    }
                    else
                    {
                        logger?.Inc();
                        logger?.WriteLine($"return type doesnt have a single type: {namedType.TypeArguments.Length}");
                        logger?.Dec();
                    }
                }
                else
                {
                    logger?.Inc();
                    logger?.WriteLine("return type isn't a special type");
                    logger?.Dec();
                }
            }
            else
            {
                logger?.Inc();
                logger?.WriteLine("return type isn't generic");
                logger?.Dec();
            }
        }
        else
        {
            logger?.Inc();
            logger?.WriteLine("return type isn't a named symbol?!");
            logger?.Dec();
        }

        logger?.Inc();
        logger?.WriteLine("unable to determine the return type");
        logger?.Dec();
        return ReturnType.Unknown;
    }

    public static YarnSpecialType ToSpecialType(this SpecialType specialType)
    {
        return specialType switch
        {
            SpecialType.System_Boolean => YarnSpecialType.Boolean,
            SpecialType.System_SByte => YarnSpecialType.SByte,
            SpecialType.System_Byte => YarnSpecialType.Byte,
            SpecialType.System_Int16 => YarnSpecialType.Int16,
            SpecialType.System_UInt16 => YarnSpecialType.UInt16,
            SpecialType.System_Int32 => YarnSpecialType.Int32,
            SpecialType.System_UInt32 => YarnSpecialType.UInt32,
            SpecialType.System_Int64 => YarnSpecialType.Int64,
            SpecialType.System_UInt64 => YarnSpecialType.UInt64,
            SpecialType.System_Decimal => YarnSpecialType.Decimal,
            SpecialType.System_Single => YarnSpecialType.Single,
            SpecialType.System_Double => YarnSpecialType.Double,
            SpecialType.System_String => YarnSpecialType.String,
            _ => YarnSpecialType.Other,
        };
    }
}