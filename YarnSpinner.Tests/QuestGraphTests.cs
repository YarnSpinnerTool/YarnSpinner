using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using Yarn;
using Yarn.Compiler;
using Yarn.Saliency;

namespace YarnSpinner.Tests
{


    public class QuestGraphTests : TestBase
    {
        private static string ProjectFilePath => Path.Combine(TestDataPath, "Projects", "QuestGraphs", "QuestGraphProject.yarnproject");

        public QuestGraphTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        [Fact]
        public void QuestGraphProjectFilesCanBeLoaded()
        {
            // Given
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);

            project.CompilerOptions.TryGetValue("questGraphs", out var info).Should().BeTrue();

            info.TryGetProperty("files", out var pattern).Should().BeTrue();
            pattern.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
            var patternEntries = pattern.EnumerateArray().Select(a => a.GetString());

            var graphs = project.ResolvePaths(patternEntries);

            graphs.Should().ContainSingle();
            graphs.Should().AllSatisfy(g => g.Should().EndWith(".questgraph"));

            var graphText = File.ReadAllText(graphs.Single());

            var questGraph = Yarn.QuestGraphs.QuestGraph.Parser.ParseJson(graphText);
        }

        [Fact]
        public void QuestGraphProjectFilesCanGenerateSource()
        {
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);

            var graphs = project.QuestGraphPaths;
            graphs.Should().ContainSingle().Which.Should().EndWith(".questgraph");
            var graphText = File.ReadAllText(graphs.Single());

            var questGraph = Yarn.QuestGraphs.QuestGraph.Parser.ParseJson(graphText);

            var text = questGraph.GetYarnDefinitionScript();

            text.Should().NotBeNull();
        }

        [Fact]
        public void QuestGraphContentIsIncludedInCompilation()
        {
            Yarn.Compiler.Project project = Yarn.Compiler.Project.LoadFromFile(ProjectFilePath);

            var questGraphContents = File.ReadAllText(project.QuestGraphPaths.Single());
            var questGraph = Yarn.QuestGraphs.QuestGraph.Parser.ParseJson(questGraphContents);
            questGraph.Should().NotBeNull();

            var job = CompilationJob.CreateFromProject(project);
            var result = Compiler.Compile(job);

            result.Diagnostics.Should().BeEmpty();

            questGraph.Nodes.Should().NotBeEmpty();
            questGraph.Nodes.Should().AllSatisfy(questNode =>
            {
                var nodeReachableVariable = questGraph.GetNodeVariableName(questNode, Yarn.QuestGraphs.NodeStateType.Reachable);
                result.Program.Nodes.Should().Contain(yarnNode => yarnNode.Value.Name == nodeReachableVariable);
            }, "every node the quest graph should have a 'reachable' smart variable defined");
        }
    }
}

