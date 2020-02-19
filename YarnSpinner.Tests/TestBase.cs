using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Globalization;

namespace YarnSpinner.Tests
{

    public class TestBase
    {

        

        protected VariableStorage storage = new MemoryVariableStore();
        protected Dialogue dialogue;
        protected IDictionary<string, Yarn.StringInfo> stringTable;
        
        protected bool errorsCauseFailures = true;

        // Returns the path that contains the test case files.

        public static string ProjectRootPath {
            get {
                var path = Assembly.GetCallingAssembly().Location.Split(Path.DirectorySeparatorChar).ToList();

                var index = path.FindIndex(x => x == "YarnSpinner.Tests");

                if (index == -1)
                {
                    throw new System.IO.DirectoryNotFoundException("Cannot find test data directory");                    
                }

                var testDataDirectory = path.Take(index).ToList();

                var pathToTestData = string.Join(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), testDataDirectory.ToArray());

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
                return Path.Combine(ProjectRootPath, "Unity/Assets/YarnSpinner/Examples/Space/Dialogue");
            }
        }

        private TestPlan testPlan;
        
        public TestBase()
        {

            dialogue = new Dialogue (storage);

            dialogue.LogDebugMessage = delegate(string message) {
                Console.ResetColor();
                Console.WriteLine (message);

            };

            dialogue.LogErrorMessage = delegate(string message) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine ("ERROR: " + message);
                Console.ResetColor ();

                if (errorsCauseFailures == true) {
                    Assert.NotNull(message);
                }
                    
            };

            dialogue.lineHandler = delegate (Line line) {
                var id = line.ID;

                Assert.Contains(id, stringTable.Keys);

                var text = stringTable[id].text;

                Console.WriteLine("Line: " + text);

                if (testPlan != null) {
                    testPlan.Next();

                    if (testPlan.nextExpectedType == TestPlan.Step.Type.Line) {
                        Assert.Equal(testPlan.nextExpectedValue, text);
                    } else {
                        throw new Xunit.Sdk.XunitException($"Received line {text}, but was expecting a {testPlan.nextExpectedType.ToString()}");
                    }
                }
                            
                return Dialogue.HandlerExecutionType.ContinueExecution;
            };

            dialogue.optionsHandler = delegate (OptionSet optionSet) {
                var optionCount = optionSet.Options.Length;

                Console.WriteLine("Options:");
                foreach (var option in optionSet.Options) {
                    var optionText = stringTable[option.Line.ID].text;
                    Console.WriteLine(" - " + optionText);
                }

                if (testPlan != null) {
                    testPlan.Next();

                    if (testPlan.nextExpectedType != TestPlan.Step.Type.Select) {
                        throw new Xunit.Sdk.XunitException($"Received {optionCount} options, but wasn't expecting them (was expecting {testPlan.nextExpectedType.ToString()})");
                    }

                    var expectedOptionCount = testPlan.nextExpectedOptions.Count();

                    Assert.Equal (expectedOptionCount, optionCount);
                    
                    if (testPlan.nextOptionToSelect != -1) {
                        dialogue.SetSelectedOption(testPlan.nextOptionToSelect - 1);                    
                    } else {
                        dialogue.SetSelectedOption(0);                    
                    }
                }

                
            };

            dialogue.commandHandler = delegate (Command command) {
                Console.WriteLine("Command: " + command.Text);
                
                if (testPlan != null) {
                    testPlan.Next();
                    if (testPlan.nextExpectedType != TestPlan.Step.Type.Command)
                    {
                        throw new Xunit.Sdk.XunitException($"Received command {command.Text}, but wasn't expecting to select one (was expecting {testPlan.nextExpectedType.ToString()})");
                    }
                    else
                    {
                        Assert.Equal(testPlan.nextExpectedValue, command.Text);
                    }
                }
                
                return Dialogue.HandlerExecutionType.ContinueExecution;
            };

            dialogue.library.RegisterFunction ("assert", 1, delegate(Yarn.Value[] parameters) {
                if (parameters[0].AsBool == false) {
                        Assert.NotNull ("Assertion failed");
                }
            });

            
            // When a node is complete, just indicate that we want to continue execution
            dialogue.nodeCompleteHandler = (string nodeName) => Dialogue.HandlerExecutionType.ContinueExecution;

            // When dialogue is complete, check that we expected a stop
            dialogue.dialogueCompleteHandler = () => {
                if (testPlan != null) {
                    testPlan.Next();

                    if (testPlan.nextExpectedType != TestPlan.Step.Type.Stop) {
                        throw new Xunit.Sdk.XunitException($"Stopped dialogue, but wasn't expecting to select it (was expecting {testPlan.nextExpectedType.ToString()})");
                    }
                }
            };

        }

        // Executes the named node, and checks any assertions made during
        // execution. Fails the test if an assertion made in Yarn fails.
        protected void RunStandardTestcase(string nodeName = "Start") {

            if (testPlan == null) {
                throw new Xunit.Sdk.XunitException("Cannot run test: no test plan provided.");
            }

            dialogue.SetNode(nodeName);

            do {
                dialogue.Continue();
            } while (dialogue.IsActive);
            
        }

        protected string CreateTestNode(string source) {
            return $"title: Start\n---\n{source}\n===";
            
        }

        public void LoadTestPlan(string path) {
            this.testPlan = new TestPlan(path);
        }
    }
}

