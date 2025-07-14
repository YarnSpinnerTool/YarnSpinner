using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using YarnLanguageServer.Handlers;

#pragma warning disable VSTHRD200 // async method names should end with "Async"

namespace YarnLanguageServer.Tests
{
    public class ReferenceTests(ITestOutputHelper outputHelper) : LanguageServerTestsBase(outputHelper)
    {
        [Fact]
        public async Task Workspace_FindsReferencesToNodes()
        {
            // Given
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);
            var filePath = Path.Combine(TestUtility.PathToTestWorkspace, "JumpsAndDetours", "JumpsAndDetours.yarn");

            // When
            var workspace = server.GetRequiredService<Workspace>();
            var project = workspace.Projects.Single(p => p.Uri?.Path.EndsWith("JumpsAndDetours.yarnproject") ?? false);

            // Node1 jumps to Node2
            // Node2 detours to Node3
            // Node4 jumps to Node1 and Node2

            // Then

            (IEnumerable<Location> References, IEnumerable<NodeJump> Jumps) GetReferencesAndJumps(string name)
            {
                IEnumerable<Location> references = ReferencesHandler.GetReferences(project, name, YarnSymbolType.Node);
                IEnumerable<NodeJump> jumps = project.Nodes.Single(n => n.UniqueTitle == name).Jumps;
                return (references, jumps);
            }

            var node1 = GetReferencesAndJumps("Node1");
            var node2 = GetReferencesAndJumps("Node2");
            var node3 = GetReferencesAndJumps("Node3");
            var node4 = GetReferencesAndJumps("Node4");

            using (new FluentAssertions.Execution.AssertionScope())
            {

                node1.References.Should().HaveCount(2);
                node1.Jumps.Should().HaveCount(2);
                node1.Jumps.Should().Contain(j => j.DestinationTitle == "Node2" && j.Type == NodeJump.JumpType.Jump);

                node2.References.Should().HaveCount(3);
                node2.Jumps.Should().HaveCount(1);
                node2.Jumps.Should().Contain(j => j.DestinationTitle == "Node3" && j.Type == NodeJump.JumpType.Detour);

                node3.References.Should().HaveCount(2);
                node3.Jumps.Should().HaveCount(0);

                node4.References.Should().HaveCount(1);
                node4.Jumps.Should().HaveCount(2);
                node4.Jumps.Should().Contain(j => j.DestinationTitle == "Node1" && j.Type == NodeJump.JumpType.Jump);
                node4.Jumps.Should().Contain(j => j.DestinationTitle == "Node2" && j.Type == NodeJump.JumpType.Jump);
            }
        }
    }
}
