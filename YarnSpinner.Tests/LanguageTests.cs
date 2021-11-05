using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

using Yarn.Compiler;
using CLDRPlurals;

namespace YarnSpinner.Tests
{
	public class LanguageTests : TestBase
    {
		public LanguageTests() : base() {

            // Register some additional functions
            dialogue.Library.RegisterFunction("add_three_operands", delegate (int a, int b, int c) {
                return a + b + c;
            });
		}

        [Fact]
        public void TestExampleScript()
        {

            runtimeErrorsCauseFailures = false;
            var path = Path.Combine(TestDataPath, "Example.yarn");
            var testPath = Path.ChangeExtension(path, ".testplan");
            
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);
            
            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;
            
            this.LoadTestPlan(testPath);

            RunStandardTestcase();
        }

        [Fact]
        public void TestMergingNodes()
        {
            var sallyPath = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            var shipPath = Path.Combine(SpaceDemoScriptsPath, "Ship.yarn");

            CompilationJob compilationJobSally = CompilationJob.CreateFromFiles(sallyPath);
            CompilationJob compilationJobSallyAndShip = CompilationJob.CreateFromFiles(sallyPath, shipPath);
            
            compilationJobSally.Library = dialogue.Library;
            compilationJobSallyAndShip.Library = dialogue.Library;
            
            var resultSally = Compiler.Compile(compilationJobSally);
            var resultSallyAndShip = Compiler.Compile(compilationJobSallyAndShip);


            Assert.Empty(resultSally.Diagnostics);
            Assert.Empty(resultSallyAndShip.Diagnostics);

            // Loading code with the same contents should throw
            Assert.Throws<InvalidOperationException>(delegate ()
            {
                var combinedNotWorking = Program.Combine(resultSally.Program, resultSallyAndShip.Program);
            });
        }



        [Fact]
        public void TestEndOfNotesWithOptionsNotAdded()
        {
            var path = Path.Combine(TestDataPath, "SkippedOptions.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);
            
            dialogue.SetProgram(result.Program);
            stringTable = result.StringTable;

            dialogue.OptionsHandler = delegate (OptionSet optionSets) {
                Assert.False(true, "Options should not be shown to the user in this test.");
            };

            dialogue.SetNode();
            dialogue.Continue();

        }

        [Fact]
        public void TestNodeHeaders()
        {
            var path = Path.Combine(TestDataPath, "Headers.yarn");
            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.Empty(result.Diagnostics);
            
            Assert.Equal(4, result.Program.Nodes.Count);

            foreach (var tag in new[] {"one", "two", "three"}) {
                Assert.Contains(tag, result.Program.Nodes["Tags"].Tags);
            }

            // Assert.Contains("version:2", result.FileTags);
            Assert.Contains(path, result.FileTags.Keys);
            Assert.Single(result.FileTags);
            Assert.Single(result.FileTags[path]);
            Assert.Contains("file_header", result.FileTags[path]);
        }

        [Fact]
        public void TestInvalidCharactersInNodeTitle()
        {
            var path = Path.Combine(TestDataPath, "InvalidNodeTitle.yarn");

            var result = Compiler.Compile(CompilationJob.CreateFromFiles(path));

            Assert.NotEmpty(result.Diagnostics);

        }

        [Fact]
    public void TestNumberPlurals() {

            (string, double , PluralCase )[] cardinalTests = new[] {

                // English
                ("en", 1, PluralCase.One),
                ("en", 2, PluralCase.Other),
                ("en", 1.1, PluralCase.Other),

                // Arabic
                ("ar", 0, PluralCase.Zero),
                ("ar", 1, PluralCase.One),
                ("ar", 2, PluralCase.Two),
                ("ar", 3, PluralCase.Few),
                ("ar", 11, PluralCase.Many),
                ("ar", 100, PluralCase.Other),
                ("ar", 0.1, PluralCase.Other),

                // Polish
                ("pl", 1, PluralCase.One),
                ("pl", 2, PluralCase.Few),
                ("pl", 3, PluralCase.Few),
                ("pl", 4, PluralCase.Few),
                ("pl", 5, PluralCase.Many),
                ("pl", 1.1, PluralCase.Other),

                // Icelandic
                ("is", 1, PluralCase.One),
                ("is", 21, PluralCase.One),
                ("is", 31, PluralCase.One),
                ("is", 41, PluralCase.One),
                ("is", 51, PluralCase.One),
                ("is", 0, PluralCase.Other),
                ("is", 4, PluralCase.Other),
                ("is", 100, PluralCase.Other),
                ("is", 3.0, PluralCase.Other),
                ("is", 4.0, PluralCase.Other),
                ("is", 5.0, PluralCase.Other),

                // Russian
                ("ru", 1, PluralCase.One),
                ("ru", 2, PluralCase.Few),
                ("ru", 3, PluralCase.Few),
                ("ru", 5, PluralCase.Many),
                ("ru", 0, PluralCase.Many),
                ("ru", 0.1, PluralCase.Other),


            };

            (string, int , PluralCase )[] ordinalTests = new[] {
                // English
                ("en", 1, PluralCase.One),
                ("en", 2, PluralCase.Two),
                ("en", 3, PluralCase.Few),
                ("en", 4, PluralCase.Other),
                ("en", 11, PluralCase.Other),
                ("en", 21, PluralCase.One),

                // Welsh
                ("cy", 0, PluralCase.Zero),
                ("cy", 7, PluralCase.Zero),
                ("cy", 1, PluralCase.One),
                ("cy", 2, PluralCase.Two),
                ("cy", 3, PluralCase.Few),
                ("cy", 4, PluralCase.Few),
                ("cy", 5, PluralCase.Many),
                ("cy", 10, PluralCase.Other),
                
            };

            foreach (var test in cardinalTests) {
                Assert.Equal(test.Item3, CLDRPlurals.NumberPlurals.GetCardinalPluralCase(test.Item1, test.Item2));
            }

            foreach (var test in ordinalTests) {
                Assert.Equal(test.Item3, CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(test.Item1, test.Item2));
            }


        }

        // Test every file in Tests/TestCases
        [Theory]
        [MemberData(nameof(FileSources), "TestCases")]
        [MemberData(nameof(FileSources), "Issues")]
        public void TestSources(string file)
        {

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"INFO: Loading file {file}");

            storage.Clear();

            var scriptFilePath = Path.Combine(TestDataPath, file);

            CompilationJob compilationJob = CompilationJob.CreateFromFiles(scriptFilePath);
            compilationJob.Library = dialogue.Library;

            var testPlanFilePath = Path.ChangeExtension(scriptFilePath, ".testplan");

            bool testPlanExists = File.Exists(testPlanFilePath);

            if (testPlanExists == false) {
                // No test plan for this file exists, which indicates that
                // the file is not expected to compile. We'll actually make
                // it a test failure if it _does_ compile.

                var result = Compiler.Compile(compilationJob);
                Assert.NotEmpty(result.Diagnostics);
            }
            else
            {
                // Compile the job, and expect it to succeed.
                var result = Compiler.Compile(compilationJob);

                Assert.Empty(result.Diagnostics);

                Assert.NotNull(result.Program);
            
                LoadTestPlan(testPlanFilePath);

                dialogue.SetProgram(result.Program);
                stringTable = result.StringTable;

                // If this file contains a Start node, run the test case
                // (otherwise, we're just testing its parsability, which
                // we did in the last line)
                if (dialogue.NodeExists("Start"))
                {
                    RunStandardTestcase();
                }
            }
        }
    }
}

