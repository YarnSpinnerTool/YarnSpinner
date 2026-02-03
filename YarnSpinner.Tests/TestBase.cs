using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;
using Yarn;
using Yarn.Compiler;
using Yarn.Saliency;

namespace YarnSpinner.Tests
{
    public class AsyncTestBase: TestBase
    {
        protected new AsyncDialogue dialogue;

        private async System.Threading.Tasks.Task EvaluateCurrentRun(TestPlan.Run currentRun)
        {
            if (dialogue.Program == null)
            {
                throw new Xunit.Sdk.XunitException("Cannot run test: dialogue does not have a program.");
            }

            var saliencyStrategies = new Dictionary<string, IContentSaliencyStrategy> {
                { "first", new FirstSaliencyStrategy() },
                { "best", new BestSaliencyStrategy() },
                { "best_least_recently_seen", new BestLeastRecentlyViewedSaliencyStrategy(dialogue.VariableStorage)}
            };

            var selections = new Queue<int>();
            foreach (var step in currentRun.Steps)
            {
                if (step is not TestPlan.ActionStep)
                {
                    continue;
                }

                // this is a selection
                // we store it for later use
                if (step is TestPlan.ActionSelectStep)
                {
                    var selection = step as TestPlan.ActionSelectStep;
                    selections.Enqueue(selection.SelectedIndex);
                }
                else if (step is TestPlan.ActionSetVariableStep set)
                {
                    if (dialogue.Program.InitialValues.TryGetValue(set.VariableName, out var operand) == false)
                    {
                        throw new ArgumentException($"Variable {set.VariableName} is not valid in program");
                    }
                    switch (set.Value)
                    {
                        case bool BoolValue:
                            dialogue.VariableStorage.SetValue(set.VariableName, BoolValue);
                            break;
                        case int IntValue:
                            dialogue.VariableStorage.SetValue(set.VariableName, IntValue);
                            break;
                    }
                }
                else if (step is TestPlan.ActionSetSaliencyStep setSaliency)
                {
                    if (saliencyStrategies.TryGetValue(setSaliency.SaliencyMode, out var saliencyStrategy))
                    {
                        dialogue.ContentSaliencyStrategy = saliencyStrategy;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Unknown saliency strategy '{setSaliency.SaliencyMode}'");
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unhandled step type :{step.GetType()}");
                }
            }

            var moments = new Queue<TestPlan.Step>();
            // set up the handlers
            AsyncDialogue.ReceivedLineHandle LineHandler = (line, token) =>
            {
                // getting the text of the line
                var text = GetComposedTextForLine(line);
                // getting it's metadata
                var metadata = stringTable[line.ID].metadata;
                // building the step and storing it for later use
                var step = new TestPlan.ExpectLineStep(text, metadata);

                moments.Enqueue(step);
                return default;
            };
            AsyncDialogue.ReceivedCommandHandle CommandHandler = (command, token) =>
            {
                // building the step and storing it for later use
                var step = new TestPlan.ExpectCommandStep(command.Text);
                moments.Enqueue(step);

                return default;
            };
            AsyncDialogue.ReceivedOptionsHandle OptionsHandler = (options, token) =>
            {
                foreach (var option in options.Options)
                {
                    // getting the text of the option
                    var text = GetComposedTextForLine(option.Line);
                    // getting it's metadata
                    var metadata = stringTable[option.Line.ID].metadata;
                    
                    // building the step and storing it for later use
                    var step = new TestPlan.ExpectOptionStep(text, metadata, option.IsAvailable);
                    moments.Enqueue(step);
                }

                var selection = selections.Dequeue();
                return new System.Threading.Tasks.ValueTask<int>(selection);
            };

            dialogue.OnReceivedLine = LineHandler;
            dialogue.OnReceivedCommand = CommandHandler;
            dialogue.OnReceivedOptions = OptionsHandler;

            // kick off the dialogue and await it finishing
            await dialogue.StartDialogue(currentRun.StartNode);

            // now we go through every moment and see if matches the test plan
            foreach (var step in currentRun.Steps)
            {
                // if the step is a set, select, or set saliency we ignore it
                // these will have been handled at runtime
                if (step is TestPlan.ActionStep)
                {
                    continue;
                }

                if (step is TestPlan.ExpectStop)
                {
                    // we early out
                    // if there are unprocessed moments this will get caught a bit later when we check the moments queue is empty
                    break;
                }

                // ok we are now a content step of some sort
                // so we want to dequeue it
                // ensure it's the same
                if (step is TestPlan.ExpectLineStep expectedLine)
                {
                    moments.Peek().Should().BeOfType<TestPlan.ExpectLineStep>();
                    var yarnStep = moments.Dequeue() as TestPlan.ExpectLineStep;
                    yarnStep.Should().NotBeNull();

                    yarnStep.ExpectedText.Should().Be(expectedLine.ExpectedText);

                    // checking we have all the expected hashtags
                    // we may (and often will have more because they can be synthesised like the lineID tag)
                    if (expectedLine.ExpectedHashtags.Count > 0)
                    {
                        foreach (var expectedTag in expectedLine.ExpectedHashtags)
                        {
                            yarnStep.ExpectedHashtags.Should().Contain(expectedTag);
                        }
                    }
                }
                else if (step is TestPlan.ExpectOptionStep expectedOption)
                {
                    moments.Peek().Should().BeOfType<TestPlan.ExpectOptionStep>();
                    var yarnStep = moments.Dequeue() as TestPlan.ExpectOptionStep;
                    yarnStep.Should().NotBeNull();

                    yarnStep.ExpectedText.Should().Be(expectedOption.ExpectedText);
                    yarnStep.ExpectedAvailability.Should().Be(expectedOption.ExpectedAvailability);

                    if (expectedOption.ExpectedHashtags.Count > 0)
                    {
                        foreach (var expectedTag in expectedOption.ExpectedHashtags)
                        {
                            yarnStep.ExpectedHashtags.Should().Contain(expectedTag);
                        }
                    }
                }
                else if (step is TestPlan.ExpectCommandStep expectedCommand)
                {
                    moments.Peek().Should().BeOfType<TestPlan.ExpectCommandStep>();
                    var yarnStep = moments.Dequeue() as TestPlan.ExpectCommandStep;
                    yarnStep.Should().NotBeNull();

                    yarnStep.ExpectedText.Should().Be(expectedCommand.ExpectedText);
                }
                else
                {
                    throw new Xunit.Sdk.XunitException($"Encountered an unknown step: {step.GetType()}");
                }
            }

            // ensure the current step is now the "we are done" step
            moments.Should().BeEmpty();
        }

        protected new async Task RunTestPlan(CompilationResult compilationResult, TestPlan plan, Action<Dialogue> config = null)
        {
            compilationResult.Diagnostics.Should().NotContain(d => d.Severity == Diagnostic.DiagnosticSeverity.Error);

            dialogue.Program = compilationResult.Program;
            stringTable = compilationResult.StringTable;
            testPlan = plan ?? throw new ArgumentNullException(nameof(plan));

            await RunStandardTestcase();
        }

        protected new async System.Threading.Tasks.Task RunStandardTestcase()
        {
            if (testPlan == null)
            {
                throw new Xunit.Sdk.XunitException("Cannot run test: no test plan provided.");
            }

            if (dialogue.Program == null)
            {
                throw new Xunit.Sdk.XunitException("Cannot run test: dialogue does not have a program.");
            }

            dialogue.OnReceivedNodeStart = (_, _) => { return default; };
            dialogue.OnReceivedNodeComplete = (_, _) => { return default; };
            dialogue.OnReceivedDialogueComplete = () => { return default; };

            foreach (var run in testPlan.Runs)
            {
                await EvaluateCurrentRun(run);
            }
        }

        public AsyncTestBase(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            dialogue = new AsyncDialogue(storage);

            dialogue.ContentSaliencyStrategy = new Yarn.Saliency.BestLeastRecentlyViewedSaliencyStrategy(storage);

            dialogue.LogDebugMessage = delegate (string message)
            {
                output.WriteLine(message);

                Console.ResetColor();
                Console.WriteLine(message);
            };

            dialogue.LogErrorMessage = delegate (string message)
            {
                output.WriteLine("ERROR: " + message);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: " + message);
                Console.ResetColor();

                if (runtimeErrorsCauseFailures == true)
                {
                    message.Should().NotBeNull();
                }

            };

            dialogue.Library.RegisterFunction("assert", delegate (bool value)
            {
                value.Should().BeTrue("assertion should pass");
                return true;
            });
        }
    }

