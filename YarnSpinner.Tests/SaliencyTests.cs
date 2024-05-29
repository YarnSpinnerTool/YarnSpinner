using Xunit;
using FluentAssertions;

using Yarn.Compiler;
using Yarn.Saliency;
using Xunit.Abstractions;

using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Yarn;

namespace YarnSpinner.Tests
{


    public class SaliencyTests : TestBase
    {
        public SaliencyTests(ITestOutputHelper outputHelper) : base(outputHelper)
        {
        }

        private CompilationResult CompileAndPrepareDialogue(string source, string node = "Start") {
            var job = CompilationJob.CreateFromString("input", source, allowPreviewFeatures: true);
            var result = Compiler.Compile(job);
            result.Diagnostics.Should().BeEmpty();

            this.dialogue.SetProgram(result.Program);
            this.dialogue.SetNode(node);

            this.dialogue.LineHandler = (line) => {};
            this.dialogue.OptionsHandler = (opts) => this.dialogue.SetSelectedOption(opts.Options.First().ID);
            this.dialogue.CommandHandler = (cmd) => {};
            this.dialogue.NodeStartHandler = (node) => {};
            this.dialogue.NodeCompleteHandler = (node) => {};
            this.dialogue.DialogueCompleteHandler = () => {};

            return result;

        }

        [Fact]
        public void TestMocking()
        {
            // Given
            var mockSaliencyStrategy = new Mock<IContentSaliencyStrategy>(MockBehavior.Strict);
            
            // Create a mock saliency strategy that mimics the
            // FirstContentStrategy (i.e. it returns the first item in the list,
            // every time)
            mockSaliencyStrategy.Setup(
                s => s.ChooseBestContent(It.IsAny<IEnumerable<IContentSaliencyOption>>())).Returns((IEnumerable<IContentSaliencyOption> a) => a.First()
            );

            var item = new Mock<IContentSaliencyOption>();

            // When
            var result = mockSaliencyStrategy.Object.ChooseBestContent(new[] { item.Object });

            // Then
            result.Should().Be(item.Object);
            mockSaliencyStrategy.Verify(
                s => s.ChooseBestContent(
                    It.Is<IEnumerable<IContentSaliencyOption>>(e => e.Count() == 1)
                )
            );
            mockSaliencyStrategy.VerifyNoOtherCalls();
        }

        [Fact]
        public void TestConditionCounts()
        {
            // Given
            var source = @"
title: Start
---
<<set $condition = true>>
<<jump NodeGroup>>
===
title: NodeGroup
when: $condition
expected: 1
---
<<stop>>
===
title: NodeGroup
when: $condition is true
expected: 1
---
<<stop>>
===
title: NodeGroup
when: once
expected: 1
---
<<stop>>
===
title: NodeGroup
when: once if $condition
expected: 2
---
<<stop>>
===
title: NodeGroup
when: once if $condition && true
expected: 3
---
<<stop>>
===
title: NodeGroup
when: once if $condition && true
when: always
expected: 3
---
<<stop>>
===
title: NodeGroup
when: always
expected: 0
---
<<stop>>
===
title: NodeGroup
when: $condition && ($condition || false)
expected: 3
---
<<stop>>
===
title: NodeGroup
when: demo_function($condition && true)
expected: 2
---
<<stop>>
===
";

            var mockSaliencyStrategy = new Mock<IContentSaliencyStrategy>(MockBehavior.Strict);

            var content = new List<IContentSaliencyOption>();

            // Create a mock saliency strategy that mimics the
            // FirstContentStrategy (i.e. it returns the first item in the list,
            // every time)
            mockSaliencyStrategy.Setup(
                s => s.ChooseBestContent(It.IsAny<IEnumerable<IContentSaliencyOption>>()))
                    .Callback<IEnumerable<IContentSaliencyOption>>(p =>
                    {
                        content.AddRange(p);
                    })
                    .Returns((IEnumerable<IContentSaliencyOption> a) => a.First()
            );

            dialogue.Library.RegisterFunction("demo_function", (bool a) => {return true;});

            var result = CompileAndPrepareDialogue(source);

            var expectedComplexities = new Dictionary<string, int>();

            int nodesInNodeGroup = 0;
            string nodeGroupName = "NodeGroup";

            foreach (var node in result.Program.Nodes) {
                var nodeGroupHeader = node.Value.Headers.SingleOrDefault(h => h.Key == SpecialHeaderNames.NodeGroupHeader);

                if (nodeGroupHeader == null) {
                    continue;
                }

                nodeGroupHeader.Value.Should().Be(nodeGroupName);

                var expectedTag = node.Value.Headers.SingleOrDefault(h => h.Key == "expected") 
                    ?? throw new Exception("Node " + node.Key + " is in node group but lacks an 'expected' header");
                
                expectedComplexities[node.Key] = int.Parse(expectedTag.Value);

                nodesInNodeGroup += 1;
            }

            this.dialogue.ContentSaliencyStrategy = mockSaliencyStrategy.Object;

            // When
            this.dialogue.Continue();

            // Then
            // The saliency strategy was invoked one time
            mockSaliencyStrategy.Verify(
                s => s.ChooseBestContent(
                    It.IsAny<IEnumerable<IContentSaliencyOption>>()
                ), Times.Once
            );

            // The saliency strategy was given two options to choose from
            content.Should().HaveCount(nodesInNodeGroup, $"there are {nodesInNodeGroup} nodes in the node group");

            // All options had a complexity of 1
            content.Should()
                   .AllSatisfy(c => c.ConditionValueCount
                    .Should().Be(expectedComplexities[c.ContentID], 
                        $"{c.ContentID} should have complexity {expectedComplexities[c.ContentID]}"));
        }
    }

}
