using Antlr4.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Xunit.Sdk;
using Yarn.Compiler;

#nullable enable

namespace YarnSpinner.Tests
{
    public class TestPlan : IEnumerable<TestPlan.Run>
    {

        public class Run : IEnumerable<Step>
        {
            public string StartNode { get; private set; } = "Start";
            
            public List<Step> Steps { get; init; } = new();

            public IEnumerator<Step> GetEnumerator()
            {
                return ((IEnumerable<Step>)this.Steps).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)this.Steps).GetEnumerator();
            }
        }

        public abstract class Step { }

        public abstract class ExpectContentStep : Step
        {
            public string? ExpectedText { get; init; }
        }

        public class ExpectLineStep : ExpectContentStep
        {
            public List<string> ExpectedHashtags { get; init; } = new();

            public ExpectLineStep(string? text, IEnumerable<string> hashtags)
            {
                this.ExpectedText = text;
                this.ExpectedHashtags.AddRange(hashtags);
            }
        }

        public class ExpectOptionStep : ExpectContentStep
        {
            public List<string> ExpectedHashtags { get; init; } = new();
            public bool ExpectedAvailability = true;

            public ExpectOptionStep(string? text, IEnumerable<string> hashtags, bool expectedAvailability)
            {
                this.ExpectedText = text;
                this.ExpectedHashtags.AddRange(hashtags);
                this.ExpectedAvailability = expectedAvailability;
            }
        }

        public class ExpectCommandStep : ExpectContentStep
        {
            public ExpectCommandStep(string text)
            {
                this.ExpectedText = text;
            }
        }

        public class ExpectStop : Step { }

        public class ActionSelectStep : Step
        {
            public int SelectedIndex { get; init; }

            public ActionSelectStep(int selectedIndex)
            {
                this.SelectedIndex = selectedIndex;
            }
        }

        public class ActionSetSaliencyStep : Step 
        {
            public string SaliencyMode { get; init; }

            public ActionSetSaliencyStep(string saliencyMode)
            {
                this.SaliencyMode = saliencyMode;
            }
        }

        public class ActionSetVariableStep : Step
        {
            public string VariableName { get; init; }
            public object Value { get; init; }

            public ActionSetVariableStep(string variableName, object value)
            {
                this.VariableName = variableName;
                this.Value = value;
            }
        }

        public class ActionJumpToNodeStep : Step
        {
            public string NodeName { get; init; }

            public ActionJumpToNodeStep(string nodeName)
            {
                this.NodeName = nodeName;
            }
        }

        public List<Run> Runs { get; init; } = new();

        public TestPlan() { }

        public static TestPlan FromFile(string path)
        {
            var text = File.ReadAllText(path);
            return FromString(text);
        }

        public static TestPlan FromString(string text)
        {
            var plan = new TestPlan();
            var charStream = CharStreams.fromString(text);
            var lexer = new YarnSpinnerTestPlanLexer(charStream);

            var tokenStream = new CommonTokenStream(lexer);
            var parser = new YarnSpinnerTestPlanParser(tokenStream);
            lexer.RemoveErrorListeners();
            parser.RemoveErrorListeners();
            var lexerErrorListener = new LexerErrorListener("testplan");
            var parserErrorListener = new ParserErrorListener("testplan");
            lexer.AddErrorListener(lexerErrorListener);
            parser.AddErrorListener(parserErrorListener);
            
            var testPlanTree = parser.testplan();

            var allDiagnostics = lexerErrorListener.Diagnostics.Concat(parserErrorListener.Diagnostics);
            if (allDiagnostics.Any(d => d.Severity == Diagnostic.DiagnosticSeverity.Error)) {
                throw new XunitException("Syntax errors in test plan: " + string.Join("\n", allDiagnostics));
            }

            foreach (var runContext in testPlanTree.run())
            {
                var run = new Run();
                Step step;
                foreach (var stepContext in runContext.step())
                {
                    if (stepContext.actionJumpToNode() != null)
                    {
                        step = new ActionJumpToNodeStep(
                           stepContext.actionJumpToNode().nodeName.Text
                       );
                    }
                    else if (stepContext.actionSelect() != null)
                    {
                        step = new ActionSelectStep(
                            int.Parse(
                                stepContext.actionSelect().NUMBER().GetText(),
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture
                            ) - 1
                        );
                    }
                    else if (stepContext.actionSet() != null)
                    {
                        if (stepContext.actionSet() is YarnSpinnerTestPlanParser.ActionSetBoolContext setBoolContext)
                        {
                            step = new ActionSetVariableStep(
                                setBoolContext.variable.Text,
                                bool.Parse(setBoolContext.value.Text)
                            );
                        }
                        else if (stepContext.actionSet() is YarnSpinnerTestPlanParser.ActionSetNumberContext setNumberContext)
                        {
                            step = new ActionSetVariableStep(
                                setNumberContext.variable.Text,
                                int.Parse(setNumberContext.value.Text, NumberStyles.Integer, CultureInfo.InvariantCulture)
                            );
                        }
                        else
                        {
                            throw new InvalidOperationException("Unhandled 'set' type: " + stepContext.GetTextWithWhitespace());
                        }
                    }
                    else if (stepContext.actionJumpToNode() != null)
                    {
                        step = new ActionJumpToNodeStep(
                            stepContext.actionJumpToNode().nodeName.Text
                        );
                    }
                    else if (stepContext.lineExpected() != null)
                    {
                        if (stepContext.lineExpected() is YarnSpinnerTestPlanParser.LineWithAnyTextExpectedContext any)
                        {
                            step = new ExpectLineStep(
                                null,
                                any.hashtag().Select(h => h.GetText())
                            );
                        }
                        else if (stepContext.lineExpected() is YarnSpinnerTestPlanParser.LineWithSpecificTextExpectedContext specific)
                        {
                            step = new ExpectLineStep(
                                specific.TEXT().GetText().Trim('`'),
                                specific.hashtag().Select(h => h.GetText())
                            );
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    else if (stepContext.optionExpected() != null)
                    {
                        step = new ExpectOptionStep(
                            stepContext.optionExpected().TEXT().GetText().Trim('`'),
                            stepContext.optionExpected().hashtag().Select(h => h.GetText()),
                            stepContext.optionExpected().isDisabled == null
                        );
                    }
                    else if (stepContext.commandExpected() != null)
                    {
                        step = new ExpectCommandStep(
                            stepContext.commandExpected().TEXT().GetText().Trim('`')
                        );
                    }
                    else if (stepContext.stopExpected() != null)
                    {
                        step = new ExpectStop();
                    }
                    else if (stepContext.actionSetSaliencyMode() != null)
                    {
                        step = new ActionSetSaliencyStep(
                            stepContext.actionSetSaliencyMode().saliencyMode.Text
                        );
                    }
                    else
                    {
                        throw new InvalidOperationException("Unhandled step type: " + stepContext.GetTextWithWhitespace());
                    }
                    run.Steps.Add(step);
                }
                plan.Runs.Add(run);

            }

            return plan;
        }

        public IEnumerator<Run> GetEnumerator()
        {
            return ((IEnumerable<Run>)this.Runs).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.Runs).GetEnumerator();
        }
    }
    public enum StepType
    {
        // expecting to see this specific line
        Line,

        // expecting to see this specific option (if '*' is given,
        // means 'see an option, don't care about text')
        Option,

        // expecting options to have been presented; value = the
        // index to select
        Select,

        // expecting to see this specific command
        Command,

        // expecting to stop the test here (this is optional - a
        // 'stop' at the end of a test plan is assumed)
        Stop,

        // sets a variable to a value
        Set,

        // runs a new node.
        Run
    }

    public class TestPlanBuilder {
        private TestPlan testPlan;
        private TestPlan.Run currentRun;

        public TestPlanBuilder() {
            testPlan = new TestPlan();
            currentRun = new TestPlan.Run();
            testPlan.Runs.Add(currentRun);
        }

        public TestPlan GetPlan() {
            return testPlan;
        }

        public TestPlanBuilder AddLine(string line) {
            currentRun.Steps.Add(new TestPlan.ExpectLineStep(line, Array.Empty<string>()));
            return this;
        }

        public TestPlanBuilder AddOption(string? text = null) {
            currentRun.Steps.Add(new TestPlan.ExpectOptionStep(text, Array.Empty<string>(), true));
            return this;
        }

        public TestPlanBuilder AddSelect(int value) {
            currentRun.Steps.Add(new TestPlan.ActionSelectStep(value));
            return this;
        }

        public TestPlanBuilder AddCommand(string command) {
            currentRun.Steps.Add(new TestPlan.ExpectCommandStep(command));
            return this;
        }

        public TestPlanBuilder AddStop() {
            currentRun.Steps.Add(new TestPlan.ExpectStop());
            return this;
        }
    }
}
