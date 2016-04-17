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

// Comment out to not catch exceptions
#define CATCH_EXCEPTIONS 

using System;
using System.Collections.Generic;
using Json;


namespace Yarn {

	internal class Loader {

		private Dialogue dialogue;

		public Program program { get; private set; }

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
			if (dialogue == null)
				throw new ArgumentNullException ("dialogue");
			
			this.dialogue = dialogue;

		}

		// Given a bunch of raw text, load all nodes that were inside it.
		// You can call this multiple times to append to the collection of nodes,
		// but note that new nodes will replace older ones with the same name.
		// Returns the number of nodes that were loaded.
		public Program Load(string text, Library library, string fileName, Program includeProgram, bool showTokens, bool showParseTree, string onlyConsiderNode) {

			// The final parsed nodes that were in the file we were given
			Dictionary<string, Yarn.Parser.Node> nodes = new Dictionary<string, Parser.Node>();

			// Load the raw data and get the array of node title-text pairs
			var nodeInfos = ParseInput (text);

			int nodesLoaded = 0;

			foreach (NodeInfo nodeInfo in nodeInfos) {

				if (onlyConsiderNode != null && nodeInfo.title != onlyConsiderNode)
					continue;

				// Attempt to parse every node; log if we encounter any errors
				#if CATCH_EXCEPTIONS
				try {
				#endif 
					
					if (nodes.ContainsKey(nodeInfo.title)) {
						throw new InvalidOperationException("Attempted to load a node called "+
							nodeInfo.title+", but a node with that name has already been loaded!");
					}

					var lexer = new Lexer ();
					var tokens = lexer.Tokenise (nodeInfo.title, nodeInfo.text);

					if (showTokens)
						PrintTokenList (tokens);

					var node = new Parser (tokens, library).Parse();

					// If this node is tagged "rawText", then preserve its source
					if (string.IsNullOrEmpty(nodeInfo.tags) == false && 
						nodeInfo.tags.Contains("rawText")) {
						node.source = nodeInfo.text;
					}

					node.name = nodeInfo.title;

					if (showParseTree)
						PrintParseTree(node);

					nodes[nodeInfo.title] = node;

					nodesLoaded++;

				#if CATCH_EXCEPTIONS
				} catch (Yarn.TokeniserException t) {
					// Add file information
					var message = string.Format ("In file {0}: Error reading node {1}: {2}", fileName, nodeInfo.title, t.Message);
					throw new Yarn.TokeniserException (message);
				} catch (Yarn.ParseException p) {
					var message = string.Format ("In file {0}: Error parsing node {1}: {2}", fileName, nodeInfo.title, p.Message);
					throw new Yarn.ParseException (message);
				} catch (InvalidOperationException e) {
					var message = string.Format ("In file {0}: Error reading node {1}: {2}", fileName, nodeInfo.title, e.Message);
					throw new InvalidOperationException (message);
				}
				#endif 


			}

			var compiler = new Yarn.Compiler(fileName);

			foreach (var node in nodes) {
				compiler.CompileNode (node.Value);
			}

			if (includeProgram != null) {
				compiler.program.Include (includeProgram);
			}

			return compiler.program;

		}

		// The raw text of the Yarn node, plus metadata
		struct NodeInfo {
			public string title;
			public string text;
			public string tags;
			public NodeInfo(string title, string text, string tags) {
				this.title = title;
				this.text = text;
				this.tags = tags;
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
				nodes.Add (new NodeInfo ("Start", text, null));
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
									nodeJSON["body"] as string,
									nodeJSON["tags"] as string
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