﻿using Xunit;
using System;
using System.Collections;
using System.Collections.Generic;
using Yarn;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Globalization;
using Yarn.Compiler;

namespace YarnSpinner.Tests
{

    public class TestBase
    {
        protected IVariableStorage storage = new MemoryVariableStore();
        protected Dialogue dialogue;
        protected IDictionary<string, Yarn.Compiler.StringInfo> stringTable;
        protected IEnumerable<Yarn.Compiler.Declaration> declarations;

        public string locale = "en";
        
        protected bool runtimeErrorsCauseFailures = true;

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

        public static string SpaceDemoScriptsPath
        {
            get
            {
                return Path.Combine(ProjectRootPath, "Tests/Projects/Space");
            }
        }

        protected TestPlan testPlan;

        public string GetComposedTextForLine(Line line) {

            // var substitutedText = Dialogue.ExpandSubstitutions(stringTable[line.ID].text, line.Substitutions);
            var substitutedText = stringTable[line.ID].text;
            try
            {
                substitutedText = string.Format(substitutedText, line.RawSubstitutions);
            }
            catch
            {

            }

            return dialogue.ParseMarkup(substitutedText).Text;
        }
        
        public TestBase()
        {

            dialogue = new Dialogue (storage);

            dialogue.LanguageCode = "en";

            dialogue.LogDebugMessage = delegate(string message) {
                Console.ResetColor();
                Console.WriteLine (message);

            };

            dialogue.LogErrorMessage = delegate(string message) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine ("ERROR: " + message);
                Console.ResetColor ();

                if (runtimeErrorsCauseFailures == true) {
                    Assert.NotNull(message);
                }
                    
            };

            dialogue.LineHandler = delegate (Line line) {
                var id = line.ID;

                Assert.Contains(id, stringTable.Keys);

                var lineNumber = stringTable[id].lineNumber;

                var text = GetComposedTextForLine(line);

                Console.WriteLine("Line: " + text);

                if (testPlan != null) {
                    testPlan.Next();

                    if (testPlan.nextExpectedType == TestPlan.Step.Type.Line) {
                        Assert.Equal($"Line {lineNumber}: {testPlan.nextExpectedValue}", $"Line {lineNumber}: {text}");
                    } else {
                        throw new Xunit.Sdk.XunitException($"Received line {text}, but was expecting a {testPlan.nextExpectedType.ToString()}");
                    }
                }
            };

            dialogue.OptionsHandler = delegate (OptionSet optionSet) {
                var optionCount = optionSet.Options.Length;

                Console.WriteLine("Options:");
                foreach (var option in optionSet.Options) {
                    var optionText = GetComposedTextForLine(option.Line);
                    Console.WriteLine(" - " + optionText);
                }

                if (testPlan != null) {
                    testPlan.Next();

                    if (testPlan.nextExpectedType != TestPlan.Step.Type.Select) {
                        throw new Xunit.Sdk.XunitException($"Received {optionCount} options, but wasn't expecting them (was expecting {testPlan.nextExpectedType.ToString()})");
                    }

                    // Assert that the list of options we were given is
                    // identical to the list of options we expect
                    var actualOptionList = optionSet.Options
                        .Select(o => (GetComposedTextForLine(o.Line), o.IsAvailable))
                        .ToList();
                    Assert.Equal(testPlan.nextExpectedOptions, actualOptionList);

                    var expectedOptionCount = testPlan.nextExpectedOptions.Count();

                    Assert.Equal (expectedOptionCount, optionCount);
                    
                    if (testPlan.nextOptionToSelect != -1) {
                        dialogue.SetSelectedOption(testPlan.nextOptionToSelect - 1);                    
                    } else {
                        dialogue.SetSelectedOption(0);                    
                    }
                }

                
            };

            dialogue.CommandHandler = delegate (Command command) {
                Console.WriteLine("Command: " + command.Text);
                
                if (testPlan != null) {
                    testPlan.Next();
                    if (testPlan.nextExpectedType != TestPlan.Step.Type.Command)
                    {
                        throw new Xunit.Sdk.XunitException($"Received command {command.Text}, but wasn't expecting to select one (was expecting {testPlan.nextExpectedType.ToString()})");
                    }
                    else
                    {
                        // We don't need to get the composed string for a
                        // command because it's been done for us in the
                        // virtual machine. The VM can do this because
                        // commands are not localised, so we don't need to
                        // refer to the string table to get the text.
                        Assert.Equal(testPlan.nextExpectedValue, command.Text);
                    }
                }
            };

            dialogue.Library.RegisterFunction ("assert", delegate(Yarn.Value value) {
                if (value.ConvertTo<bool>() == false) {
                        Assert.NotNull ("Assertion failed");
                }
                return true;
            });

            
            // When a node is complete, do nothing
            dialogue.NodeCompleteHandler = (string nodeName) => {};

