using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Yarn.Compiler
{
    public class Project
    {
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

        public const int CurrentProjectFileVersion = 2;

        public static Project LoadFromFile(string path) {
            var text = System.IO.File.ReadAllText(path);
            var config = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
            };

            var project = JsonSerializer.Deserialize<Project>(text, config);

            if (project.FileVersion != CurrentProjectFileVersion) {
                throw new ArgumentException($"Project file at {path} has incorrect file version (expected {CurrentProjectFileVersion}, got {project.FileVersion})");
            }

            project.Path = path;

            return project;
        }

        [JsonPropertyName("projectFileVersion")]
        [JsonRequired]
        public int FileVersion { get; set; }

        public string Path { get; private set; }

        [JsonPropertyName("sourceFiles")]
        public IEnumerable<string> SourceFilePatterns { get; set; } = new[] { "**/*.yarn" };

        public Dictionary<string, LocalizationInfo> Localisation { get; set; } = new Dictionary<string,LocalizationInfo>();

        [JsonRequired]
        public string BaseLanguage { get; set; } = CurrentNeutralCulture.Name;

        public string Definitions { get; set; }

        public class LocalizationInfo {
            public string Assets { get; set; }
            public string Strings { get; set; }
        }

        public Dictionary<string, object> CompilerOptions { get; set; } = new Dictionary<string, object>();

        [JsonIgnore]
        public IEnumerable<string> SourceFiles
        {
            get
            {
                Matcher matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

                matcher.AddIncludePatterns(this.SourceFilePatterns);

                var searchDirectory = System.IO.Path.GetDirectoryName(this.Path);

                return matcher.GetResultsInFullPath(searchDirectory);
            }
        }
    }
}
