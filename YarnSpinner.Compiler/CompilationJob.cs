// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System.Collections.Generic;

    /// <summary>
    /// An object that contains Yarn source code to compile, and instructions on
    /// how to compile it.
    /// </summary>
    /// <remarks>
    /// Instances of this struct are used with <see
    /// cref="Compiler.Compile(CompilationJob)"/> to produce <see
    /// cref="CompilationResult"/> objects.
    /// </remarks>
    public struct CompilationJob
    {
        /// <summary>
        /// Represents the contents of a file to compile.
        /// </summary>
        public struct File
        {
            /// <summary>
            /// The name of the file.
            /// </summary>
            /// <remarks>
            /// This may be a full path, or just the filename or anything in
            /// between. This is useful for diagnostics, and for attributing
            /// <see cref="Line"/> objects to their original source
            /// files.</remarks>
            public string FileName;

            /// <summary>
            /// The source code of this file.
            /// </summary>
            public string Source;
        }

        /// <summary>
        /// The type of compilation that the compiler will do.
        /// </summary>
        public enum Type
        {
            /// <summary>The compiler will do a full compilation, and
            /// generate a <see cref="Program"/>, function declaration set,
            /// and string table.</summary>
            FullCompilation,

            /// <summary>The compiler will derive only the variable and
            /// function declarations, and file tags, found in the
            /// script.</summary>
            DeclarationsOnly,

            /// <summary>The compiler will generate a string table
            /// only.</summary>
            StringsOnly,
        }

        /// <summary>
        /// The <see cref="File"/> structs that represent the content to
        /// parse..
        /// </summary>
        public IEnumerable<File> Files;

        /// <summary>
        /// The <see cref="Library"/> that contains declarations for
        /// functions.
        /// </summary>
        public Library Library;

        /// <summary>
        /// The type of compilation to perform.
        /// </summary>
        public Type CompilationType;

        /// <summary>
        /// The declarations for variables.
        /// </summary>
        public IEnumerable<Declaration> VariableDeclarations;

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// collection of files.
        /// </summary>
        /// <param name="paths">The paths to the files.</param>
        /// <param name="library">The <see cref="Library"/> containing functions
        /// to use for this compilation.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromFiles(IEnumerable<string> paths, Library library = null)
        {
            var fileList = new List<File>();

            // Read every file and add it to the file list
            foreach (var path in paths)
            {
                fileList.Add(new File
                {
                    FileName = path,
                    Source = System.IO.File.ReadAllText(path),
                });
            }

            return new CompilationJob
            {
                Files = fileList.ToArray(),
                Library = library,
            };
        }

        /// <inheritdoc cref="CreateFromFiles(IEnumerable{string}, Library)" path="/summary"/>
        /// <inheritdoc cref="CreateFromFiles(IEnumerable{string}, Library)" path="/param[@name='paths']"/>
        /// <inheritdoc cref="CreateFromFiles(IEnumerable{string}, Library)" path="/returns"/>
        public static CompilationJob CreateFromFiles(params string[] paths)
        {
            return CreateFromFiles((IEnumerable<string>)paths);
        }

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents
        /// of a string.
        /// </summary>
        /// <param name="fileName">The name to assign to the compiled
        /// file.</param>
        /// <param name="source">The text to compile.</param>
        /// <param name="library">Library of function definitions to use
        /// during compilation.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromString(string fileName, string source, Library library = null)
        {
            return new CompilationJob
            {
                Files = new List<File>
                {
                    new File
                    {
                        Source = source, FileName = fileName,
                    },
                },
                Library = library,
            };
        }
    }
}
