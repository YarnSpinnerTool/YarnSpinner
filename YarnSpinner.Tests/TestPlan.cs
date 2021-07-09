using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace YarnSpinner.Tests
{
    public class TestPlan
    {
        public class Step
        {
            public enum Type
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
                Stop
            }

            public Type type {get;private set;}

            public string stringValue;
            public int intValue;

            public bool expectOptionEnabled = true;
            
            public Step(string s) {
                intValue = -1;
                stringValue = null;

                var reader = new Reader(s);

                try {
                    type = reader.ReadNext<Type>();

                    var delimiter = (char)reader.Read();
                    if (delimiter != ':') {
                        throw new ArgumentException("Expected ':' after step type");
                    }

                    switch (type) {
                        // for lines, options and commands: we expect to
                        // see the rest of this line
                        case Type.Line:
                        case Type.Option:
                        case Type.Command:
                            stringValue = reader.ReadToEnd().Trim();
                            if (stringValue == "*") {
                                // '*' represents "we want to see an option
                                // but don't care what its text is" -
                                // represent this as the null value
                                stringValue = null; 
                            }

                            // Options whose text ends with " [disabled]"
                            // are expected to be present, but have their
                            // 'allowed' flag set to false
                            if (type == Type.Option && stringValue.EndsWith(" [disabled]")) {
                                expectOptionEnabled = false;
                                stringValue = stringValue.Replace(" [disabled]", "");
                            }
                            break;
                        
                        case Type.Select:
                            intValue = reader.ReadNext<int>();

                            if (intValue < 1) {
                                throw new ArgumentOutOfRangeException($"Cannot select option {intValue} - must be >= 1");
                            }

                            break;                           
                    }
                } catch (Exception e) {
                    // there was a syntax or semantic error
                    throw new ArgumentException($"Failed to parse step line: '{s}' (reason: {e.Message})");
                }
                


            }

            internal Step(Type type, string stringValue) {
                this.type = type;
                this.stringValue = stringValue;
            }

            internal Step(Type type, int intValue) {
                this.type = type;
                this.intValue = intValue;
            }

            internal Step(Type type) {

            }

            private class Reader : StringReader
            {
                // hat tip to user Dennis from Stackoverflow:
                // https://stackoverflow.com/a/26669930/2153213
                public Reader(string s) : base(s) { }

                // Parse the next T from this string, ignoring leading
                // whitespace
                public T ReadNext<T>() where T : System.IConvertible
                {                    
                    var sb = new StringBuilder();

                    do
                    {
                        var current = Read();
                        if (current < 0)
                            break;

                        // eat leading whitespace
                        if (char.IsWhiteSpace((char)current))
                            continue;

                        sb.Append((char)current);

                        var next = (char)Peek();
                        if (char.IsLetterOrDigit(next) == false)
                            break;

                    } while (true);

                    var value = sb.ToString();

                    var type = typeof(T);
                    if (type.IsEnum)
                        return (T)Enum.Parse(type, value, true);

                    return (T)((IConvertible)value).ToType(
                        typeof(T), 
                        System.Globalization.CultureInfo.InvariantCulture
                    );
                }


            }
        }

        internal List<Step> Steps = new List<Step>();

        private int currentTestPlanStep = 0;

        public TestPlan.Step.Type nextExpectedType;
        public List<(string line, bool enabled)> nextExpectedOptions = new List<(string line, bool enabled)>();
        public int nextOptionToSelect = -1;
        public string nextExpectedValue = null;

        internal TestPlan() {
            // Start with the empty step
        }

        public TestPlan(string path)
        {
            Steps = File.ReadAllLines(path)
                .Where(line => line.TrimStart().StartsWith("#") == false) // skip commented lines
                .Where(line => line.Trim() != "") // skip empty or blank lines
                .Select(line => new Step(line)) // convert remaining lines to steps
                .ToList();
        }

        public void Next() {
            // step through the test plan until we hit an expectation to
            // see a line, option, or command. specifically, we're waiting
            // to see if we got a Line, Select, Command or Assert step
            // type.

            if (nextExpectedType == Step.Type.Select) {
                // our previously-notified task was to select an option.
                // we've now moved past that, so clear the list of expected
                // options.
                nextExpectedOptions.Clear();
                nextOptionToSelect = 0;
            }

            while (currentTestPlanStep < Steps.Count) {
                
                Step currentStep = Steps[currentTestPlanStep];

                currentTestPlanStep += 1;

                switch (currentStep.type) {
                    case Step.Type.Line:
                    case Step.Type.Command:
                    
                    case Step.Type.Stop:
                        nextExpectedType = currentStep.type;
                        nextExpectedValue = currentStep.stringValue;
                        goto done;
                    case Step.Type.Select:
                        nextExpectedType = currentStep.type;
                        nextOptionToSelect = currentStep.intValue;
                        goto done;
                    case Step.Type.Option:
                        nextExpectedOptions.Add((currentStep.stringValue, currentStep.expectOptionEnabled));
                        continue;                                           
                }
            } 

            // We've fallen off the end of the test plan step list. We
            // expect a stop here.
            nextExpectedType = Step.Type.Stop;

            done:
            return;
        }


    }

    public class TestPlanBuilder {

        private TestPlan testPlan;

        public TestPlanBuilder() {
            testPlan = new TestPlan();
        }

        public TestPlan GetPlan() {
            return testPlan;
        }

        public TestPlanBuilder AddLine(string line) {
            testPlan.Steps.Add(new TestPlan.Step(TestPlan.Step.Type.Line, line));
            return this;
        }

        public TestPlanBuilder AddOption(string text = null) {
            testPlan.Steps.Add(new TestPlan.Step(TestPlan.Step.Type.Option, text));
            return this;

        }

        public TestPlanBuilder AddSelect(int value) {
            testPlan.Steps.Add(new TestPlan.Step(TestPlan.Step.Type.Select, value));
            return this;

        }

        public TestPlanBuilder AddCommand(string command) {
            testPlan.Steps.Add(new TestPlan.Step(TestPlan.Step.Type.Command, command));
            return this;

        }

        public TestPlanBuilder AddStop() {
            testPlan.Steps.Add(new TestPlan.Step(TestPlan.Step.Type.Stop));
            return this;
        }

    }


}
