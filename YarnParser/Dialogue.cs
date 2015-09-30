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



		public const string DEFAULT_START = "Start";

		public Loader loader;

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

		public IEnumerable<Yarn.Runner.RunnerResult> RunConversation(string startNode = DEFAULT_START) {

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
					
					if (result is Yarn.Runner.NodeCompleteResult) {
						var nodeComplete = result as Yarn.Runner.NodeCompleteResult;
						nextNode = nodeComplete.nextNode;

						// NodeComplete is not interactive, so skip immediately to next step
						continue;
					} else if (result is Yarn.Runner.LineResult) {
						var line = result as Yarn.Runner.LineResult;
						implementation.RunLine (line.text);
					} else if (result is Yarn.Runner.CommandResult) {
						var command = result as Yarn.Runner.CommandResult;
						implementation.RunCommand (command.command);
					} else if (result is Yarn.Runner.OptionSetResult) {
						var options = result as Yarn.Runner.OptionSetResult;
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