// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using Microsoft.Extensions.FileSystemGlobbing;
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Yarn Projects represent instructions on where to find Yarn scripts and
    /// associated assets, and how they should be compiled.
    /// </summary>
    public class Project
    {
        /// <summary>
        /// A placeholder string that represents the location of the workspace
        /// root in paths.
        /// </summary>
        public const string WorkspaceRootPlaceholder = "${workspaceRoot}";

        internal const string AllowPreviewFeaturesKey = "allowPreviewFeatures";

        /// <summary>
        /// The current version of <c>.yarnproject</c> file format.
        /// </summary>
        public const int CurrentProjectFileVersion = YarnSpinnerProjectVersion3;

        /// <summary>
        ///  A version number representing Yarn Spinner 2.
        /// </summary>
        public const int YarnSpinnerProjectVersion2 = 2;

        /// <summary>
        ///  A version number representing Yarn Spinner 3.
        /// </summary>
        public const int YarnSpinnerProjectVersion3 = 3;

        private static readonly JsonSerializerOptions SerializationOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class.
        /// </summary>
        public Project()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Project"/> class.
        /// </summary>
        /// <param name="path">The value to use for the new instance's <see
        /// cref="Path"/> property.</param>
        /// <param name="workspaceRootPath">The path to the root of the current
        /// workspace, or <see langword="null"/>.</param>
        public Project(string path, string? workspaceRootPath = null)
        {
            this.Path = path;
            this.WorkspaceRootPath = workspaceRootPath;
        }

        /// <summary>
        /// Gets or sets the file version of the project.
        /// </summary>
        /// <remarks>
        /// This value is required to be equal to <see
        /// cref="CurrentProjectFileVersion"/>.
        /// </remarks>
        [JsonPropertyName("projectFileVersion")]
        [JsonRequired]
        public int FileVersion { get; set; } = CurrentProjectFileVersion;

        /// <summary>
        /// Gets the path that the <see cref="Project"/> was loaded from.
        /// </summary>
        /// <remarks>
        /// This value is not stored when the file is saved, but is instead
        /// determined when the file is loaded by <see
        /// cref="LoadFromFile(string)"/>, or provided when the <see
        /// cref="Project"/> is constructed.
        /// </remarks>
        [JsonIgnore]
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the collection of file search patterns used to locate
        /// Yarn files that form this project.
        /// </summary>
        [JsonPropertyName("sourceFiles")]
        public IEnumerable<string> SourceFilePatterns { get; set; } = new[] { "**/*.yarn" };

        /// <summary>
        /// Gets or sets the collection of file search patterns that should be
        /// excluded from this project.
        /// </summary>
        /// <remarks>
        /// If a file is matched by a pattern in <see
        /// cref="SourceFilePatterns"/>, and is also matched by a pattern in
        /// <see cref="ExcludeFilePatterns"/>, then it is not included in the
        /// value returned by <see cref="SourceFiles"/>.
        /// </remarks>
        [JsonPropertyName("excludeFiles")]
        public IEnumerable<string> ExcludeFilePatterns { get; set; } = new string[] { };

        /// <summary>
        /// Gets or sets the collection of <see cref="LocalizationInfo"/>
        /// objects that store information about where localized data for this
        /// project is found.
        /// </summary>
        public Dictionary<string, LocalizationInfo> Localisation { get; set; } = new Dictionary<string, LocalizationInfo>();

        /// <summary>
        /// Gets or sets the base language of the project, as an IETF BCP-47
        /// language tag.
        /// </summary>
        /// <remarks>
        /// The base language is the language that the Yarn scripts is written
        /// in.
        /// </remarks>
        [JsonRequired]
        public string BaseLanguage { get; set; } = CurrentNeutralCulture.Name;

        /// <summary>
        /// Gets or sets the path to a JSON file containing command and function
        /// definitions that this project references.
        /// </summary>
        /// <remarks>
        /// Definitions files are used by editing tools to provide type
        /// information and other externally-defined data used by the Yarn
        /// scripts.
        /// </remarks>
        public string? Definitions { get; set; }

        /// <summary>
        /// Gets a value indicating whether this Project is an 'implicit'
        /// project (that is, it does not currently represent a file that exists
        /// on disk.)
        /// </summary>
        /// <remarks>
        /// An implicit project is created by tools like the Yarn Spinner
        /// Language Server when opening a folder that contains Yarn files but
        /// no Yarn Project file.
        /// </remarks>
        internal bool IsImplicit => Path != null && !System.IO.File.Exists(this.Path);

        /// <summary>
        /// Gets or sets a dictionary containing instructions that control how
        /// the Yarn Spinner compiler should compile a project.
        /// </summary>
        public Dictionary<string, JsonValue> CompilerOptions { get; set; } = new Dictionary<string, JsonValue>();

        private bool GetCompilerOptionsFlag(string key)
        {
            return CompilerOptions.TryGetValue(key, out var value) && value.GetValue<bool>();
        }
        private void SetCompilerOptionsFlag(string key, bool value)
        {
            CompilerOptions[key] = JsonValue.Create(value);
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="number"/> is a valid
        /// Yarn Spinner version number.
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        public static bool IsValidVersionNumber(int number)
        {
            return number == YarnSpinnerProjectVersion2 || number == YarnSpinnerProjectVersion3;
        }

        /// <summary>
        /// Gets or sets a value indicating whether compiler features that are
        /// not intended for production use are allowed.
        /// </summary>
        [JsonIgnore]
        public bool AllowLanguagePreviewFeatures
        {
            get => GetCompilerOptionsFlag(AllowPreviewFeaturesKey);
            set => SetCompilerOptionsFlag(AllowPreviewFeaturesKey, value);
        }

        /// <summary>
        /// Gets the collection of Yarn files that should be used to compile the
        /// project.
        /// </summary>
        /// <remarks>
        /// This collection uses a <see cref="Matcher"/> to find all files
        /// specified by <see cref="SourceFilePatterns"/>, excluding those that
        /// are specified by <see cref="ExcludeFilePatterns"/>.
        /// </remarks>
        [JsonIgnore]
        public IEnumerable<string> SourceFiles
        {
            get
            {
                Matcher matcher = Matcher;
                string? searchDirectoryPath = SearchDirectoryPath;

                if (searchDirectoryPath == null)
                {
                    return Array.Empty<string>();
                }

                var results = new List<string>(matcher.GetResultsInFullPath(searchDirectoryPath));

                foreach (var path in this.SourceFilePatterns)
                {
                    if (System.IO.Path.IsPathRooted(path) && System.IO.Path.GetExtension(path) == ".yarn")
                    {
                        // This is an explicit, absolute path to a Yarn file
                        // (which the globbing matcher won't pick up) - manually
                        // add it to the list of paths that this project
                        // references
                        results.Add(path);
                    }
                }
                return results;
            }
        }

        /// <summary>
        /// Gets the path to the Definitions file, relative to this project's
        /// location.
        /// </summary>
        [JsonIgnore]
        public string? DefinitionsPath
        {
            get
            {
                if (this.Definitions == null)
                {
                    return null;
                }
                else if (this.Definitions.IndexOf(WorkspaceRootPlaceholder) != -1)
                {
                    if (this.WorkspaceRootPath != null
                        && System.IO.Directory.Exists(WorkspaceRootPath))
                    {
                        return this.Definitions.Replace(WorkspaceRootPlaceholder, WorkspaceRootPath);
                    }
                    else
                    {
                        // The path contains the placeholder, but we have no
                        // value to insert it with. Early out here.
                        return null;
                    }
                }
                else if (this.SearchDirectoryPath != null)
                {
                    return System.IO.Path.Combine(this.SearchDirectoryPath, this.Definitions);
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Contains any data parsed from the source file that was not matched
        /// to a property on this type.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        /// <summary>
        /// Gets the path of the directory from which to start searching for
        /// .yarn files. This value is <see langword="null"/> if the directory
        /// does not exist on disk.
        /// </summary>
        private string? SearchDirectoryPath
        {
            get
            {
                if (this.Path == null)
                {
                    return null;
                }

                string? searchDirectoryPath;

                if (System.IO.Directory.Exists(this.Path))
                {
                    // This project refers to a directory on disk.
                    searchDirectoryPath = this.Path;
                }
                else if (System.IO.File.Exists(this.Path))
                {
                    // This project refers to a .yarnproject on disk.
                    searchDirectoryPath = System.IO.Path.GetDirectoryName(this.Path);
                }
                else
                {
                    // This project does not refer to a file on disk or to a
                    // directory.
                    searchDirectoryPath = null;
                }

                return searchDirectoryPath;
            }
        }

        private Matcher Matcher
        {
            get
            {
                Matcher matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

                matcher.AddIncludePatterns(this.SourceFilePatterns);
                matcher.AddExcludePatterns(this.ExcludeFilePatterns);
                return matcher;
            }
        }

        /// <summary>
        /// Gets a value indicating whether <paramref name="path"/> is a path
        /// that is included in this project.
        /// </summary>
        /// <param name="path">The path to check.</param>
        /// <returns><see langword="true"/> if <paramref name="path"/> is a path
        /// that is included in this project; <see langword="false"/>
        /// otherwise.</returns>
        public bool IsMatchingPath(string path)
        {

            string? searchDirectoryPath = this.SearchDirectoryPath;
            if (searchDirectoryPath == null)
            {
                return false;
            }
            foreach (var sourceFile in this.SourceFiles)
            {
                if (sourceFile.Equals(path))
                {
                    return true;
                }
            }

            var result = this.Matcher.Match(searchDirectoryPath, path);
            return result.HasMatches;
        }

        /// <summary>
        /// Gets a neutral <see cref="System.Globalization.CultureInfo"/> that
        /// represents the current culture.
        /// </summary>
        private static System.Globalization.CultureInfo CurrentNeutralCulture
        {
            get
            {
                var current = System.Globalization.CultureInfo.CurrentCulture;
                if (current.IsNeutralCulture == false)
                {
                    current = current.Parent;
                }

                return current;
            }
        }

        /// <summary>
        /// The location of the root of the workspace in which this project is
        /// located.
        /// </summary>
        public string? WorkspaceRootPath { get; set; }

        /// <summary>
        /// Loads and parses a <see cref="Project"/> from a file on disk.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <param name="workspaceRoot">The path of the root of the workspace in
        /// which <see langword="file"/> is located.</param>
        /// <returns>The loaded <see cref="Project"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the contents of the
        /// file cannot be loaded.</exception>
        public static Project LoadFromFile(string path, string? workspaceRoot = null)
        {
            try
            {
                var text = System.IO.File.ReadAllText(path);

                return LoadFromString(text, path, workspaceRoot);
            }
            catch (System.IO.IOException e)
            {
                throw new ArgumentException($"Project file at {path} cannot be opened", e);
            }
        }

        internal static Project LoadFromString(string text, string path, string? workspaceRoot = null)
        {
            try
            {
                var project = JsonSerializer.Deserialize<Project>(text, SerializationOptions);

                if (project == null)
                {
                    throw new ArgumentException("Failed to load Project");
                }

                if (project.FileVersion > CurrentProjectFileVersion)
                {
                    throw new ArgumentException($"Project file at {path} has incorrect file version (expected {CurrentProjectFileVersion}, got {project.FileVersion})");
                }

                project.Path = path;
                project.WorkspaceRootPath = workspaceRoot;

                return project;
            }
            catch (JsonException e)
            {
                throw new ArgumentException($"Project file at {path} has invalid JSON", e);
            }
        }

        /// <summary>
        /// Saves a <see cref="Project"/> as JSON-formatted text to a file on
        /// disk.
        /// </summary>
        /// <param name="path">The path of the file to write to.</param>
        public void SaveToFile(string path) => System.IO.File.WriteAllText(path, this.GetJson());

        /// <summary>
        /// Gets a string containing JSON-formatted text that represents this
        /// <see cref="Project"/>.
        /// </summary>
        /// <returns>The <see cref="Project"/>, serialized to JSON.</returns>
        public string GetJson() => JsonSerializer.Serialize(this, SerializationOptions);

        /// <summary>
        /// Stores the locations of where localized assets and a localized
        /// string table for a Yarn Project may be found.
        /// </summary>
        public class LocalizationInfo
        {
            /// <summary>
            /// Gets or sets the location at which localized assets may be
            /// found.
            /// </summary>
            public string? Assets { get; set; }

            /// <summary>
            /// Gets or sets the location at which the localized string table
            /// may be found.
            /// </summary>
            public string? Strings { get; set; }
        }
    }
}
