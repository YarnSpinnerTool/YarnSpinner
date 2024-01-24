using Xunit;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using Yarn.Compiler;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace YarnSpinner.Tests
{


    public class ProjectTests : TestBase
    {
		
        [Fact]
        public void TestLoadingNodes()
        {
            var path = Path.Combine(TestDataPath, "Projects", "Basic", "Test.yarn");
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            result.Diagnostics.Should().BeEmpty();
            
            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            // high-level test: load the file, verify it has the nodes we want,
            // and run one
            
            dialogue.NodeNames.Count().Should().Be(3);

            dialogue.NodeExists("TestNode").Should().BeTrue();
            dialogue.NodeExists("AnotherTestNode").Should().BeTrue();
            dialogue.NodeExists("ThirdNode").Should().BeTrue();
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

            result.Diagnostics.Should().BeEmpty();

            var headers = new Dictionary<string,string> {
                { "custom", "yes"}
            };
            string[] tags = new[] { "one", "two" };

            var generatedOutput = Utility.GenerateYarnFileWithDeclarations(result.Declarations, "Program", tags, headers);

            generatedOutput.Should().Be(originalText);
        }

        [Fact]
        public void TestLineCollisionTagging()
        {
            var paths = new List<string>()
            {
                Path.Combine(TestDataPath, "TestCases", "Duplicates", "lipsum1.yarn"),
                Path.Combine(TestDataPath, "TestCases", "Duplicates", "lipsum2.yarn"),
                Path.Combine(TestDataPath, "TestCases", "Duplicates", "lipsum3.yarn"),
            };

            // compiling the untagged but heavily duped files
            // there should be no errors and no tags
            var compilationJob = CompilationJob.CreateFromFiles(paths);
            compilationJob.CompilationType = CompilationJob.Type.StringsOnly;
            var result = Compiler.Compile(compilationJob);

            result.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error).Should().Be(false);

            var totalUntaggedLines = result.StringTable.Where(i => i.Value.isImplicitTag).Count();
            var totalLines = result.StringTable.Count();
            // at this stage these should be the same
            totalUntaggedLines.Should().Be(totalLines);

            var existingTags = result.StringTable.Where(i => i.Value.isImplicitTag == false).Select(i => i.Key).ToList();
            existingTags.Should().BeEmpty();

            // now we tag every line
            // combine that into a new compilation job
            // and see if there are any dupes
            var taggedLineContent = new List<string>();
            foreach (var path in paths)
            {
                var content = File.ReadAllText(path);
                
                // this is the older failing version
                // var taggedVersion = Utility.AddTagsToLines(content, existingTags);
                
                var tagged = Utility.TagLines(content, existingTags);
                var taggedVersion = tagged.Item1;

                // if it is null it means we have an error
                taggedVersion.Should().NotBeNull();
                taggedLineContent.Add(taggedVersion);

                // this is the fix
                existingTags = tagged.Item2 as List<string>;
            }
            // this is a bit inelegant but I don't want to write to disk
            var taggedContent = string.Join("\n",taggedLineContent);

            compilationJob = CompilationJob.CreateFromString("tagged", taggedContent);
            result = Compiler.Compile(compilationJob);
            
            // we should have no errors
            result.Diagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error).Should().Be(false);

            // we should have as many lines as we did originally
            var taggedLinesCount = result.StringTable.Count();
            taggedLinesCount.Should().Be(totalLines);

            // we should have no untagged lines
            result.StringTable.Where(l => l.Value.isImplicitTag).Should().BeEmpty();
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

// single symbol tests
🧑🏾‍❤️‍💋‍🧑🏻
🧑🏾‍❤️‍💋‍🧑🏻#line:abc122
🧑🏾‍❤️‍💋‍🧑🏻 // with comment
🧑🏾‍❤️‍💋‍🧑🏻 #line:abc124 // with a comment

// after emoji tests
🧑🏾‍❤️‍💋‍🧑🏻 text after emoji
🧑🏾‍❤️‍💋‍🧑🏻 text after emoji // with a comment
🧑🏾‍❤️‍💋‍🧑🏻 text after emoji #line:abc125
🧑🏾‍❤️‍💋‍🧑🏻 text after emoji #line:abc126 // with a comment

// before emoji tests
text before emoji 🧑🏾‍❤️‍💋‍🧑🏻
text before emoji 🧑🏾‍❤️‍💋‍🧑🏻 // with a comment
text before emoji 🧑🏾‍❤️‍💋‍🧑🏻 #line:abc127
text before emoji 🧑🏾‍❤️‍💋‍🧑🏻 #line:abc128 // with a comment

// emoji between tests
before 🧑🏾‍❤️‍💋‍🧑🏻after
before 🧑🏾‍❤️‍💋‍🧑🏻after #line:abc129
before 🧑🏾‍❤️‍💋‍🧑🏻after // with a comment
before 🧑🏾‍❤️‍💋‍🧑🏻after #line:abc130 // with a comment

// multi-moji tests
🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻
🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻 // with a comment
🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻 #line:abc131
🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻 #line:abc132 // with a comment

// testing command structures to make sure the tagger hasn't botched the whitespace
<<declare $a = 0>>
<<set $a to 5>>
<<if $a == 5>>
<<generic command goes here>>
<<endif>>
===";

            {
                // This original input should compile without errors.
                var originalCompilationJob = CompilationJob.CreateFromString("input", originalText);
                originalCompilationJob.CompilationType = CompilationJob.Type.StringsOnly;

                var originalCompilationResult = Compiler.Compile(originalCompilationJob);

                originalCompilationResult.Diagnostics.Should().BeEmpty();
            }

            // Act

            var output = Utility.AddTagsToLines(originalText);

            var compilationJob = CompilationJob.CreateFromString("input", output);
            compilationJob.CompilationType = CompilationJob.Type.StringsOnly;

            var compilationResult = Compiler.Compile(compilationJob);

            compilationResult.Diagnostics.Should().BeEmpty();

            // Assert
            var lineTagRegex = new Regex(@"#line:\w+");

            var lineTagAfterComment = new Regex(@"\/\/.*#line:\w+");

            // Ensure that the right number of tags in total is present
            var expectedExistingTags = 17;
            var expectedNewTags = 17;
            var expectedTotalTags = expectedExistingTags + expectedNewTags;

            var lineTagRegexMatches = lineTagRegex.Matches(output).Count;
            lineTagRegexMatches.Should().Be(expectedTotalTags);

            // No tags were added after a comment
            foreach (var line in output.Split('\n'))
            {
                lineTagAfterComment.IsMatch(line).Should().BeFalse($"'{line}' should not contain a tag after a comment");
            }

            var expectedResults = new List<(string tag, string line)>
            {
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

                // single symbol tests
                (null, "🧑🏾‍❤️‍💋‍🧑🏻"),
                (null, "🧑🏾‍❤️‍💋‍🧑🏻"),
                ("line:abc122", "🧑🏾‍❤️‍💋‍🧑🏻"),
                ("line:abc124", "🧑🏾‍❤️‍💋‍🧑🏻"),

                // // after emoji tests
                (null, "🧑🏾‍❤️‍💋‍🧑🏻 text after emoji"),
                (null, "🧑🏾‍❤️‍💋‍🧑🏻 text after emoji"),
                ("line:abc125", "🧑🏾‍❤️‍💋‍🧑🏻 text after emoji"),
                ("line:abc126", "🧑🏾‍❤️‍💋‍🧑🏻 text after emoji"),

                // // before emoji tests
                (null, "text before emoji 🧑🏾‍❤️‍💋‍🧑🏻"),
                (null, "text before emoji 🧑🏾‍❤️‍💋‍🧑🏻"),
                ("line:abc127", "text before emoji 🧑🏾‍❤️‍💋‍🧑🏻"),
                ("line:abc128", "text before emoji 🧑🏾‍❤️‍💋‍🧑🏻"),

                // // emoji between tests
                (null, "before 🧑🏾‍❤️‍💋‍🧑🏻after"),
                ("line:abc129", "before 🧑🏾‍❤️‍💋‍🧑🏻after"),
                (null, "before 🧑🏾‍❤️‍💋‍🧑🏻after"),
                ("line:abc130", "before 🧑🏾‍❤️‍💋‍🧑🏻after"),

                // // multi-moji tests
                (null, "🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻"),
                (null, "🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻"),
                ("line:abc131", "🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻"),
                ("line:abc132", "🧑🏾‍❤️‍💋‍🧑🏻🧑🏾‍❤️‍💋‍🧑🏻"),
            };
            expectedResults.Sort((a,b) => {
                if (a.tag == null)
                {
                    if (b.tag == null)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else if (b.tag == null)
                {
                    return -1;
                }
                return a.tag.CompareTo(b.tag);
            });
            
            lineTagRegexMatches.Should().Be(expectedResults.Count);

            // used to keep track of all line ids we have already seen
            // this is because we need to make sure we see every line in the string table
            HashSet<string> visitedIDs = new HashSet<string>();

            foreach (var result in expectedResults)
            {
                if (result.tag != null)
                {
                    result.line.Should().Be(compilationResult.StringTable[result.tag].text);
                    // flagging this ID as having been visited
                    visitedIDs.Add(result.tag);
                }
                else
                {
                    // a line exists that has this text
                    var matchingEntries = compilationResult.StringTable.Where(s => s.Value.text == result.line).Where(s => !visitedIDs.Contains(s.Key));
                    matchingEntries.Should().NotBeEmpty();

                    // that line has a line tag
                    var lineTag = matchingEntries.First().Key;
                    lineTag.Should().StartWith("line:");

                    // that line is not a duplicate of any other line tag
                    var allLineTags = compilationResult.StringTable.Keys;
                    allLineTags.Count(t => t == lineTag).Should().Be(1);

                    // flagging this ID as having been visited
                    visitedIDs.Add(lineTag);
                }
            }

            // we now should have seen every line ID
            compilationResult.StringTable.Count.Should().Be(expectedResults.Count);
            compilationResult.StringTable.Count.Should().Be(visitedIDs.Count);
        }

        [Fact]
        public void TestDebugOutputIsProduced()
        {
            var input = CreateTestNode(@"This is a test node.", "DebugTesting");

            var compilationJob = CompilationJob.CreateFromString("input", input);

            var compilationResult = Compiler.Compile(compilationJob);

            // We should have a single DebugInfo object, because we compiled a
            // single node
            compilationResult.DebugInfo.Should().NotBeNull();
            compilationResult.DebugInfo.Should().ContainSingle();

            // The first instruction of the only node should begin on the third
            // line
            var firstLineInfo = compilationResult.DebugInfo.First().Value.GetLineInfo(0);

            firstLineInfo.FileName.Should().Be("input");
            firstLineInfo.NodeName.Should().Be("DebugTesting");
            firstLineInfo.LineNumber.Should().Be(2);
            firstLineInfo.CharacterNumber.Should().Be(0);
        }
    }
}
