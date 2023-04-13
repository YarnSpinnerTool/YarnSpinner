using System.IO;
using FluentAssertions;
using Xunit;


namespace YarnSpinner.Tests
{

    public class ProjectFileTests : TestBase
    {
        [Fact]
        public void TestProjectFilesCanBeLoaded()
        {
            // Given
            var projectPath = Path.Combine(SpaceDemoScriptsPath, "Space.yarnproject");

            // When
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(projectPath);

            // Then
            project.FileVersion.Should().Be(2);
            project.Path.Should().Be(projectPath);
            project.SourceFilePatterns.Should().ContainSingle("**/*.yarn");

            project.BaseLanguage.Should().Be("en");
            project.Definitions.Should().Be("Functions.ysls.json");

            project.Localisation.Should().ContainKey("en").WhoseValue.Assets.Should().Be("../LocalisedAssets/English");
            project.Localisation.Should().ContainKey("en").WhoseValue.Strings.Should().BeNull();

            project.Localisation.Should().ContainKey("de").WhoseValue.Assets.Should().Be("../LocalisedAssets/German");
            project.Localisation.Should().ContainKey("de").WhoseValue.Strings.Should().Be("../German.csv");
        }

        
    }

}