            // When dialogue is complete, check that we expected a stop
            dialogue.DialogueCompleteHandler = () => {
                if (testPlan != null) {
                    testPlan.Next();

                    if (testPlan.nextExpectedType != TestPlan.Step.Type.Stop) {
                        throw new Xunit.Sdk.XunitException($"Stopped dialogue, but wasn't expecting to select it (was expecting {testPlan.nextExpectedType.ToString()})");
                    }
                }
            };
        }

        /// <summary>
        /// Executes the named node, and checks any assertions made during
        /// execution. Fails the test if an assertion made in Yarn fails.
        /// </summary>
        /// <param name="nodeName">The name of the node to start the test
        /// from. Defaults to "Start".</param>
        protected void RunStandardTestcase(string nodeName = "Start") {

            if (testPlan == null) {
                throw new Xunit.Sdk.XunitException("Cannot run test: no test plan provided.");
            }

            dialogue.SetNode(nodeName);

            do {
                dialogue.Continue();
            } while (dialogue.IsActive);
            
        }

        protected string CreateTestNode(string source, string name="Start") {
            return $"title: {name}\n---\n{source}\n===";
            
        }

        /// <summary>
        /// Sets the current test plan to one loaded from a given path.
        /// </summary>
        /// <param name="path">The path of the file containing the test
        /// plan.</param>
        public void LoadTestPlan(string path) {
            this.testPlan = new TestPlan(path);
        }

        // Returns the list of .node and.yarn files in the
        // Tests/<directory> directory.
        public static IEnumerable<object[]> FileSources(string directoryComponents) {

            var allowedExtensions = new[] { ".node", ".yarn" };

            var directory = Path.Combine(directoryComponents.Split('/'));

            var path = Path.Combine(TestDataPath, directory);

            var files = GetFilesInDirectory(path);

            return files.Where(p => allowedExtensions.Contains(Path.GetExtension(p)))
                        .Where(p => p.EndsWith(".upgraded.yarn") == false) // don't include ".upgraded.yarn" (used in UpgraderTests)
                        .Select(p => new[] {Path.Combine(directory, Path.GetFileName(p))});
        }

        public static IEnumerable<object[]> DirectorySources(string directoryComponents) {
            var directory = Path.Combine(directoryComponents.Split('/'));

            var path = Path.Combine(TestDataPath, directory);

            try {
                return Directory.GetDirectories(path)
                    .Select(d => d.Replace(TestDataPath + Path.DirectorySeparatorChar, ""))
                    .Select(d => new[] {d});
            } catch (DirectoryNotFoundException) {
                return new string[] { }.Select(d => new[] {d});
            }
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

        /// <summary>
        /// Given a parse tree, returns a string containing a textual
        /// representation of that tree.
        /// </summary>
        /// <param name="tree">The parse tree to evaluate.</param>
        /// <param name="indentPrefix">A string to use for each indent level of
        /// the parse tree.</param>
        /// <returns>The text version of <paramref name="tree"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref
        /// name="tree"/> contains a parse node that isn't a token or a parse
        /// rule.</exception>
        protected static string FormatParseTreeAsText(Antlr4.Runtime.Tree.IParseTree tree, string indentPrefix = "| ")
        {
            var stack = new Stack<(int Indent, Antlr4.Runtime.Tree.IParseTree Node)>();

            stack.Push((0, tree));

            var sb = new System.Text.StringBuilder();

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                sb.Append(string.Concat(Enumerable.Repeat(indentPrefix, current.Indent)));

                string item;

                switch (current.Node.Payload)
                {
                    case Antlr4.Runtime.IToken token:
                        {
                            // Display this token's name and text. Tokens have
                            // no children, so there's nothing else to do here.
                            var tokenName = YarnSpinnerLexer.DefaultVocabulary.GetSymbolicName(token.Type);
                            var tokenText = token.Text.Replace("\n", "\\n");
                            item = $"{token.Line}:{token.Column} {tokenName} \"{tokenText}\"";
                            break;
                        }

                    case Antlr4.Runtime.ParserRuleContext ruleContext:
                        {
                            // Display this rule's name (not its text, because
                            // that's comprised of all of the child tokens.)
                            var ruleName = YarnSpinnerParser.ruleNames[ruleContext.RuleIndex];
                            var start = ruleContext.Start;
                            item = $"{start.Line}:{start.Column} {ruleName}";

                            // Push all children into our stack; do this in
                            // reverse order of child, so that we encounter them
                            // in a reasonable order (i.e. child 0 will be the
                            // next item we see)
                            for (int i = ruleContext.ChildCount - 1; i >= 0; i--)
                            {
                                var child = ruleContext.GetChild(i);
                                stack.Push((current.Indent + 1, child));
                            }

                            break;
                        }

                    default:
                        throw new InvalidOperationException($"Unexpected parse node type {current.Node.GetType()}");
                }

                sb.AppendLine(item);
            }

            var result = sb.ToString();
            return result;
        }
    }
}

