// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System.Collections.Generic;
    using Antlr4.Runtime;
    using Antlr4.Runtime.Tree;

    /// <summary>
    /// Contains the result of parsing a single file of source code.
    /// </summary>
    /// <remarks>
    /// This class provides only syntactic information about a parse - that is,
    /// it provides access to the parse tree, and the stream of tokens used to
    /// produce that parse tree.
    /// </remarks>
    public struct FileParseResult
    {
        /// <summary>
        /// <inheritdoc cref="FileParseResult(string, IParseTree, CommonTokenStream)" path="/param[@name='name']"/>
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// <inheritdoc cref="FileParseResult(string, IParseTree, CommonTokenStream)" path="/param[@name='tree']"/>
        /// </summary>
        public IParseTree Tree { get; }

        /// <summary>
        /// <inheritdoc cref="FileParseResult(string, IParseTree, CommonTokenStream)" path="/param[@name='tokens']"/>
        /// </summary>
        public CommonTokenStream Tokens { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FileParseResult"/>
        /// struct.
        /// </summary>
        /// <param name="name">The name of the file.</param>
        /// <param name="tree">The parse tree extracted from the file.</param>
        /// <param name="tokens">The tokens extracted from the file.</param>
        public FileParseResult(string name, IParseTree tree, CommonTokenStream tokens)
        {
            this.Name = name;
            this.Tree = tree;
            this.Tokens = tokens;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is FileParseResult other &&
                   this.Name == other.Name &&
                   EqualityComparer<IParseTree>.Default.Equals(this.Tree, other.Tree) &&
                   EqualityComparer<CommonTokenStream>.Default.Equals(this.Tokens, other.Tokens);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hashCode = -1713343069;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(this.Name);
            hashCode = hashCode * -1521134295 + EqualityComparer<IParseTree>.Default.GetHashCode(this.Tree);
            hashCode = hashCode * -1521134295 + EqualityComparer<CommonTokenStream>.Default.GetHashCode(this.Tokens);
            return hashCode;
        }
    }
}
