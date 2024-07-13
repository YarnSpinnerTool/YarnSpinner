using System;
using System.Collections.Generic;
using Yarn;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Globalization;
using Yarn.Compiler;
using FluentAssertions;
using Xunit.Abstractions;
using Xunit.Sdk;
using Yarn.Saliency;

namespace YarnSpinner.Tests
{
    class DebugMemoryVariableStore : MemoryVariableStore {
        public override bool TryGetValue<T>(string variableName, out T result)
        {
            var fetched = base.TryGetValue(variableName, out result);
            if (fetched) {
                Console.WriteLine($"Get {typeof(T)} var {variableName}; no value found. Falling back to initial values.");
            } else {
                Console.WriteLine($"Get {typeof(T)} var {variableName}; got {result}");
            }
            return fetched;
        }
        
        public override void SetValue(string variableName, bool boolValue)
        {
            Console.WriteLine($"Set var {variableName} to {boolValue}");
            base.SetValue(variableName, boolValue);
        }

        public override void SetValue(string variableName, float floatValue)
        {
            Console.WriteLine($"Set var {variableName} to {floatValue}");
            base.SetValue(variableName, floatValue);
        }

        public override void SetValue(string variableName, string stringValue)
        {
            Console.WriteLine($"Set var {variableName} to {stringValue}");
            base.SetValue(variableName, stringValue);
        }
    }

    public class TestBase
    {
        protected readonly ITestOutputHelper output;
        protected IVariableStorage storage = new DebugMemoryVariableStore();
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

            stringTable.Should().ContainKey(line.ID);

            var stringInfo = stringTable[line.ID];

            if (stringInfo.text == null)
            {
                stringInfo.shadowLineID.Should().NotBeNull("a line that has null text is expected to be a shadow line (i.e. has a shadow line ID)");
                
                stringTable.Should().ContainKey(stringInfo.shadowLineID);

                var shadowLineText = stringTable[stringInfo.shadowLineID].text;

                shadowLineText.Should().NotBeNull("shadow line's source text should not be null");

                stringInfo.text = shadowLineText;
            }

            var substitutedText = Dialogue.ExpandSubstitutions(stringInfo.text, line.Substitutions);

            return dialogue.ParseMarkup(substitutedText, "en").Text;
        }
        
        public TestBase(ITestOutputHelper outputHelper)
        {
            this.output = outputHelper;
            
            dialogue = new Dialogue (storage);

            dialogue.ContentSaliencyStrategy = new Yarn.Saliency.BestLeastRecentlyViewedSalienceStrategy(storage);

            dialogue.LogDebugMessage = delegate(string message) {
                output.WriteLine(message);

                Console.ResetColor();
                Console.WriteLine (message);
            };

            dialogue.LogErrorMessage = delegate(string message) {
                output.WriteLine("ERROR: " + message);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine ("ERROR: " + message);
                Console.ResetColor ();

                if (runtimeErrorsCauseFailures == true) {
                    message.Should().NotBeNull();
                }
                    
            };

            dialogue.Library.RegisterFunction ("assert", delegate(bool value) {
                value.Should().BeTrue("assertion should pass");
                return true;
            });

            // When a node is complete, do nothing
            dialogue.NodeCompleteHandler = (string nodeName) => {};
        }

        /// <summary>
        /// Runs the result of a Yarn script compilation, checking its behaviour
        /// against a test plan.
        /// </summary>
        /// <param name="compilationResult">The compilation result to
        /// execute.</param>
        /// <param name="plan">The test plan to execute.</param>
        /// <param name="nodeName">The node name to start executing
        /// from.</param>
        /// <param name="config">A delegate that is called immediately before
        /// running the test, and can be used to configure the state of the <see
        /// cref="Dialogue"/> object that runs the script.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref
        /// name="plan"/> is <see langword="null"/>.</exception>
        protected void RunTestPlan(CompilationResult compilationResult, TestPlan plan, Action<Dialogue> config = null) {
            compilationResult.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

            dialogue.SetProgram(compilationResult.Program);
            stringTable = compilationResult.StringTable;
            testPlan = plan ?? throw new ArgumentNullException(nameof(plan));

            config?.Invoke(dialogue);

            RunStandardTestcase();
        }

