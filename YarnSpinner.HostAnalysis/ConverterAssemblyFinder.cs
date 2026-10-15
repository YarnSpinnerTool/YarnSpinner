// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.HostAnalysis;

#nullable enable

using Microsoft.CodeAnalysis;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Walks the compilation looking for methods that are tagged with the Yarn.Unity.YarnConverterAttribute attribute.
/// This is because a converter may exist in a different assembly than the method that makes use of it.
/// This isn't the best code is is gonna be very slow.
/// </summary>
/// <remarks>
/// In a future update this code may go away in favour of having all non-resolved converted methods be done through the same approach as the runtime linker instead.
/// Will have to do more testing before determining if that is the right course of action.
/// </remarks>
public class ConverterAssemblyFinder : SymbolVisitor<List<IMethodSymbol>>
{
    private const string attribute = "Yarn.Unity.YarnConverterAttribute";
    private readonly List<IMethodSymbol> results = new();

    /// <summary>
    /// Walks an assembly finding the tagged methods
    /// </summary>
    /// <param name="assembly">the assembly to explore</param>
    /// <returns>The list of all yarn converted attributed methods, may be empty</returns>
    public List<IMethodSymbol> FindMethods(IAssemblySymbol assembly)
    {
        Visit(assembly.GlobalNamespace);
        return results;
    }

    /// <inheritdoc />
    public override List<IMethodSymbol> VisitNamespace(INamespaceSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            member.Accept(this);
        }
        return results;
    }

    /// <inheritdoc />
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