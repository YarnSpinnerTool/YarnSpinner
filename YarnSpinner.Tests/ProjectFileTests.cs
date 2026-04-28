using FluentAssertions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Yarn.Compiler;


namespace YarnSpinner.Tests
{

    public class ProjectFileTests : AsyncTestBase
    {
        public ProjectFileTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        private static string ProjectFilePath => Path.Combine(SpaceDemoScriptsPath, "Space.yarnproject");
        private static string ProjectFolderPath => Path.GetDirectoryName(ProjectFilePath);

        [Fact]
        public void TestProjectFilesCanBeLoaded()
        {
            // Given
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);

            // Then
            using (new FluentAssertions.Execution.AssertionScope())
            {
                project.FileVersion.Should().Be(4);
                project.Path.Should().Be(ProjectFilePath);
                project.SourceFilePatterns.Should().ContainSingle("**/*.yarn");

                project.BaseLanguage.Should().Be("en");
                project.Definitions.Should().BeEquivalentTo(["Commands.ysls.json"]);

                project.Localisation.Should().ContainKey("en").WhoseValue.Assets.Should().Be("../LocalisedAssets/English");
                project.Localisation.Should().ContainKey("en").WhoseValue.Strings.Should().BeNull();

                project.Localisation.Should().ContainKey("de").WhoseValue.Assets.Should().Be("../LocalisedAssets/German");
                project.Localisation.Should().ContainKey("de").WhoseValue.Strings.Should().Be("../German.csv");
            }
        }

        [Fact]
        public void TestProjectsCanFindFiles()
        {
            // Given
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);

            // When
            IEnumerable<string> files = project.SourceFiles;
            IEnumerable<string> relativeFiles = files.Select(f => Path.GetRelativePath(ProjectFolderPath, f));

            // Then
            relativeFiles.Should().Contain("Sally.yarn");
            relativeFiles.Should().Contain("Ship.yarn");
            relativeFiles.Should().NotContain("Space.yarnproject");

        }

        [Fact]
        public void TestProjectsCanExcludeFiles()
        {
            // Given
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);
            project.ExcludeFilePatterns = new[] { "Ship.yarn" };

            // When
            IEnumerable<string> files = project.SourceFiles;
            IEnumerable<string> relativeFiles = files.Select(f => Path.GetRelativePath(ProjectFolderPath, f));

            // Then
            relativeFiles.Should().Contain("Sally.yarn");
            relativeFiles.Should().NotContain("Ship.yarn");
            relativeFiles.Should().NotContain("Space.yarnproject");
        }

        [Fact]
        public void TestProjectsCanSave()
        {
            // Given
            var project = new Yarn.Compiler.Project();
            var path = Path.GetTempFileName();
            System.Console.WriteLine($"Temporary file for {nameof(TestProjectsCanSave)} is {path}");

            // When
            project.SaveToFile(path);
            var loadedProject = Yarn.Compiler.Project.LoadFromFile(path);

            // Then
            loadedProject.Should().BeEquivalentTo(
                project,
                (options) => options
                    .Excluding(o => o.Path) // paths will be different
                    .Excluding(o => o.SourceFiles) // source files will be different (because paths are different)
            );
        }

        [Fact]
        public void TestProjectsCanBeModifiedAndSaved()
        {
            // Given
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);

            project.Localisation.Add("fr", new Yarn.Compiler.Project.LocalizationInfo
            {
                Strings = "French.csv",
                Assets = "./French/"
            });

            var path = Path.GetTempFileName();
            System.Console.WriteLine($"Temporary file for {nameof(TestProjectsCanBeModifiedAndSaved)} is {path}");

            // When
            project.SaveToFile(path);
            var newProject = Yarn.Compiler.Project.LoadFromFile(path);

            // Then
            newProject.Should().BeEquivalentTo(
                project,
                (options) => options
                    .Excluding(o => o.Path) // paths will be different
                    .Excluding(o => o.SourceFiles) // source files will be different (because paths are different)
                    .Excluding(o => o.DefinitionsFiles) // path is different
                    .Excluding(o => o.DefinitionsPath) // path is different
                    .Excluding(o => o.Definitions) // path is different
            );
        }

        [Fact]
        public void TestProjectFilesCanAllowPreviewFeatures()
        {
            var projectSource = @"
            {
                ""projectFileVersion"": 2,
                ""sourceFiles"": [""**/*.yarn""],
                ""baseLanguage"": ""en"",
                ""compilerOptions"": {
                    ""allowPreviewFeatures"": true
                }
            }";

            var project = Project.LoadFromString(projectSource, "");

            project.AllowLanguagePreviewFeatures.Should().BeTrue();
        }

        [Fact]
        public void TestProjectFilesCanSpecifyDefinitionsAsStringOrList()
        {
            var projectSourceV3 = @"
            {
                ""projectFileVersion"": 3,
                ""sourceFiles"": [""**/*.yarn""],
                ""baseLanguage"": ""en"",
                ""definitions"": ""A.ysls.json""
            }";

            var projectSourceV4 = @"
            {
                ""projectFileVersion"": 4,
                ""sourceFiles"": [""**/*.yarn""],
                ""baseLanguage"": ""en"",
                ""definitions"": [""A.ysls.json""]
            }";

            var projectV3 = Project.LoadFromString(projectSourceV3, ".");
            var projectV4 = Project.LoadFromString(projectSourceV4, ".");

            projectV3.Definitions.Should().BeEquivalentTo(projectV4.Definitions);
        }

        [Fact]
        public void TestProjectFilesCanSpecifyDiagnosticSeverityOverrides()
        {
            var projectSource = @"{
             ""projectFileVersion"": 4,
                ""sourceFiles"": [""**/*.yarn""],
                ""baseLanguage"": ""en"",
                ""compilerOptions"": {
                    ""diagnosticsSeverity"": {
                        ""YS0001"": ""error"",
                        ""YS0002"": ""warning"",
                        ""YS0003"": ""info"",
                        ""YS0004"": ""none"",
                    }
                }
            }";

            var project = Project.LoadFromString(projectSource, ".");

            project.CompilerOptions.DiagnosticsSeverity.Should().HaveCount(4);
            project.CompilerOptions.DiagnosticsSeverity.Should().ContainKey("YS0001")
                .WhoseValue.Should().Be(Diagnostic.DiagnosticSeverity.Error);
            project.CompilerOptions.DiagnosticsSeverity.Should().ContainKey("YS0002")
                .WhoseValue.Should().Be(Diagnostic.DiagnosticSeverity.Warning);
            project.CompilerOptions.DiagnosticsSeverity.Should().ContainKey("YS0003")
                .WhoseValue.Should().Be(Diagnostic.DiagnosticSeverity.Info);
            project.CompilerOptions.DiagnosticsSeverity.Should().ContainKey("YS0004")
                .WhoseValue.Should().Be(Diagnostic.DiagnosticSeverity.None);
        }
    }
}
