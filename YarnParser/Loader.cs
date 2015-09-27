using System;
using System.Collections.Generic;
using Json;

namespace Yarn {

	public class Loader {

		// The final parsed nodes that were in the file we were given
		public Dictionary<string, Yarn.Parser.Node> nodes { get; private set;}

		// Prints out the list of tokens that the tokeniser found for this node
		void PrintTokenList(IEnumerable<Token> tokenList) {
			// Sum up the result
			var sb = new System.Text.StringBuilder();
			foreach (var t in tokenList) {
				sb.AppendLine (t.ToString ());
			}

			// Let's see what we got
			Console.WriteLine("Tokens:");
			Console.Write(sb.ToString());
			Console.WriteLine();
		}

		// Prints the parse tree for the node
		void PrintParseTree(Yarn.Parser.ParseNode rootNode) {
			Console.WriteLine("Parse Tree:");
			Console.WriteLine(rootNode.PrintTree(0));
			Console.WriteLine ();
		}

		// Erase the collection of nodes.
		public void Clear() {
			nodes.Clear ();
		}

		// Given a bunch of raw text, load all nodes that were inside it.
		public void Load(string text, bool showTokens = false, bool showParseTree = false) {

			if (nodes == null)
				nodes = new Dictionary<string, Parser.Node>();

			// Load the raw data and get the array of node title-text pairs
			var nodeInfos = ParseInput (text);

			foreach (NodeInfo nodeInfo in nodeInfos) {

				// Attempt to parse every node; log if we encounter any errors
				#if !DEBUG
				try {
				#endif 

					var tokeniser = new Tokeniser ();
					var tokens = tokeniser.Tokenise (nodeInfo.text);

					if (showTokens)
						PrintTokenList (tokens);

					var node = new Parser (tokens).Parse();

					nodes[nodeInfo.title] = node;
				#if !DEBUG
				} catch (Yarn.TokeniserException t) {
					Console.WriteLine (string.Format ("Error reading node {0}: {1}", nodeInfo.title, t.Message));
				} catch (Yarn.ParseException p) {
					Console.WriteLine (string.Format ("Error parsing node {0}: {1}", nodeInfo.title, p.Message));
				}
				#endif

			} 

		}

		struct NodeInfo {
			public string title;
			public string text;
			public NodeInfo(string title, string text) {
				this.title = title;
				this.text = text;
			}
		}

		// Given either Twine, JSON or XML input, return an array
		// containing info about the nodes in that file
		NodeInfo[] ParseInput(string text)
		{
			var nodes = new List<NodeInfo> ();

			if (text.IndexOf("//") == 0) {
				nodes.Add (new NodeInfo ("Node", text));
			} else {
				try {
					var loadedJSON = JsonParser.FromJson (text);

					foreach (var item in loadedJSON) {

						var list = item.Value as IList<object>;

						foreach (IDictionary<string,object> nodeJSON in list) {
							nodes.Add(new NodeInfo(nodeJSON["title"] as string, (nodeJSON["body"] as string).Replace('\r','\n')));
						}
					}


				} catch (InvalidJsonException) {}

			}

			return nodes.ToArray();
		}
	}

}