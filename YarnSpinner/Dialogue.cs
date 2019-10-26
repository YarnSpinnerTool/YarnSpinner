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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;

namespace Yarn {

    /// Represents things that can go wrong while loading or running a dialogue.
    [Serializable]
    public  class YarnException : Exception {
        public YarnException(string message) : base(message) {}

        protected YarnException(SerializationInfo serializationInfo, StreamingContext streamingContext) : base(serializationInfo, streamingContext) {}
    }

    public struct Line {
        public Line(string text) : this()
        {
            this.Text = text;            
        }

        public string Text { get; private set; }        
    }

    public struct OptionSet {
        public OptionSet(Option[] options)
        {
            Options = options;
        }

        public struct Option {
            public Option(Line line, int id)
            {
                Line = line;
                ID = id;
            }

            public Line Line {get; private set;}
            public int ID {get; private set;}
        }
        
        public Option[] Options {get; private set;}
    }

    public struct Command {
        public Command(string text)
        {
            Text = text;
        }

        public string Text {get; private set;}
    }

    // Delegates, which are used by the client.

    /// Loggers let the client send output to a console, for both debugging
    /// and error logging.
    public delegate void Logger(string message);

    /// Where we turn to for storing and loading variable data.
    public interface VariableStorage {
        void SetValue(string variableName, Value value);

        // some convenience setters
        void SetValue(string variableName, string stringValue);
        void SetValue(string variableName, float floatValue);
        void SetValue(string variableName, bool boolValue);

        Value GetValue(string variableName);
        void Clear();
    }

    public abstract class BaseVariableStorage : VariableStorage {
        public virtual void SetValue(string variableName, string stringValue)
        {
            Value val = new Yarn.Value(stringValue);
            SetValue(variableName, val);
        }
        public virtual void SetValue(string variableName, float floatValue)
        {
            Value val = new Yarn.Value(floatValue);
            SetValue(variableName, val);
        }
        public virtual void SetValue(string variableName, bool boolValue)
        {
            Value val = new Yarn.Value(boolValue);
            SetValue(variableName, val);
        }

        public abstract void SetValue(string variableName, Value value);
        public abstract Value GetValue(string variableName);
        public abstract void Clear();
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

        /// We'll ask this object for the state of variables
        internal VariableStorage continuity;
		
        /// Delegates used for logging.
        public Logger LogDebugMessage;
        public Logger LogErrorMessage;

        /// The node we start from.
        public const string DEFAULT_START = "Start";

        /// The Program is the compiled Yarn program.
        private Program _program;
        internal Program Program { get => _program; 
            set {
                _program = value;

                vm.Program = value;
                vm.ResetState();
            }
        }

        public bool IsActive => vm.executionState != VirtualMachine.ExecutionState.Stopped;

        public delegate bool LineHandler(Line line);
        public delegate void OptionsHandler(OptionSet options);
        public delegate bool CommandHandler(Command command);
        public delegate bool NodeCompleteHandler(string completedNodeName);
        public delegate void DialogueCompleteHandler();


        /// Called when a line is ready to be shown to the user.
        public LineHandler lineHandler
        {
            get => vm.lineHandler;
            set => vm.lineHandler = value;
        }

        /// Called when a set of options are ready to be shown to the user.
        /// Call <see>SetSelectedOption</see> to indicate that the
        /// selection has been made.
        public OptionsHandler optionsHandler
        {
            get => vm.optionsHandler;
            set => vm.optionsHandler = value;
        }

        /// Called when a command is to be delivered to the game.
        public CommandHandler commandHandler
        {
            get => vm.commandHandler;
            set => vm.commandHandler = value;
        }

        /// Called when a node is finished.
        public NodeCompleteHandler nodeCompleteHandler
        {
            get => vm.nodeCompleteHandler;
            set => vm.nodeCompleteHandler = value;
        }

