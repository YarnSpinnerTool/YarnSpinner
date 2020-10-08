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
            
            Assert.Equal(3, dialogue.NodeNames.Count());

            Assert.True(dialogue.NodeExists("TestNode"));
            Assert.True(dialogue.NodeExists("AnotherTestNode"));
            Assert.True(dialogue.NodeExists("ThirdNode"));
            
        }

        [Fact]
        public void TestDeclarationFilesAreGenerated()
        {
            // Parsing a file that contains variable declarations should be
            // able to turned back into a string containing the same
            // information.
            
            var originalText = @"title: Program
tags: one two
custom: yes
---
<<declare $str = ""str"" ""str desc"">>
<<declare $num = 2 ""num desc"">>
<<declare $bool = true ""bool desc"">>
===
";

            var job = CompilationJob.CreateFromString("input", originalText);

            var result = Compiler.Compile(job);

            var headers = new Dictionary<string,string> {
                { "custom", "yes"}
            };
            string[] tags = new[] { "one", "two" };

            var generatedOutput = Utility.GenerateYarnFileWithDeclarations(result.Declarations, "Program", tags, headers);

            Assert.Equal(originalText, generatedOutput);
        }
    }
}
