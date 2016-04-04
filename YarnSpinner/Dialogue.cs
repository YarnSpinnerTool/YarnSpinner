/*

The MIT License (MIT)

Copyright (c) 2015 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

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

	// Represents things that can go wrong while loading or running
	// a dialogue.
	public  class YarnException : Exception {
		public YarnException(string message) : base(message) {}
	}

	// Delegates, which are used by the client.

	// OptionChoosers let the client tell the Dialogue about what
	// response option the user selected.
	public delegate void OptionChooser (int selectedOptionIndex);

	// Loggers let the client send output to a console, for both debugging
	// and error logging.
	public delegate void Logger(string message);

	// Information about stuff that the client should handle.
	// (Currently this just wraps a single field, but doing it like this
	// gives us the option to add more stuff later without breaking the API.)
	public struct Line { public string text; }
	public struct Options { public IList<string> options; }
	public struct Command { public string text; }

	// Where we turn to for storing and loading variable data.
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

	// The Dialogue class is the main thing that clients will use.
	public class Dialogue  {

		// We'll ask this object for the state of variables
		internal VariableStorage continuity;

		// Represents something for the end user ("client") of the Dialogue class to do.
		public abstract class RunnerResult { }

		// The client should run a line of dialogue.
		public class LineResult : RunnerResult  {

			public Line line;

			public LineResult (string text) {
				var line = new Line();
				line.text = text;
				this.line = line;
			}

		}

		// The client should run a command (it's up to them to parse the string)
		public class CommandResult: RunnerResult {
			public Command command;

			public CommandResult (string text) {
				var command = new Command();
				command.text = text;
				this.command = command;
			}

		}

		// The client should show a list of options, and call
		// setSelectedOptionDelegate before asking for the
		// next line. It's an error if you don't.
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

		// We've reached the end of this node.
		public class NodeCompleteResult: RunnerResult {
			public string nextNode;

			public NodeCompleteResult (string nextNode) {
				this.nextNode = nextNode;
			}
		}

		// Delegates used for logging.
		public Logger LogDebugMessage;
		public Logger LogErrorMessage;

		// The node we start from.
		public const string DEFAULT_START = "Start";

		// The loader contains all of the nodes we're going to run.
		private Loader loader;

		// The Program is the compiled Yarn program.
		private Program program;

		// The library contains all of the functions and operators we know about.
		public Library library;

		// The collection of nodes that we've seen.
		private HashSet<String> visitedNodeNames = new HashSet<string>();

		public Dialogue(Yarn.VariableStorage continuity) {
			this.continuity = continuity;
			loader = new Loader (this);
			library = new Library ();

			library.ImportLibrary (new StandardLibrary ());

			// Register the "visited" function, which returns true if we've visited
			// a node previously (nodes are marked as visited when we leave them)
			library.RegisterFunction ("visited", 1, delegate(Yarn.Value[] parameters) {
				var name = parameters[0].AsString;
				return visitedNodeNames.Contains(name);
			});

		}

		// Load a file from disk.
		public void LoadFile(string fileName, bool showTokens = false, bool showParseTree = false, string onlyConsiderNode=null) {
			System.IO.StreamReader reader = new System.IO.StreamReader(fileName);
			string inputString = reader.ReadToEnd ();
			reader.Close ();

			LoadString (inputString, fileName, showTokens, showParseTree, onlyConsiderNode);

		}

		// Ask the loader to parse a string. Returns the number of nodes that were loaded.
		public void LoadString(string text, string fileName="<input>", bool showTokens=false, bool showParseTree=false, string onlyConsiderNode=null) {

			if (LogDebugMessage == null) {
				throw new YarnException ("LogDebugMessage must be set before loading");
			}

			if (LogErrorMessage == null) {
				throw new YarnException ("LogErrorMessage must be set before loading");
			}

			program = loader.Load(text, library, fileName, program, showTokens, showParseTree, onlyConsiderNode);

		}

		private VirtualMachine vm;

		// Executes a node. Use this in a for-each construct; each time you iterate over it,
		// you'll get a line, command, or set of options.
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
				visitedNodeNames.Add (vm.currentNodeName);
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
				return visitedNodeNames;
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

		// Unloads ALL nodes.
		public void UnloadAll(bool clearVisitedNodes = true) {
			if (clearVisitedNodes)
				visitedNodeNames.Clear();

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
					LogDebugMessage ("Called NodeExists, but the program hasn't been compiled yet." +
					"Nodes have been loaded, so I'm going to compile them.");

					if (program == null) {
						return false;
					}
				} else {
					LogErrorMessage ("Tried to call NodeExists, but no nodes have been compiled!");
					return false;
				}
			}
			if (program.nodes == null || program.nodes.Count == 0) {
				LogDebugMessage ("Called NodeExists, but there are zero nodes. This may be an error.");
				return false;
			}
			return program.nodes.ContainsKey(nodeName);
		}

		public void Analyse(Analysis.Context context) {

			context.AddProgramToAnalysis (this.program);

		}


		// The standard, built-in library of functions and operators.
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
