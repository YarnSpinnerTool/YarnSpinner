using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    public struct RegisteredDefinition
    {
        public string YarnName;
        public Uri DefinitionFile;
        public Range DefinitionRange;
        public string DefinitionName; // Does this need to be qualified? Do we even need this?
        public IEnumerable<ParameterInfo> Parameters;
        public int? MinParameterCount;
        public int? MaxParameterCount;
        public bool IsCommand;
        public bool IsBuiltIn;
        public int Priority; // If multiple defined using the same filetype, lower priority wins.
        public string Documentation; // Do we care about markup style docstrings?
        public string Language; // = "csharp" or "txt";
        public string Signature;
        public string FileName; // an optional field used exlusively to aid searching for fuller info for things defined in json
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
    public struct YarnFunctionCall
    {
        public string Name;
        public IToken NameToken;
        public Range ParametersRange;
        public IEnumerable<Range> ParameterRanges;
        public int ParameterCount; // Count of actually valued parameters (ParameterRanges enumerable includes potentially empty ranges)
        public Range ExpressionRange;
        public bool IsCommand;
    }

    public interface IDefinitionsProvider
    {
        public Dictionary<string, RegisteredDefinition> Definitions { get; set; }
    }
}