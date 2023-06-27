using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;


namespace YarnSpinner.Tests
{

    public class ProjectFileTests : TestBase
    {
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
                project.FileVersion.Should().Be(2);
                project.Path.Should().Be(ProjectFilePath);
                project.SourceFilePatterns.Should().ContainSingle("**/*.yarn");

                project.BaseLanguage.Should().Be("en");
                project.Definitions.Should().Be("Functions.ysls.json");

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
                    .Excluding(o => o.DefinitionsPath) // path is different
            );
        }
    }

}
