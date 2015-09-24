using System;
using System.Collections.Generic;

namespace YarnParser
{
	class MainClass
	{
		public static void Main (string[] args)
		{

			String[] complexInputLines = {
			                        "Mae: This is a test",
			                        "Molly: Pretty cool!",
			                        "<<set $foo to 2>>",
									"<<set $bar to 3.0>>",
									"    Mae: Yeah well this is indented",
									"Molly: This isn't!",
									"Mae: Pretty cool. Hey, here's punctuation.",

			};

			String[] simpleInputLines = {
				"this is       cool",
				"yes  it  is"
			};

			String[] indentTestLines = {
				"this line is at indent 0",
				"so is this",
				"    this line is at indent 1",
				"    woo this is at 1 also",
				"        we need to go deeper", 
				"back to indent 0"				
			};

			String[] veryBasicExpressions = {
				
				"whoa what here's some text",
				"<<set $foo to 1>>",
				"",
				"<<if $foo is 1>>",
				"    this should appear :)",
				"    <<if 1 is 1>>",
				"        NESTED IF BLOCK WHAAAT",
				"        <<set $foo += 47 + 6>>",
				"    <<endif>>",
				"<<else>>",
				"    oh noooo it didn't work :(",
				"<<endif>>",
				"",
				"<<if $foo is 54>>",
				"    haha nice now 'set' works even when deeply nested",
				"<<else>>",
				"    aaargh :(",
				"<<endif>>"
			};

			String[] nestedIfs = {
				"<<if 1 == 1>>",
				"<<if 2 == 2>>",
				"Whoa",
				"<<endif>>",
				"<<if 2 == 3>>",
				"Whoa no :(",
				"<<endif>>",
				"<<endif>>"
			};

			String[] basicMathLines = {
				"1 + 2 / 56 - 123 * 2" 
			};


			String[] veryComplicatedTestLines = {
				"Testing some stuff...",
				"",
				"<<if $something_not_one is 1>>",
				"    First if statement went wrong.",
				"De-denting??",
				"<<else>>",
				"    First if statement went right! :)",
				"<<endif>>",
				"",
				"<<if $something_not_one is 0>>",
				"    Second if statement went right! :)",
				"<<else>>",
				"    Second if statement went wrong. :(",
				"<<endif>>",
				"[[Go to branch 1|Branch1]]",
				"[[Go to branch 2|Branch2]]",
			};

			String[] veryComplicatedTestLinesWithShortcutOptions = {
				"Testing some stuff...",
				"",
				"<<if $something_not_one is 1>>",
				"    First if statement went wrong.",
				"<<else>>",
				"    First if statement went right! :)",
				"<<endif>>",
				"",
				"<<if $something_not_one is 0>>",
				"    Second if statement went right! :)",
				"<<else>>",
				"    Second if statement went wrong. :(",
				"<<endif>>",
				"",
				"Ready for some options?",
				"-> This is an option. The next one should say: Yay it worked!",
				"-> This option shouldn't show up <<if $available is 1>>",
				"-> Yay it worked! Select this option for sub-options.",
				"    -> This is nested option 1",
				"        Oh yes, this me option 1.",
				"    -> This is nested option 2",
				"        Oh man, this me option 2.",
				"Now you should see two branches.",
				"[[Go to branch 1|Branch1]]",
				"[[Go to branch 2|Branch2]]",
			};



			//String[] linesToUse = simpleInputLines;
			//String[] linesToUse = complexInputLines;
			//String[] linesToUse = indentTestLines;
			//String[] linesToUse = veryComplicatedTestLinesWithShortcutOptions;
			//String[] linesToUse = veryComplicatedTestLines;
			//String[] linesToUse = basicMathLines;
			String[] linesToUse = veryBasicExpressions;
			//String[] linesToUse = nestedIfs;

			// Merge the test array into a big ol string
			String inputString = string.Join("\n",linesToUse);

			// Start building the tokenizer
			var tokenizer = new Yarn.Tokeniser();

			Console.WriteLine ("Yarn Input:");
			Console.WriteLine (inputString);


			// Tokenise the input
			var tokens = tokenizer.Tokenise(inputString);

			/*
			// Sum up the result
			var tokenSummary = new List<string>();
			foreach (var t in tokens) {
				tokenSummary.Add(t.ToString());
			}

			var tokenSummaryString = string.Join("\n", tokenSummary);

			// Let's see what we got
			Console.WriteLine("Tokens:\n{0}", tokenSummaryString);*/

			// Try to parse it
			var parser = new Yarn.Parser(tokens);

			var tree = parser.Parse();

			// Dump the parse tree
//			Console.WriteLine();
//			Console.WriteLine("Parse Tree:");
//			Console.WriteLine(tree.PrintTree(0));

			// Execute the parsed program
			var r = new Yarn.Runner();

			r.continuity = new SimpleContinuity ();

			// Set up the line handler
			r.RunLine += delegate(string lineText) {
				Console.WriteLine (lineText);
				Console.Read();
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
					} catch (FormatException e) {}

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
