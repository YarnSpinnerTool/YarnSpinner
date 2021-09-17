using System;
using System.Collections.Generic;
using Antlr4.Runtime;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace YarnLanguageServer
{
    public struct RegisteredFunction
    {
        public string YarnName;
        public Uri DefinitionFile;
        public Range DefinitionRange;
        public string DefinitionName; // Does this need to be qualified? Do we even need this?
        public IEnumerable<ParameterInfo> Parameters;
        public bool IsCommand;
        public bool IsBuiltIn;
        public string Documentation; // Do we care about markup style docstrings?
        public string Language; // = "csharp" or "txt";

        public string Signature;
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

    public struct YarnVariableDeclaration
    {
        public string Name;
        public Uri DefinitionFile; // more like declaration but w/e
        public Range DefinitionRange;
        public string Documentation;
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

    public interface IFunctionDefinitionsProvider
    {
        public Dictionary<string, RegisteredFunction> FunctionDefinitions { get; set; }
    }
}