using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.IO;
using System.Linq;

namespace YarnSpinner.Tests
{


	[TestFixture(true)]
	[TestFixture(false)]
    public class LanguageTests : TestBase
    {
		private bool experimentalMode;
        public LanguageTests(bool experimental)
		{
			this.experimentalMode = experimental;
		}

        [SetUp]
        public new void Init() {
            base.Init();

            dialogue.experimentalMode = this.experimentalMode;

            // Register some additional functions
            dialogue.library.RegisterFunction("add_three_operands", 3, delegate (Value[] parameters) {
                return parameters[0] + parameters[1] + parameters[2];
            });

            dialogue.library.RegisterFunction("last_value", -1, delegate (Value[] parameters)
            {
                return parameters[parameters.Length - 1];
            });


        }

        [Test]
        public void TestExampleScript()
        {

            errorsCauseFailures = false;
            var path = Path.Combine(TestDataPath, "Example.yarn.txt");
            dialogue.LoadFile(path);
            RunStandardTestcase();
        }

        [Test]
        public void TestMergingNodes()
        {
            var sallyPath = Path.Combine(UnityDemoScriptsPath, "Sally.yarn.txt");
            var examplePath = Path.Combine(TestDataPath, "Example.yarn.txt");

            dialogue.LoadFile(sallyPath);
            dialogue.LoadFile(examplePath);

            // Loading code with the same contents should throw
            Assert.Throws<InvalidOperationException>(delegate ()
            {
                var path = Path.Combine(TestDataPath, "Example.yarn.txt");
                dialogue.LoadFile(path);
                return;
            });
        }



        [Test]
        public void TestEndOfNotesWithOptionsNotAdded()
        {
            var path = Path.Combine(TestDataPath, "SkippedOptions.node");
            dialogue.LoadFile(path);

            foreach (var result in dialogue.Run())
            {
                Assert.IsNotInstanceOf<Dialogue.OptionSetResult>(result);
            }

        }

        // Test every file in Tests/TestCases
        [Test, TestCaseSource("FileSources")]
        public void TestSources(string file) {

            storage.Clear();
            bool runTest = true;

            var scriptFilePath = Path.Combine(TestDataPath, "TestCases", file);

            // skipping the indentation test when in experimental mode
            // it can never pass
            if (dialogue.experimentalMode && file == "Indentation.node")
            {
                runTest = false;
            }

            if (runTest)
            {
                dialogue.LoadFile(scriptFilePath);

                RunStandardTestcase();
            }
        }

        // Returns the list of node, json and yarn.txt files in the
        // Tests/TestCases directory.
        public static IEnumerable<string> FileSources() {

            var testCasesPath = Path.Combine(TestDataPath, "TestCases");

            var allowedExtensions = new[] { ".node", ".json", ".yarn.txt" };

            // taking only the filename to make the test case more readable in lists
            // - it gets re-added in TestSources

            return Directory
                .EnumerateFiles(testCasesPath)
                .Where(p => allowedExtensions.Contains(Path.GetExtension(p)))
                .Select(p => Path.GetFileName(p));
        }


    }

}

