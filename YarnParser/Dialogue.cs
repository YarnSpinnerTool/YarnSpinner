using System;
using System.Collections.Generic;

namespace Yarn {

	public interface Implementation {
		void RunLine (string lineText);
		int RunOptions(string[] options);
		void RunCommand(string command);
		void DialogueComplete(string nextNodeName);

		void HandleDebugMessage (string error);
		void HandleErrorMessage (string message);

		Continuity continuity {get;}

	}

	public class Dialogue {

		public const string DEFAULT_START = "Start";

		public Loader loader;

		public Dialogue() {
			
		}

		public int LoadFile(string fileName, bool showTokens = false, bool showParseTree = false) {
			System.IO.StreamReader reader = new System.IO.StreamReader(fileName);
			string inputString = reader.ReadToEnd ();
			reader.Close ();

			return LoadString (inputString,showTokens, showParseTree);


		}

		public int LoadString(string text, bool showTokens=false, bool showParseTree=false) {

			loader = new Loader ();
			loader.Load(text, showTokens, showParseTree);

			return loader.nodes.Count;
		}

		public void RunConversation(Yarn.Implementation implementation, string startNode = DEFAULT_START) {

			var runner = new Runner (implementation);

			var nextNode = startNode;

			do {
				try {
					implementation.HandleDebugMessage("Running node " + nextNode);
					var node = loader.nodes [nextNode];
					nextNode = runner.RunNode (node);

				} catch (KeyNotFoundException) {
					implementation.HandleErrorMessage ("Can't find a node named " + nextNode);
					break;
				}
			} while (nextNode != null);

			implementation.HandleDebugMessage ("Run complete.");

		}

	}
}