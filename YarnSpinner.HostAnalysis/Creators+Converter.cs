#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Yarn.Shared;

public static partial class Creators
{
    public static YarnConverter ConverterFromMethodAndType(INamedTypeSymbol type, IMethodSymbol method)
    {
        return new YarnConverter(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), method.GetStaticCallingString());
    }

    public static ImmutableArray<YarnConverter?> CollectAssemblyConverters(Compilation compilation, ILogger? logger = null)
    {
        try
        {
            // ok so get all referenced assemblies
            // remove any that don't reference yarnspinner (because they can't access the attribute)
            // then visit each of those
            
            logger?.WriteLine($"looking through {compilation.AssemblyName} references for the converter attribute");
            logger?.Inc();

            // ok so first level of thinning is does this assembly reference reference Yarn Spinner
            // if so then we can continue
            if (!compilation.ReferencedAssemblyNames.Any(a => a.Name == "YarnSpinner.Unity"))
            {
                logger?.WriteLine("Skipping this assembly due to it not referencing yarnspinner");
                logger?.Dec();
                return ImmutableArray.Create<YarnConverter?>();
            }

            var finder = new ConverterAssemblyFinder();
            List<IMethodSymbol> attributedMethods = new();
            logger?.WriteLine($"checking {compilation.References.Count()} references");
            foreach (MetadataReference metadataRef in compilation.References)
            {
                ISymbol? symbol = compilation.GetAssemblyOrModuleSymbol(metadataRef);
                if (symbol is IAssemblySymbol assemblySymbol)
                {
                    if (assemblySymbol.Modules.Any(m => m.ReferencedAssemblies.Any(a => a.Name == "YarnSpinner.Unity")))
                    {
                        attributedMethods.AddRange(finder.FindMethods(assemblySymbol));
                    }
                }
            }

            logger?.WriteLine($"found {attributedMethods.Count} inside the referenced assemblies of {compilation.Assembly.Name}");
            logger?.Inc();
            foreach (var method in attributedMethods)
            {
                logger?.WriteLine($"found {method.Name}");
                // now I need to convert these into Yarn Converters
            }
            logger?.Dec();
            logger?.Dec();

            var andThen = attributedMethods.Select(m => GetConverterFromMethodSymbol(m, compilation, logger)).ToImmutableArray();
            logger?.WriteLine($"And then converted {andThen.Length} of them");

            return andThen;
        }
        catch (System.Exception ex)
        {
            logger?.WriteLine("oh no!");
            // EmergencyLogger.ExceptionLog(ex, null, true);
            throw;
        }
    }

    private static YarnConverter? GetConverterFromMethodSymbol(IMethodSymbol methodSymbol, Compilation compilation, ILogger? logger)
    {
        logger?.WriteLine($"beginning method converter transformation from {methodSymbol.Name}");

        var attributeConstructor = methodSymbol.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == "Yarn.Unity.YarnConverterAttribute").First(a => a.ConstructorArguments.Length == 1).ConstructorArguments.First();
        var attributeValue = attributeConstructor.Value;
        if (attributeValue == null)
        {
            logger?.WriteLine("attribute constructor null");
            return null;
        }
        var attributeType = attributeConstructor.Type;
        if (attributeType == null)
        {
            logger?.WriteLine("attribute has no type?!");
            return null;
        }

        var resolvedTypeSymbol = compilation.GetTypeByMetadataName(attributeValue.ToString());
        if (resolvedTypeSymbol == null)
        {
            logger?.WriteLine("despite identifying the symbol was unable to get a type from the compilation");
            return null;
        }

        // now make the converter, check if it's fine, return it
        if (Validators.TryValidateConverter(methodSymbol, resolvedTypeSymbol, out _, true, logger))
        {
            var converter = Creators.ConverterFromMethodAndType(resolvedTypeSymbol, methodSymbol);
            return converter;
        }

        return null;
    }
}