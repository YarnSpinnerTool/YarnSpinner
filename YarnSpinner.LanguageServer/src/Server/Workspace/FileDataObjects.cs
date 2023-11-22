using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Yarn;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    public struct RegisteredDefinition
    {
        public string YarnName;
        public Uri? DefinitionFile;
        public Range? DefinitionRange;
        public string? DefinitionName; // Does this need to be qualified? Do we even need this?
        public IEnumerable<ParameterInfo>? Parameters;
        public int? MinParameterCount;
        public int? MaxParameterCount;
        public bool IsCommand;
        public bool IsBuiltIn;

        /// <summary>
        /// Converts this <see cref="RegisteredDefinition"/> from 
        /// </summary>
        /// <returns></returns>
        internal Action ToAction()
        {
            var action = new Action
            {
                YarnName = this.YarnName,
                Documentation = this.Documentation,
                Parameters = (this.Parameters ?? Array.Empty<ParameterInfo>()).Select(p =>
                {
                    return new Action.ParameterInfo
                    {
                        Name = p.Name,
                        DisplayDefaultValue = p.DefaultValue,
                        Description = p.Documentation,
                        DisplayTypeName = p.Type,
                        IsParamsArray = p.IsParamsArray,
                        Type = GetYarnType(p.Type),
                    };
                }).ToList(),
                ReturnType = GetYarnType(this.ReturnType),
                SourceFileUri = this.DefinitionFile,
                Language = this.Language ?? "csharp",
                SourceRange = this.DefinitionRange,
            };
            return action;
        }

        private static IType GetYarnType(string? type)
        {
            return type?.ToLowerInvariant() switch
            {
                "string" => Yarn.BuiltinTypes.String,
                "bool" => Yarn.BuiltinTypes.Boolean,
                "number" or "float" or "int" => Yarn.BuiltinTypes.Number,
                _ => Yarn.BuiltinTypes.Any,
            } ?? Yarn.BuiltinTypes.Any;
        }

        public int Priority; // If multiple defined using the same filetype, lower priority wins.
        public string Documentation; // Do we care about markup style docstrings?
        public string Language; // = "csharp" or "txt";
        public string Signature;
        public string FileName; // an optional field used exlusively to aid searching for fuller info for things defined in json

        // For functions, the name of the return type of this action
        public string ReturnType;
    }

    public struct ParameterInfo
    {
        public string Name;
        public string Type;
        public string Documentation;
        public string DefaultValue; // null if not optional
        public bool IsParamsArray;
    }

    public struct YarnNode
    {
        public string Name;
        public Uri DefinitionFile;
        public Range DefinitionRange;
        public string Documentation; // Is there a good place to get this or should we just look at the header lines?
    }

    /// <summary>
    /// Info about a single function or command call expression including whitespace and parenthesis.
    /// </summary>
    public struct YarnActionReference
    {
        public string Name;
        public IToken NameToken;
        public Range ParametersRange;
        public IEnumerable<Range> ParameterRanges;
        public int ParameterCount; // Count of actually valued parameters (ParameterRanges enumerable includes potentially empty ranges)
        public Range ExpressionRange;
        public bool IsCommand;
    }
}
