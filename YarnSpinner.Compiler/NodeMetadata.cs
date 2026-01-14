// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System.Collections.Generic;

    /// <summary>
    /// contains metadata about a node extracted during compilation for use by language server features
    /// this is different from nodedebuginfo which is for instruction level debugging
    /// </summary>
    public class NodeMetadata
    {
        /// <summary>
        /// the title of the node
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// the file uri where this node is defined
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        /// <summary>
        /// all jump and detour destinations referenced from this node
        /// </summary>
        public List<JumpInfo> Jumps { get; set; } = new List<JumpInfo>();

        /// <summary>
        /// all function names called within this node
        /// </summary>
        public List<string> FunctionCalls { get; set; } = new List<string>();

        /// <summary>
        /// all command names called within this node (excludes flow control like if/else/endif)
        /// </summary>
        public List<string> CommandCalls { get; set; } = new List<string>();

        /// <summary>
        /// all variable names referenced within this node
        /// </summary>
        public List<string> VariableReferences { get; set; } = new List<string>();

        /// <summary>
        /// complexity score for node groups or negative one if not part of a group
        /// calculated by the compiler based on when clauses
        /// </summary>
        public int NodeGroupComplexity { get; set; } = -1;

        /// <summary>
        /// character names found in dialogue lines within this node
        /// extracted from lines matching the pattern charactername: dialogue
        /// </summary>
        public List<string> CharacterNames { get; set; } = new List<string>();

        /// <summary>
        /// tags from the node tags header
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();

        /// <summary>
        /// preview text from the first few lines of dialogue in the node
        /// used for quick previews in ui
        /// </summary>
        public string PreviewText { get; set; } = string.Empty;

        /// <summary>
        /// number of shortcut options in this node
        /// </summary>
        public int OptionCount { get; set; } = 0;

        /// <summary>
        /// zero based line number where the node header starts (first three dashes)
        /// </summary>
        public int HeaderStartLine { get; set; } = -1;

        /// <summary>
        /// zero based line number where the title declaration is (title: nodename)
        /// </summary>
        public int TitleLine { get; set; } = -1;

        /// <summary>
        /// zero based line number where the node body starts (after second three dashes)
        /// </summary>
        public int BodyStartLine { get; set; } = -1;

        /// <summary>
        /// zero based line number where the node body ends (at or before three equals signs)
        /// </summary>
        public int BodyEndLine { get; set; } = -1;
    }

    /// <summary>
    /// information about a jump or detour from one node to another
    /// </summary>
    public class JumpInfo
    {
        /// <summary>
        /// the title of the destination node
        /// </summary>
        public string DestinationTitle { get; set; } = string.Empty;

        /// <summary>
        /// whether this is a jump or a detour
        /// </summary>
        public JumpType Type { get; set; }
    }

    /// <summary>
    /// type of jump between nodes
    /// </summary>
    public enum JumpType
    {
        /// <summary>
        /// a standard jump to another node
        /// </summary>
        Jump,

        /// <summary>
        /// a detour to another node that will return
        /// </summary>
        Detour
    }
}
