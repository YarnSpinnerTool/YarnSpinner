// Copyright Yarn Spinner Pty Ltd
// Licensed under the MIT License. See LICENSE.md in project root for license information.

namespace Yarn.Compiler
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Extensions.FileSystemGlobbing;

    /// <summary>
    /// Yarn Projects represent instructions on where to find Yarn scripts and
    /// associated assets, and how they should be compiled.
    /// </summary>
    public class Project
    {
        /// <summary>
        /// The current version of <c>.yarnproject</c> file format.
        /// </summary>
        public const int CurrentProjectFileVersion = 2;

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
        /// Gets or sets the file version of the project.
        /// </summary>
        /// <remarks>
        /// This value is required to be equal to <see
        /// cref="CurrentProjectFileVersion"/>.
        /// </remarks>
        [JsonPropertyName("projectFileVersion")]
        [JsonRequired]
        public int FileVersion { get; set; } = 2;

        /// <summary>
        /// Gets the path that the <see cref="Project"/> was loaded from.
        /// </summary>
        /// <remarks>
        /// This value is not stored when the file is saved, but is instead
        /// determined when the file is loaded by <see
        /// cref="LoadFromFile(string)"/>.
        /// </remarks>
        [JsonIgnore]
        public string Path { get; set; }

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
        public string Definitions { get; set; }

        /// <summary>
        /// Gets or sets a dictionary containing instructions that control how
        /// the Yarn Spinner compiler should compile a project.
        /// </summary>
        public Dictionary<string, object> CompilerOptions { get; set; } = new Dictionary<string, object>();

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
                string searchDirectoryPath = SearchDirectoryPath;

                if (searchDirectoryPath == null)
                {
                    return Array.Empty<string>();
                }

                return matcher.GetResultsInFullPath(searchDirectoryPath);
            }
        }

        /// <summary>
        /// Gets the path to the Definitions file, relative to this project's
        /// location.
        /// </summary>
        [JsonIgnore]
        public string DefinitionsPath
        {
            get
            {
                if (this.Definitions == null || this.SearchDirectoryPath == null)
                {
                    return null;
                }

                return System.IO.Path.Combine(this.SearchDirectoryPath, this.Definitions);
            }
        }

        /// <summary>
        /// Gets the path of the directory from which to start searching for
        /// .yarn files. This value is null if the directory does not exist on
        /// disk.
        /// </summary>
        private string SearchDirectoryPath
        {
            get
            {
                string searchDirectoryPath;

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
                    // This project does not refer to a file on disk or to a directory.
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
        public bool IsMatchingPath(string path) {

            string searchDirectoryPath = this.SearchDirectoryPath;
            if (searchDirectoryPath == null) {
                return false;
            }
            foreach (var sourceFile in this.SourceFiles) {
                if (sourceFile.Equals(path)) {
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
        /// Loads and parses a <see cref="Project"/> from a file on disk.
        /// </summary>
        /// <param name="path">The path to the file to load.</param>
        /// <returns>The loaded <see cref="Project"/>.</returns>
        /// <exception cref="ArgumentException">Thrown when the contents of the
        /// file cannot be loaded.</exception>
        public static Project LoadFromFile(string path)
        {
            try
            {
                var text = System.IO.File.ReadAllText(path);

                var project = JsonSerializer.Deserialize<Project>(text, SerializationOptions);

                if (project.FileVersion != CurrentProjectFileVersion)
                {
                    throw new ArgumentException($"Project file at {path} has incorrect file version (expected {CurrentProjectFileVersion}, got {project.FileVersion})");
                }

                project.Path = path;

                return project;
            }
            catch (JsonException e)
            {
                throw new ArgumentException($"Project file at {path} has invalid JSON", e);
            }
            catch (System.IO.IOException e)
            {
                throw new ArgumentException($"Project file at {path} cannot be opened", e);
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
            public string Assets { get; set; }

            /// <summary>
            /// Gets or sets the location at which the localized string table
            /// may be found.
            /// </summary>
            public string Strings { get; set; }
        }
    }
}
