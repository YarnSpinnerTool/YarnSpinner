using System;
using System.Collections;
using System.Collections.Generic;

namespace Yarn {

	public interface Implementation {
		void RunLine (string lineText);
		void RunOptions(IList<string> options, Runner.OptionChooser optionChooser);
		void RunCommand(string command);
		void DialogueComplete(string nextNodeName);

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

		public void RunConversation(string startNode = DEFAULT_START) {

			foreach (var result in IterateConversation(startNode)) {
				if (result is Yarn.Runner.YarnLine) {
					var line = result as Yarn.Runner.YarnLine;
					implementation.RunLine (line.text);
				} else if (result is Yarn.Runner.YarnCommand) {
					var command = result as Yarn.Runner.YarnCommand;
					implementation.RunCommand (command.command);
				} else if (result is Yarn.Runner.YarnOptionSet) {
					var options = result as Yarn.Runner.YarnOptionSet;
					implementation.RunOptions (options.options, options.chooseResult);
				}
			}

		}

		public IEnumerable<Yarn.Runner.YarnResult> IterateConversation(string startNode = DEFAULT_START) {

			var runner = new Runner (implementation);

			var nextNode = startNode;

			do {

				implementation.HandleDebugMessage ("Running node " + nextNode);	
				Parser.Node node;

				try {
					node = loader.nodes [nextNode];
				} catch (KeyNotFoundException) {
					implementation.HandleErrorMessage ("Can't find a node named " + nextNode);
					yield break;
				}

				foreach (var result in runner.RunNode(node)) {
					
					if (result is Yarn.Runner.YarnNodeComplete) {
						var nodeComplete = result as Yarn.Runner.YarnNodeComplete;
						nextNode = nodeComplete.nextNode;
					} else {
						yield return result;
					}
				}

				
			} while (nextNode != null);

			implementation.HandleDebugMessage ("Run complete.");

		}

	}
}