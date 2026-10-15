// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

#nullable enable

namespace Yarn.HostAnalysis;

using Microsoft.CodeAnalysis;

public static class HostAnalysisExtensions
{
    /// <summary>
    /// Given a roslyn method symbol returns a string form that is how you would invoke it in C#
    /// </summary>
    /// <param name="symbol">The method you want the string invocation from</param>
    /// <returns>the C# string form of invoking the method</returns>
    public static string GetStaticCallingString(this IMethodSymbol symbol)
    {
        var format = new SymbolDisplayFormat(
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType,
            parameterOptions: SymbolDisplayParameterOptions.None,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        );
        return symbol.ToDisplayString(format);
    }
}