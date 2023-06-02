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
                Stop,

                // sets a variable to a value
                Set,

                // runs a new node.
                Run
            }

            public Type type {get;private set;}
            public bool IsBlocking => !(this.type == Type.Set || this.type == Type.Run);

            public List<IConvertible> parameters;
            
            public Step(string s) {

                parameters = new List<IConvertible>();

                var reader = new Reader(s);

                try {
                    type = reader.ReadNext<Type>();

                    if (type == Type.Stop) {
                        return;
                    }

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
                            
                            var stringValue = reader.ReadToEnd().Trim();
                            if (stringValue == "*") {
                                // '*' represents "we want to see an option
                                // but don't care what its text is" -
                                // represent this as the null value
                                stringValue = null; 
                            }

                            bool expectOptionEnabled = true;

                            // Options whose text ends with " [disabled]"
                            // are expected to be present, but have their
                            // 'allowed' flag set to false
                            if (type == Type.Option && stringValue.EndsWith(" [disabled]")) {
                                expectOptionEnabled = false;
                                stringValue = stringValue.Replace(" [disabled]", "");
                            }
                            parameters.Add(stringValue);
                            if (type == Type.Option) {
                                parameters.Add(expectOptionEnabled);
                            }
                            break;

                        case Type.Select:
                            var intValue = reader.ReadNext<int>();

                            if (intValue < 1) {
                                throw new ArgumentOutOfRangeException($"Cannot select option {intValue} - must be >= 1");
                            }

                            parameters.Add(intValue);
                            break;
                        case Type.Run:
                            var nodeName = reader.ReadNext<string>();
                            parameters.Add(nodeName);
                            break;

                        case Type.Set:
                            var variableName = reader.ReadNext<string>();
                            var value = reader.ReadNext<string>();

                            if (variableName.StartsWith("$") == false) {
                                throw new ArgumentException($"Variables must start with $");
                            }

                            parameters.Add(variableName);
                            parameters.Add(value);
                            break;

                        default:
                            throw new ArgumentException($"Unhandled type {type}");
                    }
                } catch (Exception e) {
                    // there was a syntax or semantic error
                    throw new ArgumentException($"Failed to parse step line: '{s}' (reason: {e.Message})", e);
                }
                


            }

            internal Step(Type type, string stringValue) {
                this.type = type;
                this.parameters = new List<IConvertible> { stringValue };
            }

            internal Step(Type type, int intValue) {
                this.type = type;
                this.parameters = new List<IConvertible> { intValue };
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
                        if (char.IsLetterOrDigit(next))
                            continue;
                        if (next == '_')
                            continue;
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

        public Step CurrentStep => currentTestPlanStep < Steps.Count ? Steps[currentTestPlanStep] : null;
        
        public TestPlan.Step.Type nextExpectedType;
        public List<(string line, bool enabled)> nextExpectedOptions = new List<(string line, bool enabled)>();
        public int nextOptionToSelect = -1;
        public string nextExpectedValue = null;

        // The delegate to call when a 'set' step is run. Receives the variable
        // name and the value.
        public Action<string, string> onSetVariable;

        // The delegate to call when a 'run' step is run. Receives the node name.
        public Action<string> onRunNode;

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
                    case Step.Type.Set:
                        var name = (string)currentStep.parameters[0];
                        var value = (string)currentStep.parameters[1];
                        onSetVariable(name, value);
                        continue;
                    case Step.Type.Run:
                        var node = (string)currentStep.parameters[0];
                        onRunNode(node);
                        continue;
                    case Step.Type.Stop:
                        nextExpectedType = currentStep.type;
                        goto done;
                    case Step.Type.Line:
                    case Step.Type.Command:
                        nextExpectedType = currentStep.type;
                        nextExpectedValue = (string)currentStep.parameters[0];
                        goto done;
                    case Step.Type.Select:
                        nextExpectedType = currentStep.type;
                        nextOptionToSelect = (int)currentStep.parameters[0];
                        goto done;
                    case Step.Type.Option:
                        nextExpectedOptions.Add(((string)currentStep.parameters[0], (bool)currentStep.parameters[1]));
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
            TestPlan.Step item = new TestPlan.Step(TestPlan.Step.Type.Option, text);
            item.parameters.Add(true); // 'is command expected to be available' parameter
            this.testPlan.Steps.Add(item);
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
