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
        public void TestLoadingNodes()
        {
            var path = Path.Combine(TestDataPath, "Projects", "Basic", "Test.yarn");
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));
            
            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            // high-level test: load the file, verify it has the nodes we want,
            // and run one
            
            Assert.Equal(3, dialogue.allNodes.Count());

            Assert.True(dialogue.NodeExists("TestNode"));
            Assert.True(dialogue.NodeExists("AnotherTestNode"));
            Assert.True(dialogue.NodeExists("ThirdNode"));
            
        }
    }
}
