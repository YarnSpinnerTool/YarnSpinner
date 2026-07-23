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
        /// Must be unique across the entire list of all inputs.
        /// </summary>
        public string Name { get; }
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
        public class File : ISourceInput
        {
            /// <inheritdoc/>
            public string Name => Path;

            /// <summary>
            /// The path of the file
            /// </summary>
            /// <remarks> 
            /// This can be a relative or absolute path provided other pieces of the pipeline can manage to process it for diagnostic and debug purposes
            /// </remarks>
            public string Path { get; set; }

            /// <summary>
            /// The source code of this file.
            /// </summary>
            public string Source = string.Empty;

            public File(string path)
            {
                Path = path;
                Source = System.IO.File.ReadAllText(path);
            }
        }

        /// <summary>
        /// Represents a <see langword="string"/> being passed in directly for the compiler to use.
        /// This is in constrast to loading it from a file.   
        /// Mostly used internally for testing.
        /// </summary>
        public class Raw :  ISourceInput
        {
            public string Name { get; set; } = "<unknown>";

            public string Source = string.Empty;
        }

        // maek a new ISourceInput that is a stringinput instead of a file input

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
        [Obsolete("Use " + nameof(Inputs), true)]
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
        /// The type of compilation to perform.
        /// </summary>
        public Type CompilationType;

        /// <summary>
        /// A dictionary describing additional internal options for the
        /// compilation job. 
        /// </summary>
        internal Dictionary<string, string>? Options;

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
        /// A dictionary mapping diagnostic codes to overridden diagnostic
        /// severities.
        /// </summary>
        /// <see cref="Project.CompilerOptionsData.DiagnosticsSeverity"/> 
        public IDictionary<string, Diagnostic.DiagnosticSeverity>? DiagnosticSeverities { get; internal set; }

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// collection of files.
        /// </summary>
        /// <param name="paths">The paths to the files.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromFiles(IEnumerable<string> paths)
        {
            var fileList = new List<ISourceInput>();

            // Read every file and add it to the file list
            foreach (var path in paths)
            {
                fileList.Add(new File(path));
            }

            return new CompilationJob
            {
                Inputs = fileList,
            };
        }

        /// <inheritdoc cref="CreateFromFiles(IEnumerable{string}, ILibrary)" path="/summary"/>
        /// <inheritdoc cref="CreateFromFiles(IEnumerable{string}, ILibrary)" path="/param[@name='paths']"/>
        /// <inheritdoc cref="CreateFromFiles(IEnumerable{string}, ILibrary)" path="/returns"/>
        public static CompilationJob CreateFromFiles(params string[] paths)
        {
            return CreateFromFiles((IEnumerable<string>)paths);
        }

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// collection of source inputs.
        /// </summary>
        /// <param name="inputs">The inputs to the compilation.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromInputs(IEnumerable<ISourceInput> inputs, IEnumerable<Declaration>? declarations = null, int languageVersion = Project.CurrentProjectFileVersion)
        {
            return new CompilationJob
            {
                Inputs = inputs,
                Declarations = declarations ?? Array.Empty<Declaration>(),
                LanguageVersion = languageVersion,
            };
        }

        /// <summary>
        /// Creates a new <see cref="CompilationJob"/> using the contents of a
        /// string.
        /// </summary>
        /// <param name="inputName">The name to assign to the compiled
        /// file.</param>
        /// <param name="source">The text to compile.</param>
        /// <param name="languageVersion">The version of the Yarn language to
        /// use.</param>
        /// <returns>A new <see cref="CompilationJob"/>.</returns>
        public static CompilationJob CreateFromString(string inputName, string source, IEnumerable<Declaration>? declarations = null, int languageVersion = Project.CurrentProjectFileVersion)
        {
            return new CompilationJob
            {
                Inputs = new List<ISourceInput>
                {
                    new Raw
                    {
                        Source = source,
                        Name = inputName,
                    },
                },
                LanguageVersion = languageVersion,
                Declarations = declarations ?? Array.Empty<Declaration>(),
            };
        }

        // ok so for now grab the json
        // load it
        // manually find the definitions
        // later make it so this can be actually deserialised magically
        // and stored on the project correctly
        public static CompilationJob CreateFromProject(Project project)
        {
            List<Declaration> definitions = new();
            foreach (var definitionPath in project.DefinitionsFiles)
            {
                var span = System.IO.File.ReadAllBytes(definitionPath);
                var ysls = new Definitions(span);
                definitions.AddRange(ysls.functions);
            }

            // ok now I have all the function definitions from the various ysls files
            var inputs = new List<File>();
            foreach (var path in project.SourceFiles)
            {
                inputs.Add(new File(path));
            }

            return new CompilationJob
            {
                Inputs = inputs,
                Declarations = definitions,
                LanguageVersion = project.FileVersion
            };
        }
        public static CompilationJob CreateFromProject(string projectPath)
        {
            var project = Project.LoadFromFile(projectPath);
            return CreateFromProject(project);
        }
    }
}
