using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;
using Yarn;
using Yarn.Compiler;
using Yarn.Saliency;

namespace YarnSpinner.Tests
{
    public class AsyncTestBase
    {
        protected AsyncDialogue dialogue;

        protected TestBaseResponder testBaseResponder;

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
                else if (step is TestPlan.ActionJumpToNodeStep jumpStep)
                {
                    currentRun.StartNode = jumpStep.NodeName;
                }
                else
                {
                    throw new InvalidOperationException($"Unhandled step type :{step.GetType()}");
                }
            }

            var moments = new Queue<TestPlan.Step>();
            // set up the handlers
            TestBaseResponder.ReceivedLineHandle LineHandler = (line, token) =>
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
            TestBaseResponder.ReceivedCommandHandle CommandHandler = (command, token) =>
            {
                // building the step and storing it for later use
                var step = new TestPlan.ExpectCommandStep(command.Text);
                moments.Enqueue(step);

                return default;
            };
            TestBaseResponder.ReceivedOptionsHandle OptionsHandler = (options, token) =>
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

                selections.Should().NotBeEmpty();
                var selection = selections.Dequeue();
                return new System.Threading.Tasks.ValueTask<int>(selection);
            };

            testBaseResponder.OnReceivedLine = LineHandler;
            testBaseResponder.OnReceivedCommand = CommandHandler;
            testBaseResponder.OnReceivedOptions = OptionsHandler;

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

        protected new async Task RunTestPlan(CompilationResult compilationResult, TestPlan plan)
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

            testBaseResponder.OnReceivedNodeStart = (_, _) => { return default; };
            testBaseResponder.OnReceivedNodeComplete = (_, _) => { return default; };
            testBaseResponder.OnReceivedDialogueComplete = () => { return default; };

            foreach (var run in testPlan.Runs)
            {
                await EvaluateCurrentRun(run);
            }
        }

        public AsyncTestBase(ITestOutputHelper outputHelper)
        {
            dialogue = new AsyncDialogue(storage);

            testBaseResponder = new TestBaseResponder();
            testBaseResponder.Library.RegisterFunction("visited", delegate (string node)
            {
                return dialogue.IsNodeVisited(node);
            });
            testBaseResponder.Library.RegisterFunction("visited_count", delegate (string node)
            {
                return dialogue.GetNodeVisitCount(node);
            });
            testBaseResponder.Library.RegisterFunction("has_any_content", delegate (string nodeGroup)
            {
                return dialogue.HasAnyContent(nodeGroup);
            });

            dialogue.Responder = testBaseResponder;

            dialogue.ContentSaliencyStrategy = new Yarn.Saliency.BestLeastRecentlyViewedSaliencyStrategy(storage);

            this.output = outputHelper;

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

            testBaseResponder.Library.RegisterFunction("assert", delegate (bool value)
            {
                value.Should().BeTrue("assertion should pass");
                return true;
            });
        }

        protected readonly ITestOutputHelper output;
        protected IVariableStorage storage = new DebugMemoryVariableStore();
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

    public class TestBaseResponder: DialogueResponder
    {
        public BasicFunctionLibrary Library = new();
        public delegate ValueTask ReceivedLineHandle(Line line, CancellationToken token);
        public ReceivedLineHandle OnReceivedLine;
        public delegate ValueTask<int> ReceivedOptionsHandle(OptionSet options, CancellationToken token);
        public ReceivedOptionsHandle OnReceivedOptions;
        public delegate ValueTask ReceivedCommandHandle(Command command, CancellationToken token);
        public ReceivedCommandHandle OnReceivedCommand;
        public delegate ValueTask ReceivedNodeStartHandle(string node, CancellationToken token);
        public ReceivedNodeStartHandle OnReceivedNodeStart;
        public delegate ValueTask ReceivedNodeCompleteHandle(string node, CancellationToken token);
        public ReceivedNodeCompleteHandle OnReceivedNodeComplete;
        public delegate ValueTask ReceivedDialogueCompleteHandle();
        public ReceivedDialogueCompleteHandle OnReceivedDialogueComplete;
        public delegate ValueTask PrepareForLinesHandler(List<string> lineIDs, CancellationToken token);
        public PrepareForLinesHandler OnPrepareForLines;

        public void Reset()
        {
            Library.Clear();
            OnReceivedLine = null;
            OnReceivedOptions = null;
            OnReceivedCommand = null;
            OnReceivedNodeStart = null;
            OnReceivedNodeComplete = null;
            OnReceivedDialogueComplete = null;
            OnPrepareForLines = null;
        }

        public ValueTask HandleLine(Line line, CancellationToken token)
        {
            if (OnReceivedLine != null)
            {
                return OnReceivedLine(line, token);
            }
            throw new NotImplementedException();
        }

        public ValueTask<int> HandleOptions(OptionSet options, CancellationToken token)
        {
            if (OnReceivedOptions != null)
            {
                return OnReceivedOptions(options, token);
            }
            throw new NotImplementedException();
        }

        public ValueTask HandleCommand(Command command, CancellationToken token)
        {
            if (OnReceivedCommand != null)
            {
                return OnReceivedCommand(command, token);
            }
            throw new NotImplementedException();
        }

        public ValueTask HandleNodeStart(string node, CancellationToken token)
        {
            if (OnReceivedNodeStart != null)
            {
                return OnReceivedNodeStart(node, token);
            }
            throw new NotImplementedException();
        }

        public ValueTask HandleNodeComplete(string node, CancellationToken token)
        {
            if (OnReceivedNodeComplete != null)
            {
                return OnReceivedNodeComplete(node, token);
            }
            throw new NotImplementedException();
        }

        public ValueTask HandleDialogueComplete()
        {
            if (OnReceivedDialogueComplete != null)
            {
                return OnReceivedDialogueComplete();
            }
            throw new NotImplementedException();
        }

        public ValueTask PrepareForLines(List<string> lineIDs, CancellationToken token)
        {
            if (OnPrepareForLines != null)
            {
                return OnPrepareForLines(lineIDs, token);
            }
            throw new NotImplementedException();
        }

        public ValueTask<IConvertible> thunk(string functionName, IConvertible[] parameters, CancellationToken token)
        {
            return Library.Invoke(functionName, parameters, token);
        }

        public bool TryGetFunctionDefinition(string functionName, out FunctionDefinition functionDefinition)
        {
            return Library.TryGetFunctionDefinition(functionName, out functionDefinition);
        }

        public Dictionary<string, FunctionDefinition> allDefinitions => Library.allDefinitions;
    }
}