        /// <summary>
        /// Executes the named node, and checks any assertions made during
        /// execution. Fails the test if an assertion made in Yarn fails.
        /// </summary>
        /// <param name="nodeName">The name of the node to start the test
        /// from. Defaults to "Start".</param>
        protected void RunStandardTestcase() {

            if (testPlan == null) {
                throw new Xunit.Sdk.XunitException("Cannot run test: no test plan provided.");
            }

            if (dialogue.Program == null) {
                throw new Xunit.Sdk.XunitException("Cannot run test: dialogue does not have a program.");
            }

            List<TestPlan.ExpectOptionStep> expectedOptions = new();

            var saliencyStrategies = new Dictionary<string, IContentSaliencyStrategy> {
                { "first", new FirstSaliencyStrategy() },
                { "best", new BestSaliencyStrategy() },
                { "best_least_recently_seen", new BestLeastRecentlyViewedSalienceStrategy(dialogue.VariableStorage)}
            };

            OptionsHandler GetNoOptionsExpectedHandler(TestPlan.Step step) => new OptionsHandler((opts) => throw new XunitException("Expected " + step.ToString() + ", not options"));

            LineHandler GetNoLineExpectedHandler(TestPlan.Step step) => new LineHandler((line) => throw new XunitException("Expected " + step.ToString() + ", not options"));

            CommandHandler GetNoCommandExpectedHandler(TestPlan.Step step) => new CommandHandler((opts) => throw new XunitException("Expected " + step.ToString() + ", not options"));

            DialogueCompleteHandler GetNoStopExpectedHandler(TestPlan.Step step) => new DialogueCompleteHandler(() => throw new XunitException("Expected " + step.ToString() + ", not options"));

            void ExpectLine(TestPlan.ExpectLineStep expectation)
            {
                dialogue.OptionsHandler = GetNoOptionsExpectedHandler(expectation);
                dialogue.CommandHandler = GetNoCommandExpectedHandler(expectation);
                dialogue.DialogueCompleteHandler = GetNoStopExpectedHandler(expectation);
                dialogue.LineHandler = (line) =>
                {
                    var id = line.ID;

                    stringTable.Keys.Should().Contain(id);

                    var lineNumber = stringTable[id].lineNumber;

                    var text = GetComposedTextForLine(line);

                    if (expectation.ExpectedText != null) {
                        text.Should().Be(expectation.ExpectedText);
                    }

                    if (expectation.ExpectedHashtags.Any()) {
                        stringTable[id].metadata.Should().Contain(expectation.ExpectedHashtags);
                    }

                    Console.WriteLine("Line: " + text);
                };
            }
            void ExpectOptionsSelection(TestPlan.ActionSelectStep expectation) {
                dialogue.CommandHandler = GetNoCommandExpectedHandler(expectation);
                dialogue.DialogueCompleteHandler = GetNoStopExpectedHandler(expectation);
                dialogue.LineHandler = GetNoLineExpectedHandler(expectation);
                dialogue.OptionsHandler = (opts) =>
                {
                    opts.Options.Should().HaveSameCount(expectedOptions);

                    foreach (var (Option, Expectation) in opts.Options.Zip(expectedOptions)) {
                        stringTable.Should().ContainKey(Option.Line.ID);

                        var text = GetComposedTextForLine(Option.Line);
                        if (Expectation.ExpectedText != null) {

                            text.Should().Be(Expectation.ExpectedText);
                        }
                        if (Expectation.ExpectedHashtags.Any()) {
                            stringTable[Option.Line.ID].metadata.Should().Contain(Expectation.ExpectedHashtags);
                        }
                        Option.IsAvailable.Should().Be(Expectation.ExpectedAvailability, $"option \"{text}\"'s availability was expected to be {Expectation.ExpectedAvailability}");
                    }
                    opts.Options.Should().ContainSingle(o => o.ID == expectation.SelectedIndex, "one option should have the ID that we want to select");
                };
            }
            void ExpectCommand(TestPlan.ExpectCommandStep expectation) {
                dialogue.DialogueCompleteHandler = GetNoStopExpectedHandler(expectation);
                dialogue.LineHandler = GetNoLineExpectedHandler(expectation);
                dialogue.OptionsHandler = GetNoOptionsExpectedHandler(expectation);
                dialogue.CommandHandler = (command) =>
                {
                    command.Text.Should().Be(expectation.ExpectedText);
                };
            }
            void ExpectStop(TestPlan.ExpectStop expectation) {
                dialogue.LineHandler = GetNoLineExpectedHandler(expectation);
                dialogue.OptionsHandler = GetNoOptionsExpectedHandler(expectation);
                dialogue.CommandHandler = GetNoCommandExpectedHandler(expectation);
                dialogue.DialogueCompleteHandler = () =>
                {

                };
            }

            
            foreach (var run in testPlan) {
                dialogue.SetNode(run.StartNode);

                foreach (var step in run) {
                    if (step is TestPlan.ExpectLineStep line) {
                        ExpectLine(line);
                        dialogue.Continue();
                    }
                    else if (step is TestPlan.ExpectOptionStep option) {
                        // Add this option to the list of options that we expect
                        expectedOptions.Add(option);
                    }
                    else if (step is TestPlan.ActionSelectStep select) {
                        ExpectOptionsSelection(select);
                        dialogue.Continue();
                        dialogue.SetSelectedOption(select.SelectedIndex);
                        expectedOptions.Clear();
                    }
                    else if (step is TestPlan.ExpectCommandStep command) {
                        ExpectCommand(command);
                        dialogue.Continue();
                    }
                    else if (step is TestPlan.ExpectStop stop) {
                        ExpectStop(stop);
                        dialogue.Continue();
                        break;
                    }
                    else if (step is TestPlan.ActionJumpToNodeStep jump) {
                        dialogue.SetNode(jump.NodeName);
                    }
                    else if (step is TestPlan.ActionSetVariableStep set) {
                        if (dialogue.Program.InitialValues.TryGetValue(set.VariableName, out var operand) == false)
                        {
                            throw new ArgumentException($"Variable {set.VariableName} is not valid in program");
                        }
                        switch (set.Value) {
                            case bool BoolValue:
                                dialogue.VariableStorage.SetValue(set.VariableName, BoolValue);
                                break;
                            case int IntValue:
                                dialogue.VariableStorage.SetValue(set.VariableName, IntValue);
                                break;
                        }
                    }
                    else if (step is TestPlan.ActionSetSaliencyStep setSaliency) {
                        if (saliencyStrategies.TryGetValue(setSaliency.SaliencyMode, out var saliencyStrategy)) {
                            dialogue.ContentSaliencyStrategy = saliencyStrategy;
                        } else {
                            throw new InvalidOperationException($"Unknown saliency strategy '{setSaliency.SaliencyMode}'");
                        }
                    }
                    else {
                        throw new InvalidOperationException("Unhandled step type " + step.GetType());
                    }
                }
            }
        }

        protected string CreateTestNode(string source, string name="Start") {
            return $"title: {name}\n---\n{source}\n===";
            
        }
        protected string CreateTestNode(string[] sourceLines, string name="Start") {
            return $"title: {name}\n---\n{string.Join("\n", sourceLines)}\n===";
        }

        /// <summary>
        /// Sets the current test plan to one loaded from a given path.
        /// </summary>
        /// <param name="path">The path of the file containing the test
        /// plan.</param>
        public void LoadTestPlan(string path) {
            this.testPlan = TestPlan.FromFile(path);
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

