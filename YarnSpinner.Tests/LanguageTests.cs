using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

using Yarn.Compiler;

namespace YarnSpinner.Tests
{


	public class LanguageTests : TestBase
    {
		public LanguageTests() : base() {

            // Register some additional functions
            dialogue.library.RegisterFunction("add_three_operands", 3, delegate (Value[] parameters) {
                return parameters[0] + parameters[1] + parameters[2];
            });

            dialogue.library.RegisterFunction("last_value", -1, delegate (Value[] parameters)
            {
                return parameters[parameters.Length - 1];
            });

			
		}

        [Fact]
        public void TestExampleScript()
        {

            errorsCauseFailures = false;
            var path = Path.Combine(TestDataPath, "Example.yarn");
            
            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram(program);
            RunStandardTestcase();
        }

        [Fact]
        public void TestMergingNodes()
        {
            var sallyPath = Path.Combine(UnityDemoScriptsPath, "Sally.yarn");
            var shipPath = Path.Combine(UnityDemoScriptsPath, "Ship.yarn");

            Compiler.CompileFile(sallyPath, out var sally, out var sallyStringTable);
            Compiler.CompileFile(shipPath, out var ship, out var shipStringTable);


            var combinedWorking = Program.Combine(sally, ship);
            
            // Loading code with the same contents should throw
            Assert.Throws<InvalidOperationException>(delegate ()
            {
                var combinedNotWorking = Program.Combine(sally, ship, ship);
            });
        }



        [Fact]
        public void TestEndOfNotesWithOptionsNotAdded()
        {
            var path = Path.Combine(TestDataPath, "SkippedOptions.yarn");
            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram(program);

            dialogue.optionsHandler = delegate (OptionSet optionSets) {
                Assert.False(true, "Options should not be shown to the user in this test.");
            };

            dialogue.SetNode();
            dialogue.Continue();

        }

        [Fact]
        public void TestNodeHeaders()
        {
            var path = Path.Combine(TestDataPath, "Headers.yarn");
            Compiler.CompileFile(path, out var program, out stringTable);

            Assert.Equal(3, program.Nodes.Count);

            foreach (var tag in new[] {"one", "two", "three"}) {
                Assert.Contains(tag, program.Nodes["Tags"].Tags);
            }
            
        }

        [Fact]
        public void TestInvalidCharactersInNodeTitle()
        {
            var path = Path.Combine(TestDataPath, "InvalidNodeTitle.yarn");

            Assert.Throws<Yarn.Compiler.ParseException>( () => {
                Compiler.CompileFile(path, out var program, out stringTable);
            });
            
        }

        // Test every file in Tests/TestCases
        [Theory, MemberData(nameof(FileSources))]
        public void TestSources(string file) {

            storage.Clear();
            bool runTest = true;

            var scriptFilePath = Path.Combine(TestDataPath, "TestCases", file);

            // skipping the indentation test when using the ANTLR parser
            // it can never pass
            if (file == "Indentation.yarn")
            {
                runTest = false;
            }

            if (runTest)
            {
                Compiler.CompileFile(scriptFilePath, out var program, out stringTable);
                dialogue.SetProgram(program);

                // If this file contains a Start node, run the test case
                // (otherwise, we're just testing its parsability, which
                // we did in the last line)
                if (dialogue.NodeExists("Start"))
                    RunStandardTestcase();
            }
        }

        // Returns the list of .node and.yarn files in the
        // Tests/TestCases directory.
        public static IEnumerable<object[]> FileSources() {

            var directory = "TestCases";

            var allowedExtensions = new[] { ".node", ".yarn" };

            var path = Path.Combine(TestDataPath, directory);


            var files = GetFilesInDirectory(path);

            return files.Where(p => allowedExtensions.Contains(Path.GetExtension(p)))
                        .Select(p => new[] {Path.GetFileName(p)});
        }

        // Returns the list of files in a directory. If that directory doesn't
        // exist, returns an empty list.
        static IEnumerable<string> GetFilesInDirectory(string path)
        {
            try
            {
                return Directory.EnumerateFiles(path);
            }
            catch (DirectoryNotFoundException)
            {
                return new string[] { };
            }
        }
    }

}

