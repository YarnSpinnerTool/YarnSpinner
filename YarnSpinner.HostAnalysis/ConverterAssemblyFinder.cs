namespace Yarn.HostAnalysis;

#nullable enable

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;

// this is all terribly documented on the ms site
// I think this is working though
public class ConverterAssemblyFinder : SymbolVisitor<List<IMethodSymbol>>
{
    private const string attribute = "Yarn.Unity.YarnConverterAttribute";
    private readonly List<IMethodSymbol> results = new();

    public List<IMethodSymbol> FindMethods(IAssemblySymbol assembly)
    {
        Visit(assembly.GlobalNamespace);
        return results;
    }

    public override List<IMethodSymbol> VisitNamespace(INamespaceSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            member.Accept(this);
        }
        return results;
    }

    public override List<IMethodSymbol> VisitNamedType(INamedTypeSymbol symbol)
    {
        // checking if the current members symbols are one of our attributed methods
        foreach (var member in symbol.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol)
            {
                continue;
            }
            if (methodSymbol.GetAttributes().Any(a =>  a.AttributeClass?.ToDisplayString() == attribute || a.AttributeClass?.MetadataName == attribute))
            {
                results.Add(methodSymbol);
            }
        }

        // now recursivelt checking them just in case
        foreach (var nestedType in symbol.GetTypeMembers())
        {
            nestedType.Accept(this);
        }

        return results;
    }
}