        /// Called when all execution is complete, indicating that the
        /// dialogue is over.
        public DialogueCompleteHandler dialogueCompleteHandler
        {
            get => vm.dialogueCompleteHandler;
            set => vm.dialogueCompleteHandler = value;
        }

        private VirtualMachine vm;

        /// The library contains all of the functions and operators we know about.
        public Library library;

        /// The collection of nodes that we've seen.
        public Dictionary<String, int> visitedNodeCount = new Dictionary<string, int>();

        public Dialogue(Yarn.VariableStorage continuity) {
            this.continuity = continuity ?? throw new ArgumentNullException(nameof(continuity));
            library = new Library ();

            this.vm = new VirtualMachine(this);

            library.ImportLibrary (new StandardLibrary ());
        }

        /// Load a program object.
        public void LoadProgram(Program program) {
            this.Program = program;
        }

        /// Load a file from disk.
        public void LoadProgram(string fileName) {

            var bytes = File.ReadAllBytes (fileName);

            this.Program = Program.Parser.ParseFrom(bytes);
            
        }

        // Prepares to run the named node
        public void SetNode(string startNode = DEFAULT_START) {
            vm.SetNode (startNode);
        }

        public void SetSelectedOption(int selectedOptionID) {
            vm.SetSelectedOption(selectedOptionID);
        }
        
        public void Continue() {
            vm.Continue();
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
                return Program.Nodes.Keys;
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

            foreach (var node in Program.Nodes) {
                var text = Program.GetTextForNode(node.Key);

                if (text == null)
                    continue;

                d [node.Key] = text;
            }

            return d;
        }

        /// Returns the source code for the node 'nodeName', if that node was tagged with rawText.
        public string GetTextForNode(string nodeName) {
            if (Program.Nodes.Count == 0) {
                LogErrorMessage ("No nodes are loaded!");
                return null;
            } else if (Program.Nodes.ContainsKey(nodeName)) {
                return Program.GetTextForNode (nodeName);
            } else {
                LogErrorMessage ("No node named " + nodeName);
                return null;
            }
        }

		public Dictionary<string, IEnumerable<string>> GetTagsForAllNodes() {
			var d = new Dictionary<string,IEnumerable<string>>();

			foreach (var node in Program.Nodes) {
				var tags = Program.GetTagsForNode(node.Key);

				if (tags == null)
					continue;

				d [node.Key] = tags;
			}

			return d;
		}

		/// Returns the tags for the node 'nodeName'.
		public IEnumerable<string> GetTagsForNode(string nodeName) {
			if (Program.Nodes.Count == 0) {
				LogErrorMessage ("No nodes are loaded!");
				return null;
			} else if (Program.Nodes.ContainsKey(nodeName)) {
				return Program.GetTagsForNode (nodeName);
			} else {
				LogErrorMessage ("No node named " + nodeName);
				return null;
			}
		}

        public void AddStringTable(Dictionary<string, string> stringTable)
        {
            Program.LoadStrings(stringTable);
        }

        public IDictionary<string,string> GetStringTable() {
            return Program.StringTable;
        }

        internal IDictionary<string,LineInfo> GetStringInfoTable() {
            return Program.LineInfo;
        }

        /// Unloads ALL nodes.
        public void UnloadAll(bool clearVisitedNodes = true) {
            if (clearVisitedNodes)
                visitedNodeCount.Clear();

            Program = null;

        }

        public String GetByteCode() {
            return Program.DumpCode (library);
        }

        public bool NodeExists(string nodeName) {
            if (Program == null) {
                LogErrorMessage ("Tried to call NodeExists, but no nodes " +
                                 "have been compiled!");
                return false;
            }
            if (Program.Nodes == null || Program.Nodes.Count == 0) {
                LogDebugMessage ("Called NodeExists, but there are zero nodes. " +
                                 "This may be an error.");
                return false;
            }
            return Program.Nodes.ContainsKey(nodeName);
        }

        public void Analyse(Analysis.Context context) {

            context.AddProgramToAnalysis (this.Program);

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
