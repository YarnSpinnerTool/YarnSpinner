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
			Console.WriteLine ("YarnSpinner [-t] [-p] [-h] [-w] [-o=<node>] [-r=<run times>] [-s=<start>] [-v<argname>=<value>] <inputfile>");
			Console.WriteLine ();
			Console.WriteLine ("\t-t: Show the list of parsed tokens and exit.");
			Console.WriteLine ("\t-p: Show the parse tree and exit.");
			Console.WriteLine ("\t-w: After showing each line, wait for the user to press a key.");
			Console.WriteLine ("\t-s: Start at the given node, instead of the default ('" + Dialogue.DEFAULT_START + "').");
			Console.WriteLine ("\t-v: Sets the variable 'argname' to 'value'.");
			Console.WriteLine ("\t-o: Only consider the node named <node>.");
			Console.WriteLine ("\t-r: Run the script N times. Default is 1.");
			Console.WriteLine ("\t-d: Show debugging information.");
			Console.WriteLine ("\t-h: Show this message and exit.");
			Console.WriteLine ("\t-c: Show program bytecode and exit.");


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
			bool showDebugging = false;
			int runTimes = 1;
			bool compileToBytecodeOnly = false;

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

				// Handle 'run times' parameter
				if (arg.IndexOf("-r=") != -1) {
					var argArray = arg.Split ('=');
					if (argArray.Length != 2) {
						ShowHelpAndExit ();
					} else {
						runTimes = int.Parse (argArray [1]);
						continue;
					}

				}


				switch (arg) {
				case "-t":
					showTokens = true;
					showDebugging = true;
					break;
				case "-p":
					showParseTree = true;
					showDebugging = true;
					break;
				case "-w":
					waitForLines = true;
					break;
				case "-d":
					showDebugging = true;
					break;
				case "-c":
					compileToBytecodeOnly = true;
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


			// Add some methods for testing
			dialogue.library.RegisterFunction ("add_three_operands", 3, delegate(Value[] parameters) {
				var f1 = parameters[0].AsNumber;
				var f2 = parameters[1].AsNumber;
				var f3 = parameters[2].AsNumber;

				return f1+f2+f3;
			});

			dialogue.library.RegisterFunction ("last_value", -1, delegate(Value[] parameters) {
				// return the last value
				return parameters[parameters.Length-1];
			});

			dialogue.library.RegisterFunction ("is_even", 1, delegate(Value[] parameters) {
				return (int)parameters[0].AsNumber % 2 == 0;
			});

			// Register the "assert" function, which stops execution if its parameter evaluates to false
			dialogue.library.RegisterFunction ("assert", 1, delegate(Value[] parameters) {
				if (parameters[0].AsBool == false) {

					// TODO: Include file, node and line number
					dialogue.LogErrorMessage("ASSERTION FAILED");
					Environment.Exit(1);
				}
			});


			// Register a function to let test scripts register how many
			// options they expect to send
			dialogue.library.RegisterFunction ("prepare_for_options", 2, delegate(Value[] parameters) {
				impl.numberOfExpectedOptions = (int)parameters [0].AsNumber;
				impl.autoSelectOptionNumber = (int)parameters[1].AsNumber;
			});

			dialogue.library.RegisterFunction ("expect_line", 1, delegate(Value[] parameters) {
				impl.expectedNextLine = parameters[0].AsString;
			});

			dialogue.library.RegisterFunction ("expect_command", 1, delegate(Value[] parameters) {
				impl.expectedNextCommand = parameters[0].AsString;
			});

			// If debugging is enabled, log debug messages; otherwise, ignore them
			if (showDebugging) {
				dialogue.LogDebugMessage = delegate(string message) {
					Console.WriteLine ("Debug: " + message);
				};
			} else {
				dialogue.LogDebugMessage = delegate(string message) {};
			}

			dialogue.LogErrorMessage = delegate(string message) {
				Console.WriteLine ("ERROR: " + message);
			};

			dialogue.LoadFile (inputFiles [0],showTokens, showParseTree, onlyConsiderNode);

			if (compileToBytecodeOnly) {
				var result = dialogue.Compile ();
				Console.WriteLine (result);
			}

			// Only run the program when we're not emitting debug output of some kind
			var runProgram = 
				showTokens == false &&
				showParseTree == false &&
				compileToBytecodeOnly == false;

			if (runProgram) {
				// Run the conversation

				for (int run = 0; run < runTimes; run++) {
					foreach (var step in dialogue.Run (startNode)) {

						// It can be one of three types: a line to show, options
						// to present to the user, or an internal command to run

						if (step is Dialogue.LineResult) {
							var lineResult = step as Dialogue.LineResult;
							impl.RunLine (lineResult.line);
						} else if (step is Dialogue.OptionSetResult) {
							var optionsResult = step as Dialogue.OptionSetResult;
							impl.RunOptions (optionsResult.options, optionsResult.setSelectedOptionDelegate);
						} else if (step is Dialogue.CommandResult) {
							var commandResult = step as Dialogue.CommandResult;
							impl.RunCommand (commandResult.command.text);
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

			// The number of options we expect to see when we next
			// receive options. -1 means "don't care"
			public int numberOfExpectedOptions = -1;

			// The index of the option to automatically select, starting from 0.
			// -1 means "do not automatically select an option".
			public int autoSelectOptionNumber = -1;

			public string expectedNextLine = null;

			public string expectedNextCommand = null;

			public ConsoleRunnerImplementation(bool waitForLines = false) {
				this.variableStore = new MemoryVariableStore();
				this.waitForLines = waitForLines;
			}

			public void RunLine (Yarn.Line lineText)
			{

				if (expectedNextLine != null && expectedNextLine != lineText.text) {
					// TODO: Output diagnostic info here
					Console.WriteLine(string.Format("Unexpected line.\nExpected: {0}\nReceived: {1}", 
						expectedNextLine, lineText.text));
					Environment.Exit (1);
				}

				expectedNextLine = null;

				Console.WriteLine (lineText.text);
				if (waitForLines == true) {
					Console.Read();
				}
			}

			public void RunOptions (Options optionsGroup, OptionChooser optionChooser)
			{

				// Check to see if the number of expected options
				// is what we're expecting to see
				if (numberOfExpectedOptions != -1 &&
					optionsGroup.options.Count != numberOfExpectedOptions) {
					// TODO: Output diagnostic info here
					Console.WriteLine (string.Format("ERROR: Expected {0} options, but received {1}", numberOfExpectedOptions, optionsGroup.options.Count));
					Console.WriteLine ("Received options were:");
					foreach (string option in optionsGroup.options) {
						Console.WriteLine (" - " + option);
					}
					Environment.Exit (1);
				}

				// If we were told to automatically select an option, do so
				if (autoSelectOptionNumber != -1) {
					optionChooser (autoSelectOptionNumber);

					autoSelectOptionNumber = -1;

					return;

				}

				// Reset the expected options counter
				numberOfExpectedOptions = -1;


				Console.WriteLine("Options:");
				for (int i = 0; i < optionsGroup.options.Count; i++) {
					var optionDisplay = string.Format ("{0}. {1}", i + 1, optionsGroup.options [i]);
					Console.WriteLine (optionDisplay);
				}
				do {
					Console.Write ("? ");
					try {
						var selectedKey = Console.ReadKey ().KeyChar.ToString();
						var selection = int.Parse (selectedKey) - 1;
						Console.WriteLine();

						if (selection > optionsGroup.options.Count) {
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

				if (expectedNextCommand != null && expectedNextCommand != command) {
					// TODO: Output diagnostic info here
					Console.WriteLine(string.Format("Unexpected line.\nExpected: {0}\nReceived: {1}", 
						expectedNextCommand, command));
					Environment.Exit (1);
				}

				Console.WriteLine("Command: <<"+command+">>");
			}

			public void DialogueComplete ()
			{
				// All done
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

