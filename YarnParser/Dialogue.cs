using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn {

	public delegate void OptionChooser (int selectedOptionIndex);

	public interface Implementation {
		void RunLine (string lineText);
		void RunOptions(IList<string> options, OptionChooser optionChooser);
		void RunCommand(string command);
		void DialogueComplete();

		void HandleDebugMessage (string error);
		void HandleErrorMessage (string message);

		Continuity continuity {get;}

	}

	public class Dialogue {

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


		public const string DEFAULT_START = "Start";

		internal Loader loader;

		private Implementation implementation;

		public Dialogue(Yarn.Implementation implementation) {
			this.implementation = implementation;
		}

		public int LoadFile(string fileName, bool showTokens = false, bool showParseTree = false, string onlyConsiderNode=null) {
			System.IO.StreamReader reader = new System.IO.StreamReader(fileName);
			string inputString = reader.ReadToEnd ();
			reader.Close ();

			return LoadString (inputString, showTokens, showParseTree, onlyConsiderNode);

		}

		public int LoadString(string text, bool showTokens=false, bool showParseTree=false, string onlyConsiderNode=null) {

			loader = new Loader (implementation);
			loader.Load(text, showTokens, showParseTree, onlyConsiderNode);

			return loader.nodes.Count;
		}

		public IEnumerable<Yarn.Dialogue.RunnerResult> RunConversation(string startNode = DEFAULT_START) {

			var runner = new Runner (implementation);

			var nextNode = startNode;

			do {

				implementation.HandleDebugMessage ("Running node " + nextNode);	
				Parser.Node node;

				try {
					node = loader.nodes [nextNode];
				} catch (KeyNotFoundException) {
					implementation.HandleErrorMessage ("Can't find node " + nextNode);
					yield break;
				}

				foreach (var result in runner.RunNode(node)) {
					
					if (result is Yarn.Dialogue.NodeCompleteResult) {
						var nodeComplete = result as Yarn.Dialogue.NodeCompleteResult;
						nextNode = nodeComplete.nextNode;

						// NodeComplete is not interactive, so skip immediately to next step
						continue;
					} else if (result is Yarn.Dialogue.LineResult) {
						var line = result as Yarn.Dialogue.LineResult;
						implementation.RunLine (line.text);
					} else if (result is Yarn.Dialogue.CommandResult) {
						var command = result as Yarn.Dialogue.CommandResult;
						implementation.RunCommand (command.command);
					} else if (result is Yarn.Dialogue.OptionSetResult) {
						var options = result as Yarn.Dialogue.OptionSetResult;
						implementation.RunOptions (options.options, options.chooseResult);
					}
					yield return result;
				}

				
			} while (nextNode != null);

			implementation.HandleDebugMessage ("Run complete.");

			implementation.DialogueComplete ();

		}

	}
}