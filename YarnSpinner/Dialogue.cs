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
			
		// The client should show a list of options, and call setSelectedOption before
		// asking for the next line. It's an error if you don't.
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

		// We've reached the end of this node. Used internally, and not exposed to clients.
		internal class NodeCompleteResult: RunnerResult {
			public string nextNode;

			public NodeCompleteResult (string nextNode) {
				this.nextNode = nextNode;
			}
		}


		public Logger LogDebugMessage;
		public Logger LogErrorMessage;

		public const string DEFAULT_START = "Start";

		internal Loader loader;

		public Dialogue(Yarn.VariableStorage continuity) {
			this.continuity = continuity;
		}

		public int LoadFile(string fileName, bool showTokens = false, bool showParseTree = false, string onlyConsiderNode=null) {
			System.IO.StreamReader reader = new System.IO.StreamReader(fileName);
			string inputString = reader.ReadToEnd ();
			reader.Close ();

			return LoadString (inputString, showTokens, showParseTree, onlyConsiderNode);

		}

		public int LoadString(string text, bool showTokens=false, bool showParseTree=false, string onlyConsiderNode=null) {

			loader = new Loader (this);
			loader.Load(text, showTokens, showParseTree, onlyConsiderNode);

			return loader.nodes.Count;
		}

		public IEnumerable<Yarn.Dialogue.RunnerResult> Run(string startNode = DEFAULT_START) {

			var runner = new Runner (this);

			if (LogDebugMessage == null) {
				throw new YarnException ("LogDebugMessage must be set before running");
			}

			if (LogErrorMessage == null) {
				throw new YarnException ("LogErrorMessage must be set before running");
			}

			var nextNode = startNode;

			do {

				LogDebugMessage ("Running node " + nextNode);	
				Parser.Node node;

				try {
					node = loader.nodes [nextNode];
				} catch (KeyNotFoundException) {
					LogErrorMessage ("Can't find node " + nextNode);
					yield break;
				}

				foreach (var result in runner.RunNode(node)) {
					
					if (result is Yarn.Dialogue.NodeCompleteResult) {
						var nodeComplete = result as Yarn.Dialogue.NodeCompleteResult;
						nextNode = nodeComplete.nextNode;

						// NodeComplete is not interactive, so skip immediately to next step
						// (which should end this loop)
						continue;
					} 
					yield return result;
				}

				
			} while (nextNode != null);

			LogDebugMessage ("Run complete.");

		}

	}
}