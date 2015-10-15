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
using System.Collections.Generic;
using Json;

namespace Yarn {

	internal class Loader {

		private Dialogue dialogue;

		// The final parsed nodes that were in the file we were given
		internal Dictionary<string, Yarn.Parser.Node> nodes { get; private set;}

		// Prints out the list of tokens that the tokeniser found for this node
		void PrintTokenList(IEnumerable<Token> tokenList) {
			// Sum up the result
			var sb = new System.Text.StringBuilder();
			foreach (var t in tokenList) {
				sb.AppendLine (string.Format("{0} ({1} line {2})", t.ToString (), t.context, t.lineNumber));
			}

			// Let's see what we got
			dialogue.LogDebugMessage("Tokens:");
			dialogue.LogDebugMessage(sb.ToString());

		}

		// Prints the parse tree for the node
		void PrintParseTree(Yarn.Parser.ParseNode rootNode) {
			dialogue.LogDebugMessage("Parse Tree:");
			dialogue.LogDebugMessage(rootNode.PrintTree(0));

		}

		// Prepares a loader. 'implementation' is used for logging.
		public Loader(Dialogue dialogue) {
			this.dialogue = dialogue;
			nodes = new Dictionary<string, Parser.Node>();
		}

		// Erase the collection of nodes.
		public void Clear() {
			nodes.Clear ();
		}

		// Given a bunch of raw text, load all nodes that were inside it.
		// You can call this multiple times to append to the collection of nodes,
		// but note that new nodes will replace older ones with the same name.
		public void Load(string text, Library library, bool showTokens = false, bool showParseTree = false, string onlyConsiderNode=null) {
			
			// Load the raw data and get the array of node title-text pairs
			var nodeInfos = ParseInput (text);

			foreach (NodeInfo nodeInfo in nodeInfos) {

				if (onlyConsiderNode != null && nodeInfo.title != onlyConsiderNode)
					continue;

				// Attempt to parse every node; log if we encounter any errors
				#if !DEBUG
				// If this is a release build, don't crash on parse errors
				try {
				#endif 

					if (nodes.ContainsKey(nodeInfo.title)) {
						throw new InvalidOperationException("Attempted to load a node called "+
							nodeInfo.title+", but a node with that name has already been loaded!");
					}

					var tokeniser = new Tokeniser ();
					var tokens = tokeniser.Tokenise (nodeInfo.title, nodeInfo.text);

					if (showTokens)
						PrintTokenList (tokens);

					var node = new Parser (tokens, library).Parse();

					nodes[nodeInfo.title] = node;
				#if !DEBUG
				} catch (Yarn.TokeniserException t) {
					this.dialogue.LogErrorMessage (string.Format ("Error reading node {0}: {1}", nodeInfo.title, t.Message));
				} catch (Yarn.ParseException p) {
					this.dialogue.LogErrorMessage (string.Format ("Error parsing node {0}: {1}", nodeInfo.title, p.Message));
				} catch (InvalidOperationException e) {
					this.dialogue.LogErrorMessage(string.Format("Error loading node: {0}", e.Message));
				}
				#endif

			} 

		}

		// The raw text of the Yarn node, plus metadata
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
			// All the nodes we found in this file
			var nodes = new List<NodeInfo> ();

			if (text.IndexOf("//") == 0) {
				// If it starts with a comment, treat it as a single-node file
				nodes.Add (new NodeInfo ("Start", text));
			} else {
				// Blindly assume it's JSON! \:D/
				try {

					// First, parse the raw text
					var loadedJSON = JsonParser.FromJson (text);

					// Process each item that was found (probably just a single one)
					foreach (var item in loadedJSON) {

						// We expect it to be an array of dictionaries
						var list = item.Value as IList<object>;

						// For each dictionary in the list..
						foreach (IDictionary<string,object> nodeJSON in list) {

							// Pull out the node's title and body, and use that
							nodes.Add(
								new NodeInfo(
									nodeJSON["title"] as string, 
									nodeJSON["body"] as string
								)
							);
						}
					}

				} catch (InvalidCastException) {
					dialogue.LogErrorMessage ("Error parsing Yarn input: it's valid JSON, but " +
						"it didn't match the data layout I was expecting.");
				} catch (InvalidJsonException e) {
					dialogue.LogErrorMessage ("Error parsing Yarn input: " + e.Message);
				}
			}

			// hooray we're done
			return nodes.ToArray();
		}
	}

}