using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;
using Yarn;
using Yarn.Compiler;

#nullable enable

namespace YarnSpinner.Tests
{
    public class QuestGraphTests : TestBase
    {
        public QuestGraphTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

        [Fact]
        public void TestGraphEdgesCanBeParsed()
        {
            // Given
            var edgeWithoutCondition = QuestGraphEdge.Parse("quest Q:A -- Q:B");
            var edgeWithCondition = QuestGraphEdge.Parse("quest Q:A -- Q:B when C");

            // Then
            edgeWithoutCondition.FromNode.Name.Should().Be("A");
            edgeWithoutCondition.ToNode.Name.Should().Be("B");
            edgeWithoutCondition.Requirement.Should().BeNull();

            edgeWithCondition.FromNode.Name.Should().Be("A");
            edgeWithCondition.ToNode.Name.Should().Be("B");
            edgeWithCondition.Requirement.Should().Be("C");
        }

        [Fact]
        public void TestGraphCanBeExtracted()
        {
            // Given
            var source = @"title: Start
---

Mayor: you gotta go deal with this dragon!

/// the player learns about the dragon
<<quest TheDragon:Step:Start -- TheDragon:Step:DealWithDragon>>
<<quest TheDragon:Step:DealWithDragon -- TheDragon:Task:SlayDragon>>
<<quest TheDragon:Step:DealWithDragon -- TheDragon:Task:BefriendDragon>>

-> Slay Dragon
    /// dragon is slain
    <<quest TheDragon:Task:SlayDragon -- TheDragon:Step:DragonDead>>
-> Befriend Dragon
    /// dragon is befriended
    <<quest TheDragon:Task:BefriendDragon -- TheDragon:Step:DragonBefriended>>
===";

            var (parseResult, diagnostics) = Yarn.Compiler.Utility.ParseSource(source);

            // When
            var visitor = new QuestGraphVisitor(parseResult, 3, [.. diagnostics], []);
            visitor.Visit(parseResult.Tree);

            // Then
            visitor.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

            visitor.Edges.Should().NotBeEmpty();

            var allNodes = new HashSet<QuestGraphNodeDescriptor>(
                visitor.Edges.SelectMany(e => new[] { e.FromNode, e.ToNode })
            );

            visitor.Edges.Should().Contain(e =>
                e.FromNode.Name == "SlayDragon" && e.ToNode.Name == "DragonDead" && e.Description == "dragon is slain"
            );

            var expectedElements = new[] {
                new QuestGraphNodeDescriptor("TheDragon:Step:Start"),
                new QuestGraphNodeDescriptor("TheDragon:Step:DealWithDragon"),
                new QuestGraphNodeDescriptor("TheDragon:Task:SlayDragon"),
                new QuestGraphNodeDescriptor("TheDragon:Task:BefriendDragon"),
                new QuestGraphNodeDescriptor("TheDragon:Step:DragonDead"),
                new QuestGraphNodeDescriptor("TheDragon:Step:DragonBefriended"),
            };

            allNodes.Should().Contain(expectedElements);
            allNodes.Should().HaveCount(expectedElements.Length);
        }

        [Fact]
        public void TestNodesCanBeParsed()
        {

            var fullyQualified = new QuestGraphNodeDescriptor("TheDragon:Step:DealWithDragon");
            fullyQualified.Quest.Should().Be("TheDragon");
            fullyQualified.Type.Should().Be(QuestGraphNodeDescriptor.NodeType.Step);
            fullyQualified.Name.Should().Be("DealWithDragon");

            var partiallyQualified = new QuestGraphNodeDescriptor("TheDragon:DealWithDragon");
            partiallyQualified.Quest.Should().Be("TheDragon");
            partiallyQualified.Type.Should().Be(QuestGraphNodeDescriptor.NodeType.Step);
            partiallyQualified.Name.Should().Be("DealWithDragon");
        }

        [Fact]
        public void TestGraphEdgesEmitVariableSets()
        {
            var source = @"title: Start
---
<<quest Q:A -- Q:B>>
===";

            var job = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().NotContain(a => a.Severity == Diagnostic.DiagnosticSeverity.Error);

            var node = result.Program!.Nodes["Start"];

            node.Instructions.Should().NotContain(i => i.InstructionTypeCase == Yarn.Instruction.InstructionTypeOneofCase.RunCommand);
            node.Instructions.Should().Contain(i => i.InstructionTypeCase == Yarn.Instruction.InstructionTypeOneofCase.StoreVariable);
        }

        [Fact]
        public void TestGraphEdgesCreateVariables()
        {
            var source = @"title: Start
---
<<quest Q:A -- Q:B>>
<<quest Q:C -- Q:D when $condition>>
===";

            var job = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().NotContain(a => a.Severity == Diagnostic.DiagnosticSeverity.Error);

            result.Declarations.Should().Contain(v => v.Name == "$QA_QB");
            result.Declarations.Should().Contain(v => v.Name == "$condition");
        }

        [Fact]
        public void TestGraphEdgesCanHaveExternalConditions()
        {
            var source = @"title: Start
---
<<quest Q:A -- Q:B when $condition>>
===";

            var job = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().NotContain(a => a.Severity == Diagnostic.DiagnosticSeverity.Error);

            result.QuestGraphEdges.Should().Contain(e => e.Requirement == "$condition");
        }

        [Fact]
        public void TestGraphEdgesCanBeRepeated()
        {
            // Given
            var source = @"title: Start
---
<<quest Q:A -- Q:B>>
<<quest Q:A -- Q:B>>
<<quest Q:B -- Q:C>>
===";

            var job = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().NotContain(a => a.Severity == Diagnostic.DiagnosticSeverity.Error);

            // When

            // Then
            result.QuestGraphEdges.Should().HaveCount(2);
        }

        [Fact]
        public void TestRedundantGraphEdgesMustNotHaveConditions()
        {
            // Given
            var source = @"title: Start
---
<<quest Q:A -- Q:B>>
<<quest Q:A -- Q:B when $x>>
===";

            var job = CompilationJob.CreateFromString("input", source);
            var result = Compiler.Compile(job);

            var d = result.Diagnostics.Should().Contain(a => a.Severity == Diagnostic.DiagnosticSeverity.Error).Subject;
            d.Message.Should().Match("*Quest graph links can only be declared more than once if they don't have a requirement*");

        }
    }
}
