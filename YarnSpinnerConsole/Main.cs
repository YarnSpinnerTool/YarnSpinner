/*

The MIT License (MIT)

Copyright (c) 2015-2017 Secret Lab Pty. Ltd. and Yarn Spinner contributors.

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
using CsvHelper;
using CommandLine;
using System.Globalization;

namespace Yarn
{

    // Shared options for all commands
#pragma warning disable CA1812 // Avoid uninstantiated internal classes. Justification: These are used dynamically.
    class BaseOptions
    {
        [Option('d', "debug", HelpText = "Show debugging information.")]
        public bool showDebuggingInfo { get; set; }


        [Option('v', "verbose", HelpText = "Be verbose.")]
        public bool verbose { get; set; }

        [Value(0, MetaName = "source files", HelpText = "The files to use.")]
        public IList<string> files { get; set; }

    }

    // Options that pertain to compilation/execution
    class ExecutionOptions : BaseOptions
    {
        [Option('T', "string-table", HelpText = "The string table to use.")]
        public string stringTable { get; set; }

        [Option('o', "only-node", HelpText = "Only consider this node.")]

        public string onlyConsiderNode { get; set; }
        [Option('e', "exprimental-mode", HelpText = "Use the experimental compiler, results may be inconsistent")]
        public bool experimental { get; set; }
    }

    [Verb("verify", HelpText = "Verifies files.")]
    class VerifyOptions : ExecutionOptions
    {
        [Option('t', "show-tokens", HelpText = "Show the list of parsed tokens and exit.")]
        public bool showTokensAndExit { get; set; }

        [Option('p', "show-parse-tree", HelpText = "Show the parse tree and exit.")]
        public bool showParseTreeAndExit { get; set; }

        [Option('c', "dump-bytecode", HelpText = "Show program bytecode and exit.")]
        public bool compileAndExit { get; set; }

        [Option('v', "list-variables", HelpText = "List the variables used in the program.")]
        public bool listVariables { get; set; }
    }

    [Verb("run", HelpText = "Runs files.")]
    class RunOptions : ExecutionOptions
    {

        [Option('w', "wait-for-input", HelpText = "After showing each line, wait for the user to press a key.")]
        public bool waitForInput { get; set; }

        [Option('s', "start-node", Default = Dialogue.DEFAULT_START, HelpText = "Start at the given node.")]
        public string startNode { get; set; }

        [Option('V', "variables", HelpText = "Set default variable.")]
        public IList<string> variables { get; set; }

        [Option('r', "run-times", HelpText = "Run the script this many times.", Default = 1)]
        public int runTimes { get; set; }

        [Option('1', "select-first-choice", HelpText = "Automatically select the the first option when presented with options.")]
        public bool automaticallySelectFirstOption { get; set; }

    }

    [Verb("compile", HelpText = "Compiles the provided files.")]
    class CompileOptions : BaseOptions
    {
        [Option("format", HelpText = "The file format version to use.")]
        public Dialogue.CompiledFormat format { get; set; }
    }

    [Verb("genstrings", HelpText = "Generates string tables from provided files.")]
    class GenerateTableOptions : BaseOptions
    {
        [Option("only-tag", HelpText = "Only use nodes that have this tag.")]
        public string onlyUseTag { get; set; }
    }

    [Verb("taglines", HelpText = "Adds localisation tags to the provided files, where necessary.")]
    class AddLabelsOptions : BaseOptions
    {

        [Option("dry-run", HelpText = "Don't actually modify the contents of any files.")]
        public bool dryRun { get; set; }

        [Option("only-tag", HelpText = "Only use nodes that have this tag.")]
        public string onlyUseTag { get; set; }

    }
    [Verb("convert", HelpText = "Converts files from one format to another.")]
    class ConvertFormatOptions : BaseOptions
    {
        [Option("json", HelpText = "Convert to JSON", SetName = "format")]
        public bool convertToJSON { get; set; }

        [Option("yarn", HelpText = "Convert to Yarn", SetName = "format")]
        public bool convertToYarn { get; set; }

        [Option('o', "output-dir", HelpText = "The destination directory. Defaults to each file's source folder.")]
        public string outputDirectory { get; set; }
    }
#pragma warning restore CA1812 // Avoid uninstantiated internal classes

    class YarnSpinnerConsole
    {

        internal static Dialogue CreateDialogueForUtilities()
        {

            // Note that we're passing in with a null library - this means
            // that all function checking will be disabled, and missing funcs
            // will not cause a compile error. If a func IS missing at runtime,
            // THAT will throw an exception.

            // We do this because this tool has no idea about any of the custom
            // functions that you might be using.

            Dialogue d = new Dialogue(null);

            // Debug logging goes to Note
            d.LogDebugMessage = message => YarnSpinnerConsole.Note(message);

            // When erroring, call Warn, not Error, which terminates the program
            d.LogErrorMessage = message => YarnSpinnerConsole.Warn(message);

            return d;
        }

        public static void Error(params string[] messages)
        {

            foreach (var message in messages)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.Write("Error: ");
                Console.ResetColor();
                Console.WriteLine(message);
            }

            Environment.Exit(1);
        }

        public static void Note(params string[] messages)
        {

            foreach (var message in messages)
            {
                Console.ForegroundColor = ConsoleColor.DarkBlue;
                Console.Write("Note: ");
                Console.ResetColor();
                Console.WriteLine(message);
            }


        }

        public static void Warn(params string[] messages)
        {
            foreach (var message in messages)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.Write("Warning: ");
                Console.ResetColor();
                Console.WriteLine(message);
            }
        }

        static internal void CheckFileList(IList<string> paths, List<string> allowedExtensions)
        {

            if (paths.Count == 0)
            {
                Warn("No files provided.");
                return;
            }

            var invalid = new List<string>();

            foreach (var path in paths)
            {
                // Does this file exist?
                var exists = System.IO.File.Exists(path);

                // Does this file have the right extension?

                var hasAllowedExtension = allowedExtensions.FindIndex(item => path.EndsWith(item, StringComparison.InvariantCulture)) != -1;

                if (!exists || !hasAllowedExtension)
                {
                    invalid.Add(string.Format(CultureInfo.CurrentCulture, "\"{0}\"", path));
                }
            }

            if (invalid.Count != 0)
            {

                var message = string.Format(CultureInfo.CurrentCulture, "The file{0} {1} {2}.",
                    invalid.Count == 1 ? "" : "s",
                                            string.Join(", ", invalid.ToArray()),
                    invalid.Count == 1 ? "is not valid" : "are not valid"
                );

                Error(message);
            }
        }
        public static void Main(string[] args)
        {

            // Read and dispatch the appropriate command
            var results = CommandLine.Parser.Default.ParseArguments
            <
            RunOptions,
            VerifyOptions,
            CompileOptions,
            GenerateTableOptions,
            AddLabelsOptions,
            ConvertFormatOptions
            >(args);

            var returnCode = results.MapResult(
                (RunOptions options) => Run(options),
                (VerifyOptions options) => Verify(options),
                (CompileOptions options) => ProgramExporter.Export(options),
                (AddLabelsOptions options) => LineAdder.AddLines(options),
                (GenerateTableOptions options) => TableGenerator.GenerateTables(options),
                (ConvertFormatOptions options) => FileFormatConverter.ConvertFormat(options),

                errors => { return 1; });

            Environment.Exit(returnCode);


        }

        static internal List<string> ALLOWED_EXTENSIONS = new List<string>(new string[] { ".json", ".node", ".yarn.bytes", ".yarn.txt" });

        static int Run(RunOptions options)
        {

            CheckFileList(options.files, ALLOWED_EXTENSIONS);

            // Create the object that handles callbacks
            var impl = new ConsoleRunnerImplementation(waitForLines: options.waitForInput);

            // load the default variables we got on the command line
            foreach (var variable in options.variables)
            {
                var entry = variable.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);

                float value;
                // If there aren't two parts to this or the second part isn't a float, fail
                if (entry.Length != 2 || float.TryParse(entry[1], out value) == false)
                {
                    Warn(string.Format(CultureInfo.CurrentCulture, "Skipping invalid variable {0}", variable));
                    continue;
                }
                var name = entry[0];

                impl.SetNumber(name, value);
            }

            if (options.automaticallySelectFirstOption == true)
            {
                impl.autoSelectFirstOption = true;
            }

            Dialogue dialogue = CreateDialogue(options, impl);

            // Run the conversation

            for (int run = 0; run < options.runTimes; run++)
            {
                foreach (var step in dialogue.Run(options.startNode))
                {

                    // It can be one of three types: a line to show, options
                    // to present to the user, or an internal command to run

                    if (step is Dialogue.LineResult)
                    {
                        var lineResult = step as Dialogue.LineResult;
                        impl.RunLine(lineResult.line);
                    }
                    else if (step is Dialogue.OptionSetResult)
                    {
                        var optionsResult = step as Dialogue.OptionSetResult;
                        impl.RunOptions(optionsResult.options, optionsResult.setSelectedOptionDelegate);
                    }
                    else if (step is Dialogue.CommandResult)
                    {
                        var commandResult = step as Dialogue.CommandResult;
                        impl.RunCommand(commandResult.command.text);
                    }
                }
                impl.DialogueComplete();
            }
            return 0;
        }

        static int Verify(VerifyOptions options)
        {

            CheckFileList(options.files, ALLOWED_EXTENSIONS);

            Dialogue dialogue;
            try
            {
                dialogue = CreateDialogue(options, null);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
            {
                return 1;
            }
#pragma warning restore CA1031 // Do not catch general exception types

            if (options.compileAndExit)
            {
                var result = dialogue.GetByteCode();
                Console.WriteLine(result);
                return 0;
            }

            var context = new Yarn.Analysis.Context();

            dialogue.Analyse(context);

            foreach (var diagnosis in context.FinishAnalysis())
            {
                switch (diagnosis.severity)
                {
                    case Analysis.Diagnosis.Severity.Error:
                        Error(diagnosis.ToString(showSeverity: false));
                        break;
                    case Analysis.Diagnosis.Severity.Warning:
                        Warn(diagnosis.ToString(showSeverity: false));
                        break;
                    case Analysis.Diagnosis.Severity.Note:
                        Note(diagnosis.ToString(showSeverity: false));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

            }
            return 0;

        }

        static internal Dialogue CreateDialogue(ExecutionOptions options, ConsoleRunnerImplementation impl)
        {

            // Load nodes
            var dialogue = new Dialogue(impl);

            if (options.experimental)
            {
                Warn("Running YarnSpinner in experimental mode may have unexpected behaviour.");
                dialogue.experimentalMode = true;
            }

            // Add some methods for testing
            dialogue.library.RegisterFunction("add_three_operands", 3, delegate (Value[] parameters)
            {
                return parameters[0] + parameters[1] + parameters[2];
            });

            dialogue.library.RegisterFunction("last_value", -1, delegate (Value[] parameters)
            {
                // return the last value
                return parameters[parameters.Length - 1];
            });

            dialogue.library.RegisterFunction("is_even", 1, delegate (Value[] parameters)
            {
                return (int)parameters[0].AsNumber % 2 == 0;
            });

            // Register the "assert" function, which stops execution if its parameter evaluates to false
            dialogue.library.RegisterFunction("assert", -1, delegate (Value[] parameters)
            {
                if (parameters[0].AsBool == false)
                {

                    // TODO: Include file, node and line number
                    if (parameters.Length > 1 && parameters[1].AsBool)
                    {
                        dialogue.LogErrorMessage("ASSERTION FAILED: " + parameters[1].AsString);
                    }
                    else
                    {
                        dialogue.LogErrorMessage("ASSERTION FAILED");
                    }
                    Environment.Exit(1);
                }
            });


            // Register a function to let test scripts register how many
            // options they expect to send
            dialogue.library.RegisterFunction("prepare_for_options", 2, delegate (Value[] parameters)
            {
                impl.numberOfExpectedOptions = (int)parameters[0].AsNumber;
                impl.autoSelectOptionNumber = (int)parameters[1].AsNumber;
            });

            dialogue.library.RegisterFunction("expect_line", 1, delegate (Value[] parameters)
            {
                impl.expectedNextLine = parameters[0].AsString;
            });

            dialogue.library.RegisterFunction("expect_command", 1, delegate (Value[] parameters)
            {
                impl.expectedNextCommand = parameters[0].AsString;
            });

            // If debugging is enabled, log debug messages; otherwise, ignore them
            if (options.showDebuggingInfo)
            {
                dialogue.LogDebugMessage = delegate (string message)
                {
                    Note(message);
                };
            }
            else
            {
                dialogue.LogDebugMessage = delegate (string message) { };
            }

            dialogue.LogErrorMessage = delegate (string message)
            {
                Warn("Yarn Error: " + message);
            };

            foreach (var file in options.files)
            {
                try
                {
                    dialogue.LoadFile(file, false, false, options.onlyConsiderNode);
                }
                catch (Yarn.TokeniserException e)
                {
                    Warn(e.Message);
                }
                catch (Yarn.ParseException e)
                {
                    Warn(e.Message);
                }

            }

            // Load string table
            if (options.stringTable != null)
            {

                var parsedTable = new Dictionary<string, string>();

                using (var reader = new System.IO.StreamReader(options.stringTable))
                {
                    using (var csvReader = new CsvReader(reader))
                    {
                        if (csvReader.ReadHeader() == false)
                        {
                            Error(string.Format(CultureInfo.CurrentCulture, "{0} is not a valid string table", options.stringTable));
                        }

                        foreach (var row in csvReader.GetRecords<LocalisedLine>())
                        {
                            parsedTable[row.LineCode] = row.LineText;
                        }
                    }
                }

                dialogue.AddStringTable(parsedTable);
            }

            return dialogue;
        }

        // A simple Implementation for the command line.
        internal class ConsoleRunnerImplementation : Yarn.VariableStorage
        {

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

            public ConsoleRunnerImplementation(bool waitForLines = false)
            {
                this.variableStore = new MemoryVariableStore();
                this.waitForLines = waitForLines;
            }

            public void RunLine(Yarn.Line lineText)
            {

                if (expectedNextLine != null && expectedNextLine != lineText.text)
                {
                    // TODO: Output diagnostic info here
                    Error(string.Format(CultureInfo.CurrentCulture, "Unexpected line.\nExpected: {0}\nReceived: {1}",
                        expectedNextLine, lineText.text));

                }

                expectedNextLine = null;

                Console.WriteLine(lineText.text);
                if (waitForLines == true)
                {
                    Console.Read();
                }
            }

            public void RunOptions(Options optionsGroup, OptionChooser optionChooser)
            {

                Console.WriteLine("Options:");
                for (int i = 0; i < optionsGroup.options.Count; i++)
                {
                    var optionDisplay = string.Format(CultureInfo.CurrentCulture, "{0}. {1}", i + 1, optionsGroup.options[i]);
                    Console.WriteLine(optionDisplay);
                }


                // Check to see if the number of expected options
                // is what we're expecting to see
                if (numberOfExpectedOptions != -1 &&
                    optionsGroup.options.Count != numberOfExpectedOptions)
                {
                    // TODO: Output diagnostic info here
                    Error(string.Format(CultureInfo.CurrentCulture, "[ERROR: Expected {0} options, but received {1}]", numberOfExpectedOptions, optionsGroup.options.Count));

                }

                // If we were told to automatically select an option, do so
                if (autoSelectOptionNumber != -1)
                {
                    Note(string.Format(CultureInfo.CurrentCulture, "[Received {0} options, choosing option {1}]", optionsGroup.options.Count, autoSelectOptionNumber));

                    optionChooser(autoSelectOptionNumber);

                    autoSelectOptionNumber = -1;

                    return;

                }

                // Reset the expected options counter
                numberOfExpectedOptions = -1;



                if (autoSelectFirstOption == true)
                {
                    Note("[automatically choosing option 1]");
                    optionChooser(0);
                    return;
                }

                do
                {
                    Console.Write("? ");
                    try
                    {
                        var selectedKey = Console.ReadKey().KeyChar.ToString(CultureInfo.InvariantCulture);
                        int selection;

                        if (int.TryParse(selectedKey, out selection) == true)
                        {
                            Console.WriteLine();

                            // we present the list as 1,2,3, but the API expects
                            // answers as 0,1,2
                            selection -= 1;

                            if (selection > optionsGroup.options.Count)
                            {
                                Console.WriteLine("Invalid option.");
                            }
                            else
                            {
                                optionChooser(selection);
                                break;
                            }
                        }

                    }
                    catch (FormatException) { }

                } while (true);
            }

            public void RunCommand(string command)
            {

                if (expectedNextCommand != null && expectedNextCommand != command)
                {
                    // TODO: Output diagnostic info here
                    Error(string.Format(CultureInfo.CurrentCulture, "Unexpected command.\n\tExpected: {0}\n\tReceived: {1}",
                                        expectedNextCommand, command));
                }

                expectedNextCommand = null;

                Console.WriteLine("Command: <<" + command + ">>");
            }

            public void DialogueComplete()
            {
                // All done
            }

            public void HandleErrorMessage(string error)
            {
                Error(error);
            }

            public void HandleDebugMessage(string message)
            {
                Note(message);
            }

            public virtual void SetNumber(string variableName, float number)
            {
                variableStore.SetValue(variableName, new Value(number));
            }

            public virtual float GetNumber(string variableName)
            {
                return variableStore.GetValue(variableName).AsNumber;
            }

            public virtual void SetValue(string variableName, Value value)
            {
                variableStore.SetValue(variableName, value);
            }

            public virtual Value GetValue(string variableName)
            {
                return variableStore.GetValue(variableName);
            }

            public void Clear()
            {
                variableStore.Clear();
            }
        }


    }
}

