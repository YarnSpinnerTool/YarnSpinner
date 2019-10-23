using Xunit;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Yarn;

namespace YarnSpinner.Tests
{


    public class ProjectTests : TestBase
    {
		
        [Fact]
        public void TestLoadingSingleFile()
        {
            var path = Path.Combine(TestDataPath, "Projects", "Basic", "Test.yarn.txt");

            // high-level test: load the file, verify it has the nodes we want,
            // and run one
            dialogue.LoadFile(path);

            Assert.Equal(3, dialogue.program.Nodes.Count);

            Assert.True(dialogue.NodeExists("TestNode"));
            Assert.True(dialogue.NodeExists("AnotherTestNode"));
            Assert.True(dialogue.NodeExists("ThirdNode"));

            // execute a node
            var secondNodeName = "AnotherTestNode";
            ExpectLine("This is a second test node!");

            // we are now expecting a node
            Assert.True(this.isExpectingLine);

            RunStandardTestcase(secondNodeName);

            // the line should have run, so this property should become true
            Assert.False(this.isExpectingLine);

            // and the variable $x should now have a value
            Assert.Equal(23, (int)dialogue.continuity.GetValue("$x").AsNumber);

            // Next, we'll do a lower-level test, verifying that properties are loaded correctly

            // manually load the NodeInfos and verify data
            var text = File.ReadAllText(path);

            var nodes = dialogue.loader.GetNodesFromText(text, NodeFormat.Text);

            // first node has a colorID
            Assert.Equal(3, nodes[0].colorID);

            // second node has got a position defined
            var position = nodes[1].position;
            Assert.Equal(2, position.x);
            Assert.Equal(4, position.y);

            // third node has tags
            var expectedTags = new List<string>(new string[] { "multiple", "tags!"});

            Assert.Equal(expectedTags, nodes[2].tagsList);

            // the third node's body is empty
            Assert.Empty(nodes[2].body);
        }
    }
}