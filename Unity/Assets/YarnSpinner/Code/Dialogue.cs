/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn {

    /// Represents things that can go wrong while loading or running a dialogue.
    public  class YarnException : Exception {
        public YarnException(string message) : base(message) {}
    }

    // Delegates, which are used by the client.

    /// OptionChoosers let the client tell the Dialogue about what
    /// response option the user selected.
    public delegate void OptionChooser (int selectedOptionIndex);

    /// Loggers let the client send output to a console, for both debugging
    /// and error logging.
    public delegate void Logger(string message);

    /// Information about stuff that the client should handle.
    /** (Currently this just wraps a single field, but doing it like this
     * gives us the option to add more stuff later without breaking the API.)
     */
    public struct Line { public string text; }
    public struct Options { public IList<string> options; }
    public struct Command { public string text; }

    /// Where we turn to for storing and loading variable data.
    public interface VariableStorage {

        [Obsolete] void SetNumber(string variableName, float number);
        [Obsolete] float GetNumber(string variableName);
        void SetValue(string variableName, Value value);
        Value GetValue(string variableName);
        void Clear();
    }

    public abstract class BaseVariableStorage : VariableStorage {
        [Obsolete]
        public void SetNumber(string variableName, float number) {
            this.SetValue(variableName, new Value(number));
        }

        [Obsolete]
        public float GetNumber(string variableName) {
            return this.GetValue(variableName).AsNumber;
        }

        public abstract void SetValue(string variableName, Value value);
        public abstract Value GetValue(string variableName);
        public abstract void Clear();
    }

    /// A line, localised into the current locale.
    /** LocalisedLines are used in both lines, options, and shortcut options - basically,
     * anything user-facing.
     */
    public class LocalisedLine
    {
        public string LineCode { get; set; }
        public string LineText { get; set; }
        public string Comment { get; set; }
    }

    /// Very simple continuity class that keeps all variables in memory
    public class MemoryVariableStore : Yarn.BaseVariableStorage
    {
        Dictionary<string, Value> variables = new Dictionary<string, Value>();

        public override void SetValue(string variableName, Value value)
        {
            variables[variableName] = value;
        }

        public override Value GetValue(string variableName)
        {
            Value value = Value.NULL;
            if (variables.ContainsKey(variableName))
            {

                value = variables[variableName];

            }
            return value;
        }

        public override void Clear()
        {
            variables.Clear();
        }
    }

    /// The Dialogue class is the main thing that clients will use.
    public class Dialogue  {


		internal VariableStorage continuity;
		/// We'll ask this object for the state of variables
        public bool experimentalMode = false;
        // currently this is just the ANTLR compiler but it could change over time
        // used to determine if the dialogue should use the experimental features

        /// Represents something for the end user ("client") of the Dialogue class to do.
        public abstract class RunnerResult { }

        /// The client should run a line of dialogue.
        public class LineResult : RunnerResult  {

            public Line line;

            public LineResult (string text) {
                var line = new Line();
                line.text = text;
                this.line = line;
            }

        }

        /// The client should run a command (it's up to them to parse the string)
        public class CommandResult: RunnerResult {
            public Command command;

            public CommandResult (string text) {
                var command = new Command();
                command.text = text;
                this.command = command;
            }

        }

        /// The client should show a list of options, and call
        /// setSelectedOptionDelegate before asking for the
        /// next line. It's an error if you don't.
        public class OptionSetResult : RunnerResult {
            public Options options;
            public OptionChooser setSelectedOptionDelegate;

            public OptionSetResult (IList<string> optionStrings, OptionChooser setSelectedOption) {
                var options = new Options();
                options.options = optionStrings;
                this.options = options;
                this.setSelectedOptionDelegate = setSelectedOption;
            }

        }

        /// We've reached the end of this node.
        public class NodeCompleteResult: RunnerResult {
            public string nextNode;

            public NodeCompleteResult (string nextNode) {
                this.nextNode = nextNode;
            }
        }

        /// Delegates used for logging.
        public Logger LogDebugMessage;
        public Logger LogErrorMessage;

        /// The node we start from.
        public const string DEFAULT_START = "Start";

        /// The loader contains all of the nodes we're going to run.
        internal Loader loader;

        /// The Program is the compiled Yarn program.
        internal Program program;

        /// The library contains all of the functions and operators we know about.
        public Library library;

        /// The collection of nodes that we've seen.
        public Dictionary<String, int> visitedNodeCount = new Dictionary<string, int>();

        /// A function exposed to Yarn that returns the number of times a node has been run.
        /** If no parameters are supplied, returns the number of time the current node
         * has been run.
         */
        object YarnFunctionNodeVisitCount (Value[] parameters)
        {
            // Determine the node we're checking
            string nodeName;

            if (parameters.Length == 0) {
                // No parameters? Check the current node
                nodeName = vm.currentNodeName;
            } else if (parameters.Length == 1) {
                // A parameter? Check the named node
                nodeName = parameters [0].AsString;

                // Ensure this node exists
                if (NodeExists (nodeName) == false) {
                    var errorMessage = string.Format ("The node {0} does not " + "exist.", nodeName);
                    LogErrorMessage (errorMessage);
                    return 0;
                }
            } else {
                // We got too many parameters
                var errorMessage = string.Format ("Incorrect number of parameters to " + "visitCount (expected 0 or 1, got {0})", parameters.Length);
                LogErrorMessage (errorMessage);
                return 0;
            }

            // Figure out how many times this node was run
            int visitCount = 0;
            visitedNodeCount.TryGetValue (nodeName, out visitCount);

            return visitCount;
        }

        /// A Yarn function that returns true if the named node, or the current node
        /// if no parameters were provided, has been visited at least once.
        object YarnFunctionIsNodeVisited (Value[] parameters)
        {
            return (int)YarnFunctionNodeVisitCount(parameters) > 0;
        }

        public Dialogue(Yarn.VariableStorage continuity) {
            this.continuity = continuity;
            loader = new Loader (this);
            library = new Library ();

            library.ImportLibrary (new StandardLibrary ());

            // Register the "visited" function, which returns true if we've visited
            // a node previously (nodes are marked as visited when we leave them)
            library.RegisterFunction ("visited", -1, (ReturningFunction)YarnFunctionIsNodeVisited);

            // Register the "visitCount" function, which returns the number of times
            // a node has been run (which increments when a node ends). If called with
            // no parameters, check the CURRENT node.
            library.RegisterFunction ("visitCount", -1, (ReturningFunction)YarnFunctionNodeVisitCount);

        }

        /// Load a file from disk.
        public void LoadFile(string fileName, bool showTokens = false, bool showParseTree = false, string onlyConsiderNode=null) {

            // Is this a compiled program file?
            if (fileName.EndsWith(".yarn.bytes")) {

                var bytes = System.IO.File.ReadAllBytes(fileName);
                LoadCompiledProgram(bytes, fileName);

                return;
            } else {
                // It's source code, either a single node in text form or a JSON file
                string inputString;
                using (System.IO.StreamReader reader = new System.IO.StreamReader(fileName))
                {
                    inputString = reader.ReadToEnd();
                }

                LoadString(inputString, fileName, showTokens, showParseTree, onlyConsiderNode);
            }

        }

        public void LoadCompiledProgram(byte[] bytes, string fileName, CompiledFormat format = LATEST_FORMAT)
        {

            if (LogDebugMessage == null)
            {
                throw new YarnException("LogDebugMessage must be set before loading");
            }

            if (LogErrorMessage == null)
            {
                throw new YarnException("LogErrorMessage must be set before loading");
            }

            switch (format)
            {
                case CompiledFormat.V1:
                    LoadCompiledProgramV1(bytes);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

        }

        private void LoadCompiledProgramV1(byte[] bytes)
        {
            using (var stream = new System.IO.MemoryStream(bytes))
            {
                using (var reader = new Newtonsoft.Json.Bson.BsonReader(stream))
                {
                    var serializer = new Newtonsoft.Json.JsonSerializer();

                    try
                    {
                        // Load the stored program
                        var newProgram = serializer.Deserialize<Program>(reader);

                        // Merge it with our existing one, if present
                        if (program != null)
                        {
                            program.Include(newProgram);
                        }
                        else {
                            program = newProgram;
                        }
                    }
                    catch (Newtonsoft.Json.JsonReaderException e)
                    {
                        LogErrorMessage(string.Format("Cannot load compiled program: {0}", e.Message));
                    }
                }
            }
        }

        /// Ask the loader to parse a string.
        /** Returns the number of nodes that were loaded.
         */
        public void LoadString(string text, string fileName="<input>", bool showTokens=false, bool showParseTree=false, string onlyConsiderNode=null) {

            if (LogDebugMessage == null) {
                throw new YarnException ("LogDebugMessage must be set before loading");
            }

            if (LogErrorMessage == null) {
                throw new YarnException ("LogErrorMessage must be set before loading");
            }

            // Try to infer the type

            NodeFormat format;

            if (text.StartsWith("[", StringComparison.Ordinal)) {
                // starts with a {? this is probably a JSON array
                format = NodeFormat.JSON;
            } else if (text.Contains("---")) {
                // contains a --- delimiter? probably multi node text
                format = NodeFormat.Text;
            } else {
                // fall back to the single node format
                format = NodeFormat.SingleNodeText;
            }

            program = loader.Load(text, library, fileName, program, showTokens, showParseTree, onlyConsiderNode, format, this.experimentalMode);

        }

        private VirtualMachine vm;

        // Executes a node.
        /** Use this in a for-each construct; each time you iterate over it,
         * you'll get a line, command, or set of options.
         */
        public IEnumerable<Yarn.Dialogue.RunnerResult> Run(string startNode = DEFAULT_START) {

            if (LogDebugMessage == null) {
                throw new YarnException ("LogDebugMessage must be set before running");
            }

            if (LogErrorMessage == null) {
                throw new YarnException ("LogErrorMessage must be set before running");
            }

            if (program == null) {
                LogErrorMessage ("Dialogue.Run was called, but no program was loaded. Stopping.");
                yield break;
            }

            vm = new VirtualMachine (this, program);

            RunnerResult latestResult;

            vm.lineHandler = delegate(LineResult result) {
                latestResult = result;
            };

            vm.commandHandler = delegate(CommandResult result) {
                // Is it the special custom command "<<stop>>"?
                if (result is CommandResult && (result as CommandResult).command.text == "stop") {
                    vm.Stop();
                }
                latestResult = result;
            };

            vm.nodeCompleteHandler = delegate(NodeCompleteResult result) {

                // get the count if it's there, otherwise it defaults to 0
                int count = 0;
                visitedNodeCount.TryGetValue(vm.currentNodeName, out count);

                visitedNodeCount[vm.currentNodeName] = count + 1;
                latestResult = result;
            };

            vm.optionsHandler = delegate(OptionSetResult result) {
                latestResult = result;
            };

            if (vm.SetNode (startNode) == false) {
                yield break;
            }

            // Run until the program stops, pausing to yield important
            // results
            do {

                latestResult = null;
                vm.RunNext ();
                if (latestResult != null)
                    yield return latestResult;

            } while (vm.executionState != VirtualMachine.ExecutionState.Stopped);

        }

        public void Stop() {
            if (vm != null)
                vm.Stop();
        }

        public IEnumerable<string> visitedNodes {
            get {
                return visitedNodeCount.Keys;
            }
            set {
                visitedNodeCount = new Dictionary<string, int>();
                foreach (var entry in value) {
                    visitedNodeCount[entry] = 1;
                }
            }
        }

        public IEnumerable<string> allNodes {
            get {
                return program.nodes.Keys;
            }
        }

        public string currentNode {
            get {
                if (vm == null) {
                    return null;
                } else {
                    return vm.currentNodeName;
                }

            }
        }

        public Dictionary<string, string> GetTextForAllNodes() {
            var d = new Dictionary<string,string>();

            foreach (var node in program.nodes) {
                var text = program.GetTextForNode(node.Key);

                if (text == null)
                    continue;

                d [node.Key] = text;
            }

            return d;
        }

        /// Returns the source code for the node 'nodeName', if that node was tagged with rawText.
        public string GetTextForNode(string nodeName) {
            if (program.nodes.Count == 0) {
                LogErrorMessage ("No nodes are loaded!");
                return null;
            } else if (program.nodes.ContainsKey(nodeName)) {
                return program.GetTextForNode (nodeName);
            } else {
                LogErrorMessage ("No node named " + nodeName);
                return null;
            }
        }

        public void AddStringTable(Dictionary<string, string> stringTable)
        {
            program.LoadStrings(stringTable);
        }

        public Dictionary<string,string> GetStringTable() {
            return program.strings;
        }

        internal Dictionary<string,LineInfo> GetStringInfoTable() {
            return program.lineInfo;
        }

        public enum CompiledFormat
        {
            V1
        }

        public const CompiledFormat LATEST_FORMAT = CompiledFormat.V1;

        public byte[] GetCompiledProgram(CompiledFormat format = LATEST_FORMAT)
        {

            switch (format)
            {
                case CompiledFormat.V1:
                    return GetCompiledProgramV1();
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private byte[] GetCompiledProgramV1()
        {
            using (var outputStream = new System.IO.MemoryStream())
            {
                using (var bsonWriter = new Newtonsoft.Json.Bson.BsonWriter(outputStream))
                {
                    var s = new Newtonsoft.Json.JsonSerializer();
                    s.Serialize(bsonWriter, this.program);
                }

                return outputStream.ToArray();
            }
        }

        /// Unloads ALL nodes.
        public void UnloadAll(bool clearVisitedNodes = true) {
            if (clearVisitedNodes)
                visitedNodeCount.Clear();

            program = null;

        }

        [Obsolete("Calling Compile() is no longer necessary.")]
        public String Compile() {
            return program.DumpCode (library);
        }

        public String GetByteCode() {
            return program.DumpCode (library);
        }

        public bool NodeExists(string nodeName) {
            if (program == null) {

                if (program.nodes.Count > 0) {
                    LogErrorMessage ("Internal consistency error: Called NodeExists, and " +
                                     "there are nodes loaded, but the program hasn't " +
                                     "been compiled yet, somehow?");

                    return false;

                } else {
                    LogErrorMessage ("Tried to call NodeExists, but no nodes " +
                                     "have been compiled!");
                    return false;
                }
            }
            if (program.nodes == null || program.nodes.Count == 0) {
                LogDebugMessage ("Called NodeExists, but there are zero nodes. " +
                                 "This may be an error.");
                return false;
            }
            return program.nodes.ContainsKey(nodeName);
        }

        public void Analyse(Analysis.Context context) {

            context.AddProgramToAnalysis (this.program);

        }

        /// The standard, built-in library of functions and operators.
        private class StandardLibrary : Library {

            public StandardLibrary() {

                #region Operators

                this.RegisterFunction(TokenType.Add.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] + parameters[1];
                });

                this.RegisterFunction(TokenType.Minus.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] - parameters[1];
                });

                this.RegisterFunction(TokenType.UnaryMinus.ToString(), 1, delegate(Value[] parameters) {
                    return -parameters[0];
                });

                this.RegisterFunction(TokenType.Divide.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] / parameters[1];
                });

                this.RegisterFunction(TokenType.Multiply.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] * parameters[1];
                });

                this.RegisterFunction(TokenType.Modulo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] % parameters[1];
                });

                this.RegisterFunction(TokenType.EqualTo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].Equals( parameters[1] );
                });

                this.RegisterFunction(TokenType.NotEqualTo.ToString(), 2, delegate(Value[] parameters) {

                    // Return the logical negative of the == operator's result
                    var equalTo = this.GetFunction(TokenType.EqualTo.ToString());

                    return !equalTo.Invoke(parameters).AsBool;
                });

                this.RegisterFunction(TokenType.GreaterThan.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] > parameters[1];
                });

                this.RegisterFunction(TokenType.GreaterThanOrEqualTo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] >= parameters[1];
                });

                this.RegisterFunction(TokenType.LessThan.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] < parameters[1];
                });

                this.RegisterFunction(TokenType.LessThanOrEqualTo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] <= parameters[1];
                });

                this.RegisterFunction(TokenType.And.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].AsBool && parameters[1].AsBool;
                });

                this.RegisterFunction(TokenType.Or.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].AsBool || parameters[1].AsBool;
                });

                this.RegisterFunction(TokenType.Xor.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].AsBool ^ parameters[1].AsBool;
                });

                this.RegisterFunction(TokenType.Not.ToString(), 1, delegate(Value[] parameters) {
                    return !parameters[0].AsBool;
                });

                #endregion Operators
			}
		}

    }
}
