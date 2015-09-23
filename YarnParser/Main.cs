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
									"    Molly: This isn't!",
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
				"    and this is at 1 also",
				"        we need to go deeper", 
				"back to indent 0"				
			};

			String[] veryBasicExpressions = {
				"1",
				"2 + 1",
				"3",
				"4"
			};

			String[] basicMathLines = {
				"1 + 2 / 56 - 123 * 2" 
			};

			String[] veryComplicatedTestLines = {
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
			//String[] linesToUse = veryComplicatedTestLines;
			//String[] linesToUse = basicMathLines;
			String[] linesToUse = veryBasicExpressions;

			// Merge the test array into a big ol string
			String inputString = string.Join("\n",linesToUse);

			// Start building the tokenizer
			var tokenizer = new Yarn.Tokeniser();


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
			Console.WriteLine("Input:\n{0}", inputString);
			Console.WriteLine("Tokens:\n{0}", tokenSummaryString);
			*/
			// Try to parse it
			var parser = new Yarn.Parser(tokens);

			var tree = parser.Parse();

			// Dump the parse tree
			Console.WriteLine(tree.ToString());
		}
	}
}
