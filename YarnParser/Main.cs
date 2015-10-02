using System;
using System.Collections;
using System.Collections.Generic;


namespace Yarn
{

	class YarnSpinnerConsole
	{

		static void ShowHelpAndExit() {
			Console.WriteLine ("YarnSpinner: Executes Yarn dialog files.");
			Console.WriteLine ();
			Console.WriteLine ("Usage:");
			Console.WriteLine ("YarnSpinner [-t] [-p] [-h] [-w] [-o=node] [-s=<start>] [-v<argname>=<value>] <inputfile>");
			Console.WriteLine ();
			Console.WriteLine ("\t-t: Show the list of parsed tokens and exit.");
			Console.WriteLine ("\t-p: Show the parse tree and exit.");
			Console.WriteLine ("\t-w: After showing each line, wait for the user to press a key.");
			Console.WriteLine ("\t-s: Start at the given node, instead of the default of '" + Dialogue.DEFAULT_START + "'.");
			Console.WriteLine ("\t-v: Sets the variable 'argname' to 'value'.");
			Console.WriteLine ("\t-o: Only consider node <node>.");
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
			string onlyConsiderNode = null;

			var inputFiles = new List<string> ();
			string startNode = Dialogue.DEFAULT_START;

			string[] allowedExtensions = {".node", ".json" };

			var defaultVariables = new Dictionary<string,float> ();

			foreach (var arg in args) {

				// Handle 'start' parameter
				if (arg.IndexOf("-s=") != -1) {
					var startArray = arg.Split (new char[]{ '=' });
					if (startArray.Length != 2) {
						ShowHelpAndExit ();
					} else {
						startNode = startArray [1];
						continue;
					}
				}

				// Handle variable input
				if (arg.IndexOf("-v") != -1) {
					var variable = arg.Substring (2);
					var variableArray = variable.Split (new char[]{ '=' });
					if (variableArray.Length != 2) {
						ShowHelpAndExit ();
					} else {
						var varName = "$" + variableArray [0];
						var varValue = float.Parse (variableArray [1]);
						defaultVariables [varName] = varValue;
						continue;
					}

				}

				// Handle 'only this node' parameter
				if (arg.IndexOf("-o=") != -1) {
					var startArray = arg.Split (new char[]{ '=' });
					if (startArray.Length != 2) {
						ShowHelpAndExit ();
					} else {
						onlyConsiderNode = startArray [1];
						continue;
					}
				}

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

					// only allow one file
					if (inputFiles.Count > 0) {
						Console.Error.WriteLine ("Error: Too many files specified.");
						Environment.Exit (1);
					}

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

			// Create the object that handles callbacks
			var impl = new ConsoleRunnerImplementation (waitForLines:waitForLines);

			// load the default variables we got on the command line
			foreach (var variable in defaultVariables) {
				
				impl.SetNumber (variable.Key, variable.Value);
			}

			// Load nodes
			var dialogue = new Dialogue(impl);
			dialogue.LoadFile (inputFiles [0],showTokens, showParseTree, onlyConsiderNode);
			dialogue.LogDebugMessage = delegate(string message) {
				Console.WriteLine ("Debug: " + message);
			};
			dialogue.LogErrorMessage = delegate(string message) {
				Console.WriteLine ("ERROR: " + message);
			};

			for (var count = 1; count <= 2; count++) {
				if (showTokens == false && showParseTree == false) {
					// Run the conversation
					foreach (var step in dialogue.Run (startNode)) {

						if (step is Dialogue.LineResult) {
							var line = step as Dialogue.LineResult;
							impl.RunLine (line.text);
						} else if (step is Dialogue.OptionSetResult) {
							var optionSet = step as Dialogue.OptionSetResult;
							impl.RunOptions (optionSet.options, optionSet.setSelectedOptionDelegate);
						} else if (step is Dialogue.CommandResult) {
							var command = step as Dialogue.CommandResult;
							impl.RunCommand (command.command);
						}
					}
					impl.DialogueComplete ();
				}
			}

		}

		// A simple Implementation for the command line.
		private class ConsoleRunnerImplementation : Yarn.VariableStorage {

			private bool waitForLines = false;

			Yarn.MemoryVariableStore variableStore;

			public ConsoleRunnerImplementation(bool waitForLines = false) {
				this.variableStore = new MemoryVariableStore();
				this.waitForLines = waitForLines;
			}

			public void RunLine (string lineText)
			{
				Console.WriteLine (lineText);
				if (waitForLines == true) {
					Console.Read();
				}
			}

			public void RunOptions (IList<string> options, OptionChooser optionChooser)
			{
				Console.WriteLine("Options:");
				for (int i = 0; i < options.Count; i++) {
					var optionDisplay = string.Format ("{0}. {1}", i + 1, options [i]);
					Console.WriteLine (optionDisplay);
				}
				do {
					Console.Write ("? ");
					try {
						var selectedKey = Console.ReadKey ().KeyChar.ToString();
						var selection = int.Parse (selectedKey) - 1;
						Console.WriteLine();

						if (selection > options.Count) {
							Console.WriteLine ("Invalid option.");
						} else {							
							optionChooser(selection);
							break;
						}
					} catch (FormatException) {}

				} while (true);
			}

			public void RunCommand (string command)
			{
				Console.WriteLine("Command: <<"+command+">>");
			}

			public void DialogueComplete ()
			{
				Console.WriteLine("Conversation complete.");
			}

			public void HandleErrorMessage (string error)
			{
				Console.WriteLine("Error: " + error);
			}
			
			public void HandleDebugMessage (string message)
			{
				Console.WriteLine("Debug: " + message);
			}

			public void SetNumber (string variableName, float number)
			{				
				variableStore.SetNumber(variableName, number);
			}

			public float GetNumber (string variableName)
			{
				return variableStore.GetNumber(variableName);
			}

			public void Clear()
			{
				variableStore.Clear();
			}
		}


	}
}
