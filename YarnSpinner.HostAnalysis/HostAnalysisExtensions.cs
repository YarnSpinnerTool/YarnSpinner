#nullable enable

namespace Yarn.HostAnalysis;

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Yarn.Shared;

public static class HostAnalysisExtensions
{
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

    // System.IO.Path.GetRelativePath(projectRoot, SourceFileName); siiiiiiigh
    // this is based on: https://stackoverflow.com/questions/275689/how-to-get-relative-path-from-absolute-path
    public static string GetRelativePath(string projectRoot, string SourceFileName)
    {
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new ArgumentNullException("root is null");
        }
        if (string.IsNullOrEmpty(SourceFileName))
        {
            throw new ArgumentNullException("absolute is null");
        }

        Uri from = new Uri(projectRoot);
        Uri to = new Uri(SourceFileName);

        if (from.Scheme != to.Scheme)
        {
            return SourceFileName;
        }

        Uri relativeUri = from.MakeRelativeUri(to);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        if (to.Scheme.Equals("file", StringComparison.InvariantCultureIgnoreCase))
        {
            relativePath = relativePath.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
        }

        return relativePath;
    }
}