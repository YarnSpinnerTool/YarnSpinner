﻿/*

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
			Console.WriteLine ("\t-V: Verifies the provided script");
			Console.WriteLine ("\t-o: Only consider the node named <node>.");
			Console.WriteLine ("\t-r: Run the script N times. Default is 1.");
			Console.WriteLine ("\t-d: Show debugging information.");
			Console.WriteLine ("\t-c: Show program bytecode and exit.");
			Console.WriteLine ("\t-f: Write program bytecode to file and exit.");
			Console.WriteLine ("\t-a: Show analysis of the program and exit.");
			Console.WriteLine ("\t-1: Automatically select the the first option when presented with options.");
			Console.WriteLine ("\t-j: Dump parse tree to pure JSON and exit.");
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
			bool showDebugging = false;
			int runTimes = 1;
			bool compileToBytecodeOnly = false;
			bool verifyOnly = false;
			bool autoSelectFirstOption = false;
			bool analyseOnly = false;
			bool showJson = false;
			string outputFile = null;

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

				// Handle 'output file' parameter
				if (arg.IndexOf("-f=") != -1) {
					var argArray = arg.Split('=');
					if (argArray.Length != 2) {
						ShowHelpAndExit();
					} else {
						outputFile = argArray[1];
						continue;
					}
				}

				switch (arg) {
				case "-V":
					verifyOnly = true;
					break;
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
				case "-1":
					autoSelectFirstOption = true;
					break;
				case "-h":
					ShowHelpAndExit ();
					break;
				case "-a":
					analyseOnly = true;
					break;
				case "-j":
					showJson = true;
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




			// If debugging is enabled, log debug messages; otherwise, ignore them
			Logger debugLogger =  delegate(string message) {};
			if (showDebugging) {
				debugLogger = delegate(string message) {
					Console.WriteLine ("Debug: " + message);
				};
			}

			Logger errorLogger = delegate(string message) {
				Console.WriteLine ("ERROR: " + message);
			};


			// Load nodes
			var dialogue = new Dialogue(impl, debugLogger, errorLogger);

			// Add some methods for testing
			dialogue.library.RegisterFunction ("add_three_operands", 3, delegate(Value[] parameters) {
				return parameters[0]+parameters[1]+parameters[2];
			});

			dialogue.library.RegisterFunction ("last_value", -1, delegate(Value[] parameters) {
				// return the last value
				return parameters[parameters.Length-1];
			});

			dialogue.library.RegisterFunction ("is_even", 1, delegate(Value[] parameters) {
				return (int)parameters[0].AsNumber % 2 == 0;
			});

			// Register the "assert" function, which stops execution if its parameter evaluates to false
			dialogue.library.RegisterFunction ("assert", -1, delegate(Value[] parameters) {
				if (parameters[0].AsBool == false) {

					// TODO: Include file, node and line number
					if( parameters.Length > 1 && parameters[1].AsBool ) {
						dialogue.LogErrorMessage ("ASSERTION FAILED: " + parameters[1].AsString);
					} else {
						dialogue.LogErrorMessage ("ASSERTION FAILED");
					}
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

			if (autoSelectFirstOption == true) {
				impl.autoSelectFirstOption = true;
			}

			if (verifyOnly) {
				try {
					dialogue.LoadFile (inputFiles [0],showTokens, showParseTree, showJson, onlyConsiderNode);
				} catch (Exception e) {
					Console.WriteLine ("Error: " + e.Message);
				}
				return;
			}

			dialogue.LoadFile (inputFiles [0],showTokens, showParseTree, showJson, onlyConsiderNode);

			if (outputFile != null) {
				var result = dialogue.GetByteCode();
				System.Text.UTF8Encoding utf8 = new System.Text.UTF8Encoding();
				using (System.IO.StreamWriter file = new System.IO.StreamWriter(outputFile, false, utf8)) {
					file.Write(result);
				}
				return;
			}

			if (compileToBytecodeOnly) {
				var result = dialogue.GetByteCode ();
				Console.WriteLine (result);
				return;
			}

			if (analyseOnly) {

				var context = new Yarn.Analysis.Context ();

				dialogue.Analyse (context);

				foreach (var diagnosis in context.FinishAnalysis()) {
					Console.WriteLine (diagnosis.ToString(showSeverity:true));
				}
				return;
			}

			// Only run the program when we're not emitting debug output of some kind
			var runProgram =
				showTokens == false &&
				showParseTree == false &&
				compileToBytecodeOnly == false &&
				showJson == false &&
				analyseOnly == false;

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

			public bool autoSelectFirstOption = false;

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

				Console.WriteLine("Options:");
				for (int i = 0; i < optionsGroup.options.Count; i++) {
					var optionDisplay = string.Format ("{0}. {1}", i + 1, optionsGroup.options [i]);
					Console.WriteLine (optionDisplay);
				}


				// Check to see if the number of expected options
				// is what we're expecting to see
				if (numberOfExpectedOptions != -1 &&
					optionsGroup.options.Count != numberOfExpectedOptions) {
					// TODO: Output diagnostic info here
					Console.WriteLine (string.Format("[ERROR: Expected {0} options, but received {1}]", numberOfExpectedOptions, optionsGroup.options.Count));
					Environment.Exit (1);
				}

				// If we were told to automatically select an option, do so
				if (autoSelectOptionNumber != -1) {
					Console.WriteLine ("[Received {0} options, choosing option {1}]", optionsGroup.options.Count, autoSelectOptionNumber);

					optionChooser (autoSelectOptionNumber);

					autoSelectOptionNumber = -1;

					return;

				}

				// Reset the expected options counter
				numberOfExpectedOptions = -1;



				if (autoSelectFirstOption == true) {
					Console.WriteLine ("[automatically choosing option 1]");
					optionChooser (0);
					return;
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
					Console.WriteLine(string.Format("Unexpected command.\n\tExpected: {0}\n\tReceived: {1}",
						expectedNextCommand, command));
					Environment.Exit (1);
				}

				expectedNextCommand = null;

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

			public virtual void SetNumber (string variableName, float number)
			{
				variableStore.SetNumber(variableName, number);
			}

			public virtual float GetNumber (string variableName)
			{
				return variableStore.GetNumber(variableName);
			}

			public virtual void SetValue (string variableName, Value value) {
				variableStore.SetValue(variableName, value);
			}

			public virtual Value GetValue (string variableName) {
				return variableStore.GetValue(variableName);
			}

			public void Clear()
			{
				variableStore.Clear();
			}
		}


	}
}

