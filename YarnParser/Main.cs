using System;
using System.Collections.Generic;


namespace YarnParser
{
	class MainClass
	{

		static void ShowHelpAndExit() {
			Console.WriteLine ("YarnParser: Parses Yarn dialog files.");
			Console.WriteLine ();
			Console.WriteLine ("Usage:");
			Console.WriteLine ("YarnParser [-t] [-p] [-h] [-w] <files>");
			Console.WriteLine ("\t-t: Show the list of parsed tokens and exit.");
			Console.WriteLine ("\t-p: Show the parse tree and exit.");
			Console.WriteLine ("\t-w: After showing a line, wait for the user to press a key.");
			Console.WriteLine ("\t-h: Show this message and exit.");

			Environment.Exit (0);
		}

		public static void Main (string[] args)
		{

			if (args.Length == 0) {
				ShowHelpAndExit ();
			}
			bool showTokens = false;
			bool showParseTree = false;
			bool waitForLines = false;

			var inputFiles = new List<string> ();


			string[] allowedExtensions = {".node", ".json" };

			foreach (var arg in args) {
				switch (arg) {
				case "-t":
					showTokens = true;
					break;
				case "-p":
					showParseTree = true;
					break;
				case "-w":
					waitForLines = true;
					break;

				case "-h":
					ShowHelpAndExit ();
					break;
				default:
					var extension = System.IO.Path.GetExtension (arg);
					if (Array.IndexOf(allowedExtensions, extension) != -1) {
						inputFiles.Add (arg);
					}
					break;
				}
			}

			if (inputFiles.Count == 0) {
				Console.Error.WriteLine ("Error: No files specified.");
				Environment.Exit (1);
			}

			// TODO: use multiple files

			System.IO.StreamReader reader = new System.IO.StreamReader(inputFiles[0]);
			string inputString = reader.ReadToEnd ();
			reader.Close ();

			// Tokenise the input
			var tokenizer = new Yarn.Tokeniser();
			var tokens = tokenizer.Tokenise(inputString);

			if (showTokens) {
				// Sum up the result
				var tokenSummary = new List<string>();
				foreach (var t in tokens) {
					tokenSummary.Add(t.ToString());
				}

				var tokenSummaryString = string.Join("\n", tokenSummary);

				// Let's see what we got
				Console.WriteLine("Tokens:");
				Console.WriteLine (tokenSummaryString);
				Console.WriteLine();
			}


			// Try to parse it
			var parser = new Yarn.Parser(tokens);
			Yarn.Parser.Node tree = null;

			#if DEBUG
			tree = parser.Parse();	
			#else
			try {
				tree = parser.Parse();	
			} catch (Yarn.ParseException p) {
				Console.Error.WriteLine(string.Format("Parse error on line {0}",
					p.lineNumber
				));
				Environment.Exit (1);
			}
			#endif


			if (showParseTree) {
				// Dump the parse tree
				Console.WriteLine("Parse Tree:");
				Console.WriteLine(tree.PrintTree(0));
				Console.WriteLine ();
			}

			// Execute the parsed program
			var r = new Yarn.Runner();

			r.continuity = new SimpleContinuity ();

			// Set up the line handler
			r.RunLine += delegate(string lineText) {
				Console.WriteLine (lineText);
				if (waitForLines == true) {
					Console.Read();
				}

			};

			r.RunOptions += delegate(string[] options) {
				Console.WriteLine("Options:");
				for (int i = 0; i < options.Length; i++) {
					var optionDisplay = string.Format ("{0}. {1}", i + 1, options [i]);
					Console.WriteLine (optionDisplay);
				}
				do {
					Console.Write ("? ");
					try {
						var selectedKey = Console.ReadKey ().KeyChar.ToString();
						var selection = int.Parse (selectedKey) - 1;
						Console.WriteLine();

						if (selection > options.Length) {
							Console.WriteLine ("Invalid option.");
						} else {							
							return selection;
						}
					} catch (FormatException) {}

				} while (true);

			};

			r.NodeComplete += delegate(string nextNodeName) {
				if (nextNodeName != null) {
					Console.WriteLine("Finished; next node = " + nextNodeName);
				} else {
					Console.WriteLine("All done! :)");
				}
			};

			Console.WriteLine ("\nRUNNING THE DIALOGUE:");
			r.RunNode (tree);
		}

		// Very simple continuity class that keeps all variables in memory
		private class SimpleContinuity : Yarn.Runner.Continuity {
			#region Continuity implementation

			Dictionary<string, float> variables = new Dictionary<string, float>();

			void Yarn.Runner.Continuity.SetNumber (float number, string variableName)
			{
				variables [variableName] = number;
			}

			float Yarn.Runner.Continuity.GetNumber (string variableName)
			{
				if (variables.ContainsKey(variableName)) {
					return variables [variableName];
				} else {
					return 0.0f;
				}

			}

			#endregion
		}
	}



}
