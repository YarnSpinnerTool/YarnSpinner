using Xunit;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using Yarn.Compiler;
using System.Linq;

namespace YarnSpinner.Tests
{


    public class ProjectTests : TestBase
    {
		
        [Fact]
        public void TestLoadingSingleFile()
        {
            var path = Path.Combine(TestDataPath, "Projects", "Basic", "Test.yarn.txt");

            Compiler.CompileFile(path, out var program, out stringTable);

            // high-level test: load the file, verify it has the nodes we want,
            // and run one
            dialogue.SetProgram(program);

            Assert.Equal(3, dialogue.allNodes.Count());

            Assert.True(dialogue.NodeExists("TestNode"));
            Assert.True(dialogue.NodeExists("AnotherTestNode"));
            Assert.True(dialogue.NodeExists("ThirdNode"));

            // execute a node
            var secondNodeName = "AnotherTestNode";
            ExpectLine("This is a second test node!");

            // we are now expecting a node
            Assert.True(this.IsExpectingLine);

            RunStandardTestcase(secondNodeName);

            // the line should have run, so this property should become true
            Assert.False(this.IsExpectingLine);

            // and the variable $x should now have a value
            Assert.Equal(23, (int)dialogue.continuity.GetValue("$x").AsNumber);
        }
    }
}