    class DebugMemoryVariableStore : MemoryVariableStore
    {
        public override bool TryGetValue<T>(string variableName, out T result)
        {
            var fetched = base.TryGetValue(variableName, out result);
            if (!fetched)
            {
                Console.WriteLine($"Get {typeof(T)} var {variableName}; no value found. Falling back to initial values.");
            }
            else
            {
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

        public static string ProjectRootPath
        {
            get
            {
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

        public string GetComposedTextForLine(Line line)
        {
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

            var lineParser = new Yarn.Markup.LineParser();
            var builtInReplacer = new Yarn.Markup.BuiltInMarkupReplacer();
            lineParser.RegisterMarkerProcessor("select", builtInReplacer);
            lineParser.RegisterMarkerProcessor("ordinal", builtInReplacer);
            lineParser.RegisterMarkerProcessor("plural", builtInReplacer);

            var substitutedText = Yarn.Markup.LineParser.ExpandSubstitutions(stringInfo.text, line.Substitutions);

            return lineParser.ParseString(substitutedText, "en").Text;
        }

        public TestBase(ITestOutputHelper outputHelper)
        {
            this.output = outputHelper;

            dialogue = new Dialogue(storage);

            dialogue.ContentSaliencyStrategy = new Yarn.Saliency.BestLeastRecentlyViewedSaliencyStrategy(storage);

            dialogue.LogDebugMessage = delegate (string message)
            {
                output.WriteLine(message);

                Console.ResetColor();
                Console.WriteLine(message);
            };

            dialogue.LogErrorMessage = delegate (string message)
            {
                output.WriteLine("ERROR: " + message);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: " + message);
                Console.ResetColor();

                if (runtimeErrorsCauseFailures == true)
                {
                    message.Should().NotBeNull();
                }

            };

            dialogue.Library.RegisterFunction("assert", delegate (bool value)
            {
                value.Should().BeTrue("assertion should pass");
                return true;
            });

            // When a node is complete, do nothing
            dialogue.NodeCompleteHandler = (string nodeName) => { };
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
        protected void RunTestPlan(CompilationResult compilationResult, TestPlan plan, Action<Dialogue> config = null)
        {
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
        protected void RunStandardTestcase()
        {

            if (testPlan == null)
            {
                throw new Xunit.Sdk.XunitException("Cannot run test: no test plan provided.");
            }

            if (dialogue.Program == null)
            {
                throw new Xunit.Sdk.XunitException("Cannot run test: dialogue does not have a program.");
            }

            List<TestPlan.ExpectOptionStep> expectedOptions = new();

            var saliencyStrategies = new Dictionary<string, IContentSaliencyStrategy> {
                { "first", new FirstSaliencyStrategy() },
                { "best", new BestSaliencyStrategy() },
                { "best_least_recently_seen", new BestLeastRecentlyViewedSaliencyStrategy(dialogue.VariableStorage)}
            };

            OptionsHandler GetNoOptionsExpectedHandler(TestPlan.Step step) => new OptionsHandler((opts) => throw new XunitException($"Expected {step}, not options"));

            LineHandler GetNoLineExpectedHandler(TestPlan.Step step) => new LineHandler((line) => throw new XunitException($"Expected {step}, not line \"{GetComposedTextForLine(line)}\""));

            CommandHandler GetNoCommandExpectedHandler(TestPlan.Step step) => new CommandHandler((cmd) => throw new XunitException($"Expected {step}, not command \"{cmd}\""));

            DialogueCompleteHandler GetNoStopExpectedHandler(TestPlan.Step step) => new DialogueCompleteHandler(() => throw new XunitException($"Expected {step}, not stop"));

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

                    if (expectation.ExpectedText != null)
                    {
                        text.Should().Be(expectation.ExpectedText);
                    }

                    if (expectation.ExpectedHashtags.Any())
                    {
                        stringTable[id].metadata.Should().Contain(expectation.ExpectedHashtags);
                    }

                    Console.WriteLine("Line: " + text);
                };
            }
            void ExpectOptionsSelection(TestPlan.ActionSelectStep expectation)
            {
                dialogue.CommandHandler = GetNoCommandExpectedHandler(expectation);
                dialogue.DialogueCompleteHandler = GetNoStopExpectedHandler(expectation);
                dialogue.LineHandler = GetNoLineExpectedHandler(expectation);
                dialogue.OptionsHandler = (opts) =>
                {
                    opts.Options.Should().HaveSameCount(expectedOptions);

                    bool isAnyAvailable = false;
                    foreach (var (Option, Expectation) in opts.Options.Zip(expectedOptions))
                    {
                        stringTable.Should().ContainKey(Option.Line.ID);

                        var text = GetComposedTextForLine(Option.Line);
                        if (Expectation.ExpectedText != null)
                        {
                            text.Should().Be(Expectation.ExpectedText);
                        }
                        if (Expectation.ExpectedHashtags.Any())
                        {
                            stringTable[Option.Line.ID].metadata.Should().Contain(Expectation.ExpectedHashtags);
                        }
                        Option.IsAvailable.Should().Be(Expectation.ExpectedAvailability, $"option \"{text}\"'s availability was expected to be {Expectation.ExpectedAvailability}");
                        if (Option.IsAvailable)
                        {
                            isAnyAvailable = true;
                        }
                    }
                    if (isAnyAvailable)
                    {
                        opts.Options.Should().ContainSingle(o => o.ID == expectation.SelectedIndex, "one option should have the ID that we want to select");
                    }
                    else
                    {
                        expectation.SelectedIndex.Should().Be(-1);
                    }
                };
            }
            void ExpectCommand(TestPlan.ExpectCommandStep expectation)
            {
                dialogue.DialogueCompleteHandler = GetNoStopExpectedHandler(expectation);
                dialogue.LineHandler = GetNoLineExpectedHandler(expectation);
                dialogue.OptionsHandler = GetNoOptionsExpectedHandler(expectation);
                dialogue.CommandHandler = (command) =>
                {
                    command.Text.Should().Be(expectation.ExpectedText);
                };
            }
            void ExpectStop(TestPlan.ExpectStop expectation)
            {
                dialogue.LineHandler = GetNoLineExpectedHandler(expectation);
                dialogue.OptionsHandler = GetNoOptionsExpectedHandler(expectation);
                dialogue.CommandHandler = GetNoCommandExpectedHandler(expectation);
                dialogue.DialogueCompleteHandler = () =>
                {

                };
            }


            foreach (var run in testPlan)
            {
                dialogue.SetNode(run.StartNode);

                foreach (var step in run)
                {
                    if (step is TestPlan.ExpectLineStep line)
                    {
                        ExpectLine(line);
                        dialogue.Continue();
                    }
                    else if (step is TestPlan.ExpectOptionStep option)
                    {
                        // Add this option to the list of options that we expect
                        expectedOptions.Add(option);
                    }
                    else if (step is TestPlan.ActionSelectStep select)
                    {
                        ExpectOptionsSelection(select);
                        dialogue.Continue();
                        dialogue.SetSelectedOption(select.SelectedIndex);
                        expectedOptions.Clear();
                    }
                    else if (step is TestPlan.ExpectCommandStep command)
                    {
                        ExpectCommand(command);
                        dialogue.Continue();
                    }
                    else if (step is TestPlan.ExpectStop stop)
                    {
                        ExpectStop(stop);
                        dialogue.Continue();
                        break;
                    }
                    else if (step is TestPlan.ActionJumpToNodeStep jump)
                    {
                        dialogue.SetNode(jump.NodeName);
                    }
                    else if (step is TestPlan.ActionSetVariableStep set)
                    {
                        if (dialogue.Program.InitialValues.TryGetValue(set.VariableName, out var operand) == false)
                        {
                            throw new ArgumentException($"Variable {set.VariableName} is not valid in program");
                        }
                        switch (set.Value)
                        {
                            case bool BoolValue:
                                dialogue.VariableStorage.SetValue(set.VariableName, BoolValue);
                                break;
                            case int IntValue:
                                dialogue.VariableStorage.SetValue(set.VariableName, IntValue);
                                break;
                        }
                    }
                    else if (step is TestPlan.ActionSetSaliencyStep setSaliency)
                    {
                        if (saliencyStrategies.TryGetValue(setSaliency.SaliencyMode, out var saliencyStrategy))
                        {
                            dialogue.ContentSaliencyStrategy = saliencyStrategy;
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unknown saliency strategy '{setSaliency.SaliencyMode}'");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("Unhandled step type " + step.GetType());
                    }
                }
            }
        }

        protected string CreateTestNode(string source, string name = "Start")
        {
            return $"title: {name}\n---\n{source}\n===";

        }
        protected string CreateTestNode(string[] sourceLines, string name = "Start")
        {
            return $"title: {name}\n---\n{string.Join("\n", sourceLines)}\n===";
        }

        /// <summary>
        /// Sets the current test plan to one loaded from a given path.
        /// </summary>
        /// <param name="path">The path of the file containing the test
        /// plan.</param>
        public void LoadTestPlan(string path)
        {
            this.testPlan = TestPlan.FromFile(path);
        }

        // Returns the list of .node and.yarn files in the
        // Tests/<directory> directory.
        public static IEnumerable<object[]> FileSources(string directoryComponents)
        {

            var allowedExtensions = new[] { ".node", ".yarn" };

            var directory = Path.Combine(directoryComponents.Split('/'));

            var path = Path.Combine(TestDataPath, directory);

            var files = GetFilesInDirectory(path);

            return files.Where(p => allowedExtensions.Contains(Path.GetExtension(p)))
                        .Where(p => p.EndsWith(".upgraded.yarn") == false) // don't include ".upgraded.yarn" (used in UpgraderTests)
                        .Select(p => new[] { Path.Combine(directory, Path.GetFileName(p)) });
        }

        // Returns the list of .node and.yarn files in the Tests/<directory>
        // directory that have a corresponding .testplan file, indicating that
        // it is expected to compile without errors.
        public static IEnumerable<object[]> ValidFileSources(string directoryComponents)
        {

            var allowedExtensions = new[] { ".node", ".yarn" };

            var directory = Path.Combine(directoryComponents.Split('/'));

            var path = Path.Combine(TestDataPath, directory);

            var files = GetFilesInDirectory(path);

            return files.Where(p => allowedExtensions.Contains(Path.GetExtension(p)))
                        .Where(p => p.EndsWith(".upgraded.yarn") == false) // don't include ".upgraded.yarn" (used in UpgraderTests)
                        .Where(p => File.Exists(p.Replace(".yarn", ".testplan"))) // only include file that have a testplan (i.e. are expected to compile)
                        .Select(p => new[] { Path.Combine(directory, Path.GetFileName(p)) });
        }

        public static IEnumerable<object[]> DirectorySources(string directoryComponents)
        {
            var directory = Path.Combine(directoryComponents.Split('/'));

            var path = Path.Combine(TestDataPath, directory);

            try
            {
                return Directory.GetDirectories(path)
                    .Select(d => d.Replace(TestDataPath + Path.DirectorySeparatorChar, ""))
                    .Select(d => new[] { d });
            }
            catch (DirectoryNotFoundException)
            {
                return new string[] { }.Select(d => new[] { d });
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

