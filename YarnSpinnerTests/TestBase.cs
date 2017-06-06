using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.Reflection;
using System.IO;
using System.Linq;

namespace YarnSpinner.Tests
{

    public class TestBase
    {

        string nextExpectedLine = null;
        int nextExpectedOptionCount = -1;
        int nextOptionToSelect = -1;
        string nextExpectedCommand = null;


        protected VariableStorage storage = new MemoryVariableStore();
        protected Dialogue dialogue;

        protected bool errorsCauseFailures = true;

        // Returns the path that contains the test case files.

        public static string ProjectRootPath {
            get {
                var path = Assembly.GetCallingAssembly().Location.Split(Path.DirectorySeparatorChar).ToList();

                var index = path.FindIndex(x => x == "YarnSpinnerTests");

                if (index == -1)
                {
                    Assert.Fail("Not in a test directory; cannot get test data directory");
                }

                var testDataDirectory = path.Take(index).ToList();

                var pathToTestData = string.Join(Path.DirectorySeparatorChar.ToString(), testDataDirectory.ToArray());

                pathToTestData = Path.DirectorySeparatorChar + pathToTestData;

                return pathToTestData;
            }
        }


        public static string TestDataPath
        {
            get
            {
                return Path.Combine(ProjectRootPath, "Tests");


            }
        }

        public static string UnityDemoScriptsPath
        {
            get
            {
                return Path.Combine(ProjectRootPath, "Unity/Assets/Yarn Spinner/Examples/Demo Assets/Space");
            }
        }


        [SetUp]
        public void Init()
        {

            dialogue = new Dialogue (storage);

            dialogue.LogDebugMessage = delegate(string message) {

                Console.WriteLine (message);

            };

            dialogue.LogErrorMessage = delegate(string message) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine ("ERROR: " + message);
                Console.ResetColor ();

                if (errorsCauseFailures == true)
                    Assert.Fail(message);
            };

            dialogue.library.RegisterFunction ("assert", -1, delegate(Yarn.Value[] parameters) {
                if (parameters[0].AsBool == false) {
                    if( parameters.Length > 1 && parameters[1].AsBool ) {
                        Assert.Fail ("Assertion failed: " + parameters[1].AsString);
                    } else {
                        Assert.Fail ("Assertion failed");
                    }
                }
            });

            dialogue.library.RegisterFunction ("prepare_for_options", 2, delegate(Value[] parameters) {
                nextExpectedOptionCount = (int)parameters[0].AsNumber;
                nextOptionToSelect = (int)parameters[1].AsNumber;
            });

            dialogue.library.RegisterFunction ("expect_line", -1, delegate(Value[] parameters) {
                nextExpectedLine = parameters[0].AsString;
            });

            dialogue.library.RegisterFunction ("expect_command", -1, delegate(Value[] parameters) {
                nextExpectedCommand = parameters[0].AsString;
            });

            nextExpectedOptionCount = -1;
            nextOptionToSelect = -1;
        }


        protected void RunStandardTestcase(string nodeName = "Start") {
            foreach (var result in dialogue.Run(nodeName)) {
                HandleResult (result);
            }
        }

        protected void HandleResult(Yarn.Dialogue.RunnerResult result) {

            if (result is Yarn.Dialogue.LineResult) {
                var text = (result as Yarn.Dialogue.LineResult).line.text;

                Console.WriteLine("Line: " + text);

                if (isExpectingLine) {
                    Assert.AreEqual (text, nextExpectedLine);
                }

            } else if (result is Yarn.Dialogue.OptionSetResult) {
                var options = (result as Yarn.Dialogue.OptionSetResult).options.options;
                var optionCount = options.Count;
                var resultDelegate = (result as Yarn.Dialogue.OptionSetResult).setSelectedOptionDelegate;

                Console.WriteLine("Options:");
                foreach (var option in options) {
                    Console.WriteLine(" - " + option);
                }

                if (nextExpectedOptionCount != -1) {
                    Assert.AreEqual (nextExpectedOptionCount, optionCount);
                }

                if (nextOptionToSelect != -1) {
                    resultDelegate (nextOptionToSelect);
                } else {
                    resultDelegate(0);
                }
            } else if (result is Yarn.Dialogue.CommandResult) {
                var commandText = (result as Yarn.Dialogue.CommandResult).command.text;

                Console.WriteLine("Command: " + commandText);

                if (nextExpectedCommand != null) {
                    Assert.AreEqual (nextExpectedCommand, commandText);
                }
            }

            // Reset all 'expected' stuff
            nextExpectedLine = null;
            nextExpectedCommand = null;
            nextExpectedOptionCount = -1;
            nextOptionToSelect = -1;

        }

        protected void ExpectLine(string line) {
            nextExpectedLine = line;
        }

        protected bool isExpectingLine {
            get {
                return nextExpectedLine != null;
            }
        }
    }
}

