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
            var testPath = Path.ChangeExtension(path, ".testplan");
            
            Compiler.CompileFile(path, out var program, out stringTable);

            dialogue.SetProgram(program);
            this.LoadTestPlan(testPath);

            RunStandardTestcase();
        }

        [Fact]
        public void TestMergingNodes()
        {
            var sallyPath = Path.Combine(SpaceDemoScriptsPath, "Sally.yarn");
            var shipPath = Path.Combine(SpaceDemoScriptsPath, "Ship.yarn");

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

            Assert.Equal(4, program.Nodes.Count);

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
        public void TestSources(string file) {

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine ($"INFO: Loading file {file}");

            storage.Clear();
            bool runTest = true;

            var scriptFilePath = Path.Combine(TestDataPath, file);
            var testPlanFilePath = Path.ChangeExtension(scriptFilePath, ".testplan");
            
            // skipping the indentation test when using the ANTLR parser
            // it can never pass
            if (file == "TestCases/Indentation.yarn")
            {
                runTest = false;
            }

            if (runTest)
            {
                LoadTestPlan(testPlanFilePath);

                Compiler.CompileFile(scriptFilePath, out var program, out stringTable);
                dialogue.SetProgram(program);

                // If this file contains a Start node, run the test case
                // (otherwise, we're just testing its parsability, which
                // we did in the last line)
                if (dialogue.NodeExists("Start"))
                    RunStandardTestcase();
            }
        }
    }

}

