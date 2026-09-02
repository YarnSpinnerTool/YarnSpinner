#nullable enable

namespace Yarn.Analyser;

using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Yarn.Shared;

public static class GeneratorExtensions
{
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

    public static string ToJSON(this Yarn.Shared.Action action, Location location, string? projectRoot, HashSet<string>? diagnosticCodes)
    {
        var result = new Dictionary<string, object?>();

        var span = location.GetLineSpan();
        
        var relativePath = span.Path;
        if (projectRoot != null)
        {
            relativePath = GeneratorExtensions.GetRelativePath(projectRoot, relativePath);
        }

        result["yarnName"] = action.Name;
        result["definitionName"] = action.MethodName;
        result["fileName"] = relativePath;

        result["language"] = "csharp";
        result["async"] = action.IsAsync;

        result["containsErrors"] = false;
        // result["containsErrors"] = diagnosticCodes?.Count > 0; // quick hack for now until I work out a better way to say if a diagnostic is an error or a warning
        if (diagnosticCodes?.Count > 0)
        {
            result["errorCodes"] = diagnosticCodes;
        }

        var startPosition = new Dictionary<string, int>()
        {
            {"line", span.StartLinePosition.Line},
            {"character", span.StartLinePosition.Character},
        };
        var endPosition = new Dictionary<string, int>()
        {
            {"line", span.EndLinePosition.Line},
            {"character", span.EndLinePosition.Character},
        };
        result["location"] = new Dictionary<string, Dictionary<string, int>>()
        {
            {"start", startPosition},
            {"end", endPosition},
        };

        result["parameters"] = new List<Dictionary<string, object?>>(action.Parameters
        .Where(p => p is not TokenParameter && p is not UnknownParameter)
        .Select(p =>
        {
            var paramObject = new Dictionary<string, object?>();

            paramObject["name"] = p.Name;
            if (p.HasDefaultValue)
            {
                // there is a quirk/oversight here
                // if the default value is null it will be captured as null exactly, which is what we want
                // but the ysls schema needs it to be a string
                // this feels like an oversight we'd need to fix
                paramObject["defaultValue"] = p.DefaultValueDisplay;
            }
            paramObject["isParamsArray"] = p.IsArray;

            switch (p)
            {
                case BasicParameter b:
                    switch (b.SpecialType)
                    {
                        case YarnSpecialType.Boolean: paramObject["type"] = "bool"; break;
                        case YarnSpecialType.SByte: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Byte: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Int16: paramObject["type"] = "number"; break;
                        case YarnSpecialType.UInt16: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Int32: paramObject["type"] = "number"; break;
                        case YarnSpecialType.UInt32: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Int64: paramObject["type"] = "number"; break;
                        case YarnSpecialType.UInt64: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Decimal: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Single: paramObject["type"] = "number"; break;
                        case YarnSpecialType.Double: paramObject["type"] = "number"; break;
                        case YarnSpecialType.String: paramObject["type"] = "string"; break;
                    }
                    break;
                
                case EnumParameter e:
                    paramObject["type"] = "enum";
                    paramObject["subtype"] = e.EnumTypeName;
                    break;
                
                case GameObjectParameter g:
                    paramObject["type"] = "instance";
                    paramObject["subtype"] = g.NamedType.shortType;
                    break;
                case ComponentParameter gp:
                    paramObject["type"] = "instance";
                    paramObject["subtype"] = gp.NamedType.shortType;
                    break;
                
                case ConverterParameter cp:
                    paramObject["type"] = "instance";
                    paramObject["subtype"] = cp.Converter.FullyQualifiedTypeName;
                    break;
            }

            // there are two special cases, if we are node or enum attributed
            // this does mean you can override the type
            // but that is on you, don't do that
            if (p.AttributedEnumSubtype != null)
            {
                paramObject["type"] = "enum";
                paramObject["subtype"] = p.AttributedEnumSubtype;
            }
            if (p.IsNodeAttributed)
            {
                paramObject["type"] = "node";
            }

            return paramObject;
        }).ToArray());

        if (action.Type == ActionType.Function)
        {
            var retvrn = new Dictionary<string, string>();
            switch (action.Return)
            {
                case ReturnType.String:
                case ReturnType.AsyncString:
                    retvrn["type"] = "string";
                    break;
                
                case ReturnType.Number:
                case ReturnType.AsyncNumber:
                    retvrn["type"] = "number";
                    break;
                
                case ReturnType.Boolean:
                case ReturnType.AsyncBoolean:
                    retvrn["type"] = "boolean";
                    break;
            }
            result["return"] = retvrn;
        }
        return System.Text.Json.JsonSerializer.Serialize(result);
    }
}