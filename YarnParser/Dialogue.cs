using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn {

	public  class YarnException : Exception {
		public YarnException(string message) : base(message) {}
	}

	public delegate void OptionChooser (int selectedOptionIndex);
	public delegate void Logger(string message);


	public class Dialogue  {

		internal Continuity continuity;

		public class RunnerResult { }

		public class LineResult : RunnerResult  {
			public string text;

			public LineResult (string text) {
				this.text = text;
			}

		}

		public class CommandResult: RunnerResult {
			public string command;

			public CommandResult (string command) {
				this.command = command;
			}

		}

		public class NodeCompleteResult: RunnerResult {
			public string nextNode;

			public NodeCompleteResult (string nextNode) {
				this.nextNode = nextNode;
			}
		}

		public class OptionSetResult : RunnerResult {
			public IList<string> options;
			public OptionChooser chooseResult;

			public OptionSetResult (IList<string> options, OptionChooser chooseResult) {
				this.options = options;
				this.chooseResult = chooseResult;
			}

		}

		public Logger LogDebugMessage;
		public Logger LogErrorMessage;

		public const string DEFAULT_START = "Start";

		internal Loader loader;

		public Dialogue(Yarn.Continuity continuity) {
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
						continue;
					} 
					yield return result;
				}

				
			} while (nextNode != null);

			LogDebugMessage ("Run complete.");

		}

	}
}