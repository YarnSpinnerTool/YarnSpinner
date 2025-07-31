// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

// Uncomment to ensure that all expressions have a known type at compile time
// #define VALIDATE_ALL_EXPRESSIONS

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// An input into a Yarn Spinner compilation.
    /// </summary>
    public interface ISourceInput
    {
        /// <summary>
        /// The name of the input.
        /// </summary>
        public string FileName { get; }
    }

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
        public struct File : ISourceInput
        {
            /// <summary>
            /// The name of the file.
            /// </summary>
            /// <remarks>
            /// This may be a full path, or just the filename or anything in
            /// between. This is useful for diagnostics, and for attributing
            /// <see cref="Line"/> objects to their original source
            /// files.</remarks>
            public string FileName { get; set; }

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
            TypeCheck,

            /// <summary>Generate declarations only. This is equivalent to <see
            /// cref="TypeCheck"/>.</summary>
            [Obsolete("Use TypeCheck instead")]
            DeclarationsOnly = TypeCheck,

            /// <summary>The compiler will generate a string table
            /// only.</summary>
            StringsOnly,
        }

        /// <summary>
        /// The <see cref="File"/> structs that represent the content to
        /// parse..
        /// </summary>
        [Obsolete("Use " + nameof(Inputs))]
        public IEnumerable<File> Files
        {
            get
            {
                List<File> files = new();
                foreach (var input in Inputs)
                {
                    if (input is File file)
                    {
                        files.Add(file);
                    }
                }
                return files;
            }
        }


        public IEnumerable<ISourceInput> Inputs;

        /// <summary>
        /// The <see cref="Library"/> that contains declarations for
        /// functions.
        /// </summary>
        public Library? Library;

        /// <summary>
        /// The type of compilation to perform.
        /// </summary>
        public Type CompilationType;

        /// <summary>
        /// The declarations for variables.
        /// </summary>
        [Obsolete("Use " + nameof(Declarations))]
        public IEnumerable<Declaration> VariableDeclarations
        {
            readonly get => Declarations;
            set => Declarations = value;
        }

        /// <summary>
        /// The declarations for variables and functions.
        /// </summary>
        public IEnumerable<Declaration> Declarations;

        /// <summary>
        /// Gets or sets the version of the Yarn language.
        /// </summary>
        public int LanguageVersion { get; set; }

        /// <summary>
        /// The collection of type declarations that should be imported and made
        /// available to the compiler, prior to compilation.
        /// </summary>
        public IEnumerable<IType>? TypeDeclarations { get; set; }

        /// <summary>
        /// A cancellation token that can be used to signal that the compilation
        /// should be cancelled.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// collection of files.
        /// </summary>
        /// <param name="paths">The paths to the files.</param>
        /// <param name="library">The <see cref="Library"/> containing functions
        /// to use for this compilation.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromFiles(IEnumerable<string> paths, Library? library = null)
        {
            var fileList = new List<ISourceInput>();

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
                Inputs = fileList,
                Library = library,
                Declarations = Array.Empty<Declaration>(),
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
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// collection of source inputs.
        /// </summary>
        /// <param name="inputs">The inputs to the compilation.</param>
        /// <param name="library">The <see cref="Library"/> containing functions
        /// to use for this compilation.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromInputs(IEnumerable<ISourceInput> inputs, Library? library = null)
        {
            return new CompilationJob
            {
                Inputs = inputs,
                Library = library,
                Declarations = Array.Empty<Declaration>(),
            };
        }

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// string.
        /// </summary>
        /// <param name="fileName">The name to assign to the compiled
        /// file.</param>
        /// <param name="source">The text to compile.</param>
        /// <param name="library">Library of function definitions to use during
        /// compilation.</param>
        /// <param name="languageVersion">The version of the Yarn language to
        /// use.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromString(string fileName, string source, Library? library = null, int languageVersion = Project.CurrentProjectFileVersion)
        {
            return new CompilationJob
            {
                Inputs = new List<ISourceInput>
                {
                    new File
                    {
                        Source = source, FileName = fileName,
                    },
                },
                Library = library,
                LanguageVersion = languageVersion,
            };
        }
    }
}
