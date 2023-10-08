// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class FileCompilationResult {
        public List<Node> Nodes { get; internal set; } = new List<Node>();

        public List<NodeDebugInfo> DebugInfos { get; internal set; } = new List<NodeDebugInfo>();

        public List<Diagnostic> Diagnostics { get; internal set; } = new List<Diagnostic>();
    }


    /// <summary>
    /// The result of a compilation.
    /// </summary>
    /// <remarks>
    /// Instances of this class are produced as a result of supplying a <see
    /// cref="CompilationJob"/> to <see
    /// cref="Compiler.Compile(CompilationJob)"/>.
    /// </remarks>
    public class CompilationResult
    {
        /// <summary>
        /// Gets the compiled Yarn program that the <see cref="Compiler"/>
        /// produced.
        /// </summary>
        /// <remarks>
        /// <para>This value will be <see langword="null"/> if there were errors
        /// in the compilation. If this is the case, <see cref="Diagnostics"/>
        /// will contain information describing the errors.</para>
        /// <para>
        /// It will also be <see langword="null"/> if the <see
        /// cref="CompilationJob"/> object's <see
        /// cref="CompilationJob.CompilationType"/> value was not <see
        /// cref="CompilationJob.Type.FullCompilation"/>.
        /// </para>
        /// </remarks>
        public Program? Program { get; internal set; }

        /// <summary>
        /// Gets a dictionary mapping line IDs to StringInfo objects.
        /// </summary>
        /// <remarks>
        /// The string table contains the extracted line text found in the
        /// provided source code. The keys of this dictionary are the line IDs
        /// for each line - either through explicit line tags indicated through
        /// the <c>#line:</c> tag, or implicitly-generated line IDs that the
        /// compiler added during compilation.
        /// </remarks>
        public IDictionary<string, StringInfo>? StringTable { get; internal set; }

        /// <summary>
        /// Gets the collection of variable declarations that were found during
        /// compilation.
        /// </summary>
        /// <remarks>
        /// This value will be <see langword="null"/> if the <see
        /// cref="CompilationJob"/> object's <see
        /// cref="CompilationJob.CompilationType"/> value was not <see
        /// cref="CompilationJob.Type.TypeCheck"/> or <see
        /// cref="CompilationJob.Type.FullCompilation"/>.
        /// </remarks>
        public IEnumerable<Declaration> Declarations { get; internal set; } = Array.Empty<Declaration>();

        /// <summary>
        /// Gets a value indicating whether the compiler had to create line IDs
        /// for lines in the source code that lacked <c>#line:</c> tags.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every line is required to have a line ID. If a line doesn't have a
        /// line ID specified in the source code (via a <c>#line:</c> tag), the
        /// compiler will create one.
        /// </para>
        /// <para>
        /// Implicit line IDs are guaranteed to remain the same between
        /// compilations when the source file does not change. If you want line
        /// IDs to remain the same when the source code may be modified in the
        /// future, add a <c>#line:</c> tag to the line. This may be done by
        /// hand, or added using the <see cref="Utility.AddTagsToLines(string,
        /// ICollection{string})"/> method.
        /// </para>
        /// </remarks>
        public bool ContainsImplicitStringTags { get; internal set; }

        /// <summary>
        /// Gets the collection of file-level tags found in the source code.
        /// </summary>
        /// <remarks>The keys of this dictionary are the file names (as
        /// indicated by the <see cref="CompilationJob.File.FileName"/> property
        /// of the <see cref="CompilationJob"/>'s <see
        /// cref="CompilationJob.Files"/> collection), and the values are the
        /// file tags associated with that file.
        /// </remarks>
        public Dictionary<string, IEnumerable<string>> FileTags { get; internal set; } = new Dictionary<string, IEnumerable<string>>();

        /// <summary>
        /// Gets the collection of <see cref="Diagnostic"/> objects that
        /// describe problems in the source code.
        /// </summary>
        /// <remarks>
        /// If the compiler encounters errors while compiling source code, the
        /// <see cref="CompilationResult"/> it produces will have a <see
        /// cref="Program"/> value of <see langword="null"/>. To help figure out
        /// what the error is, users should consult the contents of this
        /// property.
        /// </remarks>
        public IEnumerable<Diagnostic> Diagnostics { get; internal set; } = Array.Empty<Diagnostic>();

        /// <summary>
        /// Gets the collection of <see cref="DebugInfo"/> objects for each node
        /// in <see cref="Program"/>.
        /// </summary>
        [Obsolete("Use ProjectDebugInfo.Nodes")]
        public IReadOnlyDictionary<string, NodeDebugInfo> DebugInfo => ProjectDebugInfo?.Nodes.ToDictionary(n => n.NodeName, n => n) ?? new Dictionary<string, NodeDebugInfo>();

        /// <summary>
        /// Gets the debugging information for this compiled project.
        /// </summary>
        public ProjectDebugInfo? ProjectDebugInfo { get; internal set; }
        
    }
}
