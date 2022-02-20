using Xunit;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using Yarn.Compiler;
using System.Linq;
using System.Text.RegularExpressions;

namespace YarnSpinner.Tests
{


    public class ProjectTests : TestBase
    {
		
        [Fact]
        public void TestLoadingNodes()
        {
            var path = Path.Combine(TestDataPath, "Projects", "Basic", "Test.yarn");
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);
            
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
/// str desc
<<declare $str = ""str"">>

/// num desc
<<declare $num = 2>>

/// bool desc
<<declare $bool = true>>
===
";

            var job = CompilationJob.CreateFromString("input", originalText);

            var result = Compiler.Compile(job);

            Assert.Empty(result.Diagnostics);

            var headers = new Dictionary<string,string> {
                { "custom", "yes"}
            };
            string[] tags = new[] { "one", "two" };

            var generatedOutput = Utility.GenerateYarnFileWithDeclarations(result.Declarations, "Program", tags, headers);

            Assert.Equal(originalText, generatedOutput);
        }

        [Fact]
        public void TestLineTagsAreAdded() {
            // Arrange
            var originalText = @"title: Program
---
// A comment. No line tag is added.
A single line, with no line tag.
A single line, with a line tag. #line:expected_abc123

-> An option, with no line tag.
-> An option, with a line tag. #line:expected_def456

A line with no tag, but a comment at the end. // a comment
A line with a tag, and a comment. #line:expected_ghi789 // a comment

A line with a conditional and no line tag. <<if false>>
A line with a conditional, a comment, and no line tag. <<if false>> // a comment

A line with a conditional and a line tag. <<if false>> #line:expected_jkl123
A line with a conditional, a comment and a line tag. <<if false>>  #line:expected_mno456 // a comment

-> An option with a conditional and no line tag. <<if false>>
-> An option with a conditional, a comment, and no line tag. <<if false>> // a comment
-> An option with a conditional and a line tag.  <<if false>> #line:expected_pqr789
-> An option with a conditional, a comment and a line tag.  <<if false>> #line:expected_stu123 // a comment

// A comment with no text:
//
// A comment with a single space:
// 

===";

            {
                // This original input should compile without errors.
                var originalCompilationJob = CompilationJob.CreateFromString("input", originalText);
                originalCompilationJob.CompilationType = CompilationJob.Type.StringsOnly;

                var originalCompilationResult = Compiler.Compile(originalCompilationJob);

                Assert.Empty(originalCompilationResult.Diagnostics);
            }

            // Act


            var output = Utility.AddTagsToLines(originalText);

            var compilationJob = CompilationJob.CreateFromString("input", output);
            compilationJob.CompilationType = CompilationJob.Type.StringsOnly;

            var compilationResult = Compiler.Compile(compilationJob);

            Assert.Empty(compilationResult.Diagnostics);

            // Assert
            var lineTagRegex = new Regex(@"#line:\w+");

            var lineTagAfterComment = new Regex(@"\/\/.*#line:\w+");

            // Ensure that the right number of tags in total is present
            var expectedExistingTags = 7;
            var expectedNewTags = 7;
            var expectedTotalTags = expectedExistingTags + expectedNewTags;

            Assert.Equal(expectedTotalTags, lineTagRegex.Matches(output).Count);

            // No tags were added after a comment
            foreach (var line in output.Split('\n')) {
                Assert.False(lineTagAfterComment.IsMatch(line), $"'{line}' should not contain a tag after a comment");
            }
                

            var expectedResults = new (string tag, string line)[] {
                ("line:expected_abc123", "A single line, with a line tag."),
                ("line:expected_def456", "An option, with a line tag."),
                ("line:expected_ghi789", "A line with a tag, and a comment."),
                
                (null, "A line with a conditional and no line tag."),
                (null, "A line with a conditional, a comment, and no line tag."),

                ("line:expected_jkl123", "A line with a conditional and a line tag."),
                ("line:expected_mno456", "A line with a conditional, a comment and a line tag."),

                (null, "An option with a conditional and no line tag."),
                (null, "An option with a conditional, a comment, and no line tag."),

                ("line:expected_pqr789", "An option with a conditional and a line tag."),
                ("line:expected_stu123", "An option with a conditional, a comment and a line tag."),

                (null, "A single line, with no line tag."),
                (null, "An option, with no line tag."),
                (null, "A line with no tag, but a comment at the end."),
            };

            foreach (var result in expectedResults) {
                if (result.tag != null) {
                    Assert.Equal(compilationResult.StringTable[result.tag].text, result.line);
                } else {
                    // a line exists that has this text
                    var matchingEntries = compilationResult.StringTable.Where(s => s.Value.text == result.line);
                    Assert.Single(matchingEntries);

                    // that line has a line tag
                    var lineTag = matchingEntries.First().Key;
                    Assert.StartsWith("line:", lineTag);

                    // that line is not a duplicate of any other line tag
                    var allLineTags = compilationResult.StringTable.Keys;
                    Assert.Equal(1, allLineTags.Count(t => t == lineTag));
                }
            }
            
        }

        [Fact]
        public void TestDebugOutputIsProduced()
        {
            var input = CreateTestNode(@"This is a test node.", "DebugTesting");

            var compilationJob = CompilationJob.CreateFromString("input", input);

            var compilationResult = Compiler.Compile(compilationJob);

            // We should have a single DebugInfo object, because we compiled a
            // single node
            Assert.NotNull(compilationResult.DebugInfo);
            Assert.Single(compilationResult.DebugInfo);

            // The first instruction of the only node should begin on the third
            // line
            var firstLineInfo = compilationResult.DebugInfo.First().Value.GetLineInfo(0);

            Assert.Equal("input", firstLineInfo.FileName);
            Assert.Equal("DebugTesting", firstLineInfo.NodeName);
            Assert.Equal(2, firstLineInfo.LineNumber);
            Assert.Equal(0, firstLineInfo.CharacterNumber);
        }
    }
}
