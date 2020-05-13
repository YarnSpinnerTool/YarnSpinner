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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using YarnSpinner;
using System.Text;

namespace Yarn {

    /// <summary>
    /// A line of dialogue, sent from the <see cref="Dialogue"/> to the
    /// game.
    /// </summary>
    /// <remarks>
    /// When the game receives a <see cref="Line"/>, it should do the
    /// following things to prepare the line for presentation to the user. 
    ///
    /// 1. Use the value in the <see cref="ID"/> field to look up the
    /// appropriate user-facing text in the string table. 
    ///
    /// 2. For each of the entries in the <see cref="Substitutions"/>
    /// field, replace the corresponding placeholder with the entry. That
    /// is, the text "`{0}`" should be replaced with the value of
    /// `Substitutions[0]`, "`{1}`" with `Substitutions[1]`, and so on. 
    ///
    /// 3. Use <see cref="Dialogue.ExpandFormatFunctions(string, string)"/>
    /// to expand all [format functions]({{|ref
    /// "/docs/syntax.md#format-functions"|}}) in the line.
    ///
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.LineHandler"/>
    public struct Line {
        internal Line(string stringID) : this()
        {
            this.ID = stringID;
            this.Substitutions = new string[] {};
        }

        /// <summary>
        /// The string ID for this line.
        /// </summary>
        public string ID;

        /// <summary>
        /// The text of the line.
        /// </summary>
        [Obsolete("This field will always be empty; lines do not contain their own text. Instead, use the ID field to look up the text in the string table.")]
        public string Text;

        /// <summary>
        /// The values that should be inserted into the user-facing text before delivery.
        /// </summary>
        public string[] Substitutions;
    }

    /// <summary>
    /// A set of <see cref="OptionSet.Option"/>s, sent from the <see
    /// cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.OptionsHandler"/>
    public struct OptionSet {
        internal OptionSet(Option[] options)
        {
            Options = options;
        }

        /// <summary>
        /// An option to be presented to the user.
        /// </summary>
        public struct Option {
            internal Option(Line line, int id)
            {
                Line = line;
                ID = id;
            }

            /// <summary>
            /// Gets the <see cref="Line"/> that should be presented to the user
            /// for this option.
            /// </summary>
            /// <remarks>
            /// See the documentation for the <see cref="Yarn.Line"/> class for
            /// information on how to prepare a line before presenting it
            /// to the user. 
            /// </remarks>
            public Line Line {get; private set;}

            /// <summary>
            /// Gets the identifying number for this option.
            /// </summary>
            /// <remarks>
            /// When the user selects this option, this value should be used as the parameter for <see cref="Dialogue.SetSelectedOption(int)"/>.
            /// </remarks>
            public int ID {get; private set;}
        }
        
        /// <summary>
        /// Gets the <see cref="Option"/>s that should be presented to the user.
        /// </summary>
        /// <seealso cref="Option"/>
        public Option[] Options {get; private set;}
    }

    /// <summary>
    /// A command, sent from the <see cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.CommandHandler"/>    
    public struct Command {
        internal Command(string text)
        {
            Text = text;
        }

        /// <summary>
        /// Gets the text of the command.
        /// </summary>
        public string Text {get; private set;}
    }

    /// <summary>
    /// Represents a method that receives diagnostic messages and error information
    /// from a <see cref="Dialogue"/>.
    /// </summary>
    /// <remarks>
    /// The text that this delegate receives may be output to a console, or
    /// sent to a log.
    /// </remarks>
    /// <param name="message">The text that should be logged.</param>
    public delegate void Logger(string message);

    /// <summary>Provides a mechanism for storing and retrieving instances
    /// of the <see cref="Value"/> class.</summary>
    public interface VariableStorage {

        /// <summary>
        /// Stores a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this variable.</param>
        /// <param name="value">The value to store.</param>
        void SetValue(string variableName, Value value);

        // some convenience setters

        /// <summary>
        /// Stores a <see cref="string"/> as a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this variable.</param>
        /// <param name="stringValue">The string to store.</param>
        void SetValue(string variableName, string stringValue);

        /// <summary>
        /// Stores a <see cref="float"/> as a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this variable.</param>
        /// <param name="floatValue">The number to store.</param>
        void SetValue(string variableName, float floatValue);

        /// <summary>
        /// Stores a <see cref="bool"/> as a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this variable.</param>
        /// <param name="boolValue">The boolean value to store.</param>
        void SetValue(string variableName, bool boolValue);

        /// <summary>
        /// Retrieves a <see cref="Value"/> by name.
        /// </summary>
        /// <param name="variableName">The name of the variable to retrieve
        /// the value of.</param>
        /// <returns>The <see cref="Value"/>. If a variable by the name of
        /// `variableName` is not present, returns a value representing
        /// `null`.</returns>
        Value GetValue(string variableName);

        /// <summary>
        /// Removes all variables from storage.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// An abstract class that implements convenience methods for
    /// converting values to instances of <see cref="Value"/>. 
    /// </summary>
    /// <remarks>
    /// If you subclass this, you only have to implement <see
    /// cref="BaseVariableStorage.SetValue(string, Value)"/>, <see
    /// cref="BaseVariableStorage.GetValue(string)"/> and <see
    /// cref="BaseVariableStorage.Clear"/>.
    /// </remarks>
    /// <inheritdoc cref="VariableStorage"/>
    public abstract class BaseVariableStorage : VariableStorage {

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, string stringValue)
        {
            Value val = new Yarn.Value(stringValue);
            SetValue(variableName, val);
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, float floatValue)
        {
            Value val = new Yarn.Value(floatValue);
            SetValue(variableName, val);
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, bool boolValue)
        {
            Value val = new Yarn.Value(boolValue);
            SetValue(variableName, val);
        }

        /// <inheritdoc/>
        public abstract void SetValue(string variableName, Value value);

        /// <inheritdoc/>
        public abstract Value GetValue(string variableName);

        /// <inheritdoc/>
        public abstract void Clear();
    }

    /// <summary>
    /// A simple concrete subclass of <see cref="BaseVariableStorage"/> that
    /// keeps all variables in memory.
    /// </summary>
    public class MemoryVariableStore : Yarn.BaseVariableStorage
    {

        private Dictionary<string, Value> variables = new Dictionary<string, Value>();

        /// <inheritdoc/>
        public override void SetValue(string variableName, Value value)
        {
            variables[variableName] = value;
        }

        /// <inheritdoc/>
        public override Value GetValue(string variableName)
        {
            Value value = Value.NULL;
            if (variables.ContainsKey(variableName))
            {
                value = variables[variableName];
            }
            return value;
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            variables.Clear();
        }
    }

    /// <summary>
    /// Co-ordinates the execution of Yarn programs.
    /// </summary>
    public class Dialogue {

        /// We'll ask this object for the state of variables
        internal VariableStorage variableStorage {get;set;}
		
        /// <summary>
        /// Invoked when the Dialogue needs to report debugging information.
        /// </summary>
        public Logger LogDebugMessage {get;set;}

        /// <summary>
        /// Invoked when the Dialogue needs to report an error.
        /// </summary>
        public Logger LogErrorMessage {get;set;}

        /// <summary>The node that execution will start from.</summary>
        public const string DEFAULT_START = "Start";

        // A string used to mark where a value should be injected in a
        // format function. Generated during format function parsing; not
        // typed by a human.
        private const string FormatFunctionValuePlaceholder = "<VALUE PLACEHOLDER>";

        private Program _program;

        /// <summary>Gets or sets the compiled Yarn program.</summary>
        internal Program Program
        {
            get => _program;
            set
            {
                _program = value;

                vm.Program = value;
                vm.ResetState();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Dialogue is currently executing Yarn instructions.
        /// </summary>
        public bool IsActive => vm.executionState != VirtualMachine.ExecutionState.Stopped;

        /// <summary>
        /// Used as a return type by handlers (such as the <see
        /// cref="LineHandler"/>) to indicate whether a <see
        /// cref="Dialogue"/> should suspend execution, or continue
        /// executing, after it has called the handler.
        /// </summary>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        public enum HandlerExecutionType {

            /// <summary>
            /// Indicates that the <see cref="Dialogue"/> should suspend execution.
            /// </summary>
            PauseExecution,

            /// <summary>
            /// Indicates that the <see cref="Dialogue"/> should continue execution.
            /// </summary>
            ContinueExecution,
        }

        /// <summary>
        /// Represents the method that is called when the Dialogue delivers
        /// a <see cref="Line"/>.
        /// </summary>
        /// <param name="line">The <see cref="Line"/> that has been
        /// delivered.</param>
        /// <returns>Whether the <see cref="Dialogue"/> should suspend
        /// execution after delivering this line.</returns>
        /// <seealso cref="HandlerExecutionType"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate HandlerExecutionType LineHandler(Line line);

        /// <summary>
        /// Represents the method that is called when the Dialogue delivers
        /// an <see cref="OptionSet"/>.
        /// </summary>
        /// <param name="options">The <see cref="OptionSet"/> that has been
        /// delivered.</param>
        /// <remarks>
        /// Unlike <see cref="LineHandler"/>, <see cref="OptionsHandler"/>
        /// does not return a <see cref="HandlerExecutionType"/> to signal
        /// that the Dialogue should suspend execution. This is because the
        /// Dialogue will _always_ need to wait for the user to make a
        /// selection before execution can resume.
        /// </remarks>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate void OptionsHandler(OptionSet options);

        /// <summary>
        /// Represents the method that is called when the Dialogue delivers
        /// a <see cref="Command"/>.
        /// </summary>
        /// <param name="command">The <see cref="Command"/> that has been
        /// delivered.</param>
        /// <returns>Whether the <see cref="Dialogue"/> should suspend
        /// execution after delivering this command.</returns>
        /// <seealso cref="HandlerExecutionType"/>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate HandlerExecutionType CommandHandler(Command command);

        /// <summary>
        /// Represents the method that is called when the Dialogue reaches
        /// the end of a node.
        /// </summary>
        /// <param name="completedNodeName">The name of the node.</param>
        /// <returns>Whether the <see cref="Dialogue"/> should suspend
        /// execution after this method has been called.</returns>
        /// <remarks>
        /// This method may be called multiple times over the course of
        /// code execution. A node being complete does not necessarily
        /// represent the end of the conversation.
        /// </remarks>
        /// <seealso cref="HandlerExecutionType"/>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate HandlerExecutionType NodeCompleteHandler(string completedNodeName);

        /// <summary>
        /// Represents the method that is called when the dialogue has
        /// reached its end, and no more code remains to be run.
        /// </summary>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        public delegate void DialogueCompleteHandler();

        /// <summary>
        /// Gets or sets the <see cref="LineHandler"/> that is called when
        /// a line is ready to be shown to the user.
        /// </summary>
        public LineHandler lineHandler
        {
            get => vm.lineHandler;
            set => vm.lineHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="OptionsHandler"/> that is called
        /// when a set of options are ready to be shown to the user.
        /// </summary>
        /// <remarks>
        /// The Options Handler delivers an <see cref="OptionSet"/> to the
        /// game. Before <see cref="Continue"/> can be called to resume
        /// execution, <see cref="SetSelectedOption"/> must be called to
        /// indicate which <see cref="OptionSet.Option"/> was selected by the user.
        /// If <see cref="SetSelectedOption"/> is not called, an exception
        /// is thrown.
        /// </remarks>
        public OptionsHandler optionsHandler
        {
            get => vm.optionsHandler;
            set => vm.optionsHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="CommandHandler"/> that is called
        /// when a command is to be delivered to the game.
        /// </summary>
        public CommandHandler commandHandler
        {
            get => vm.commandHandler;
            set => vm.commandHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="NodeCompleteHandler"/> that is called
        /// when a node is complete.
        /// </summary>
        public NodeCompleteHandler nodeCompleteHandler
        {
            get => vm.nodeCompleteHandler;
            set => vm.nodeCompleteHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="DialogueCompleteHandler"/> that is called
        /// when the dialogue reaches its end.
        /// </summary>
        public DialogueCompleteHandler dialogueCompleteHandler
        {
            get => vm.dialogueCompleteHandler;
            set => vm.dialogueCompleteHandler = value;
        }

        private VirtualMachine vm;

        /// <summary>
        /// Gets the <see cref="Library"/> that this Dialogue uses to
        /// locate functions.
        /// </summary>
        /// <remarks>
        /// When the Dialogue is constructed, the Library is initialized
        /// with the built-in operators like `+`, `-`, and so on.
        /// </remarks>
        public Library library { get; internal set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialogue"/> class.
        /// </summary>
        /// <param name="variableStorage">The <see cref="VariableStorage"/> that this Dialogue should use.</param>
        public Dialogue(Yarn.VariableStorage variableStorage)
        {
            this.variableStorage = variableStorage ?? throw new ArgumentNullException(nameof(variableStorage));
            library = new Library();

            this.vm = new VirtualMachine(this);

            library.ImportLibrary(new StandardLibrary());
        }

        /// <summary>
        /// Loads all nodes from the provided <see cref="Yarn.Program"/>.
        /// </summary>
        /// <remarks>
        /// This method replaces any existing nodes have been loaded. If
        /// you want to load nodes from an _additional_ Program, use the
        /// <see cref="AddProgram(Program)"/> method.
        /// </remarks>
        /// <param name="program">The <see cref="Yarn.Program"/> to
        /// use.</param>
        public void SetProgram(Program program) {
            this.Program = program;
        }

        /// <summary>
        /// Loads the nodes from the specified <see cref="Yarn.Program"/>,
        /// and adds them to the nodes already loaded.
        /// </summary>
        /// <param name="program">The additional program to load.</param>
        /// <remarks>
        /// If <see cref="Program"/> is `null`, this method has the effect
        /// as calling
        /// <see cref="SetProgram(Program)"/>.
        /// </remarks>
        public void AddProgram(Program program) {
            if (this.Program == null) {
                SetProgram(program);
                return;
            } else {
                this.Program = Program.Combine(this.Program, program);
            }
            
        }

        /// <summary>
        /// Loads a compiled <see cref="Yarn.Program"/> from a file.
        /// </summary>
        /// <param name="fileName">The path of the file to load.</param>
        /// <remarks>
        /// This method replaces the current value of <see cref="Program"/>
        /// with the result of loading the file.
        ///
        /// This method does not compile Yarn source. To compile Yarn
        /// source code into a <see cref="Yarn.Program"/>, use the <see
        /// cref="Yarn.Compiler"/> class.
        /// </remarks>
        internal void LoadProgram(string fileName)
        {
            var bytes = File.ReadAllBytes(fileName);

            this.Program = Program.Parser.ParseFrom(bytes);
        }

        /// <summary>
        /// Prepares the <see cref="Dialogue"/> that the user intends to start running a node.
        /// </summary>
        /// <param name="startNode">The name of the node that will be run. The node have been loaded by calling <see cref="SetProgram(Program)"/> or <see cref="AddProgram(Program)"/>.</param>
        /// <remarks>
        /// After this method is called, you call <see cref="Continue"/> to start executing it.
        /// </remarks>
        /// <throws cref="DialogueException">Thrown when no node named `startNode` has been loaded.</throws>
        public void SetNode(string startNode = DEFAULT_START)
        {
            vm.SetNode(startNode);
        }

        /// <summary>
        /// Signals to the <see cref="Dialogue"/> that the user has
        /// selected a specified <see cref="OptionSet.Option"/>.
        /// </summary>
        /// <remarks>
        /// After the Dialogue delivers an <see cref="OptionSet"/>, this
        /// method must be called before <see cref="Continue"/> is called.
        ///
        /// The ID number that should be passed as the parameter to this
        /// method should be the <see cref="OptionSet.Option.ID"/> field in
        /// the <see cref="OptionSet.Option"/> that represents the user's
        /// selection.
        /// </remarks>
        /// <param name="selectedOptionID">The ID number of the Option that
        /// the user selected.</param>
        /// <throws cref="DialogueException">Thrown when the Dialogue is
        /// not expecting an option to be selected.</throws>
        /// <throws cref="ArgumentOutOfRangeException">Thrown when
        /// `selectedOptionID` is not a valid option ID.</throws>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="OptionSet"/>
        /// <seealso cref="Continue"/>
        public void SetSelectedOption(int selectedOptionID) {
            vm.SetSelectedOption(selectedOptionID);
        }
        
        /// <summary>
        /// Starts, or continues, execution of the current Program.
        /// </summary>
        /// <remarks>
        /// This method repeatedly executes instructions until one of the following conditions is encountered:
        /// 
        /// * The <see cref="lineHandler"/>, <see cref="commandHandler"/>, or <see cref="nodeCompleteHandler"/> return <see cref="HandlerExecutionType.PauseExecution"/>.
        /// * The <see cref="optionsHandler"/> is called. When this occurs, the Dialogue is waiting for the user to specify which of the options has been selected, and <see cref="SetSelectedOption(int)"/> must be called before <see cref="Continue"/> is called again.)
        /// * The Program reaches its end. When this occurs, <see cref="SetNode(string)"/> must be called before <see cref="Continue"/> is called again.
        /// * An error occurs while executing the Program.
        ///
        /// This method has no effect if it is called while the <see cref="Dialogue"/> is currently in the process of executing instructions.
        /// </remarks>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        /// <seealso cref="HandlerExecutionType"/>
        public void Continue() {
            if (vm.executionState == VirtualMachine.ExecutionState.Running) {
                // Cannot 'continue' an already running VM.
                return;
            }
            vm.Continue();
        }

        /// <summary>
        /// Immediately stops the <see cref="Dialogue"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="dialogueCompleteHandler"/> will not be called if
        /// the dialogue is ended by calling <see cref="Stop"/>.
        /// </remarks>
        public void Stop() {
            if (vm != null)
                vm.Stop();
        }

        /// <summary>
        /// Gets the names of the nodes in the Program.
        /// </summary>
        public IEnumerable<string> allNodes {
            get {
                return Program.Nodes.Keys;
            }
        }

        /// <summary>
        /// Gets the name of the node that this Dialogue is currently
        /// executing.
        /// </summary>
        /// <remarks>If <see cref="Continue"/> has never been called, this
        /// value will be `null`.</remarks>
        public string currentNode
        {
            get
            {
                if (vm == null)
                {
                    return null;
                }
                else
                {
                    return vm.currentNodeName;
                }

            }
        }

        /// <summary>
        /// Returns the string ID that contains the original, uncompiled
        /// source text for a node.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The string ID.</returns>
        /// <remarks>
        /// A node's source text will only be present in the string table
        /// if its `tags` header contains `rawText`.
        ///
        /// Because the <see cref="Dialogue"/> class is designed to be
        /// unaware of the contents of the string table, this method does
        /// not test to see if the string table contains an entry with the
        /// line ID. You will need to test for that yourself.
        /// </remarks>
        public string GetStringIDForNode(string nodeName)
        {
            if (Program.Nodes.Count == 0)
            {
                LogErrorMessage("No nodes are loaded!");
                return null;
            }
            else if (Program.Nodes.ContainsKey(nodeName))
            {
                return "line:" + nodeName;
            }
            else
            {
                LogErrorMessage("No node named " + nodeName);
                return null;
            }
        }

        /// <summary>
        /// Returns the tags for the node 'nodeName'.
        /// </summary>
        /// <remarks>
        /// The tags for a node are defined by setting the `tags`
        /// [header]({{|ref "/docs/syntax.md#header"|}}) in the node's source
        /// code. This header must be a space-separated list.
        /// </remarks>      
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The node's tags, or `null` if the node is not present
        /// in the Program.</returns>
        public IEnumerable<string> GetTagsForNode(string nodeName)
        {
            if (Program.Nodes.Count == 0)
            {
                LogErrorMessage("No nodes are loaded!");
                return null;
            }
            else if (Program.Nodes.ContainsKey(nodeName))
            {
                return Program.GetTagsForNode(nodeName);
            }
            else
            {
                LogErrorMessage("No node named " + nodeName);
                return null;
            }
        }

        /// <summary>
        /// Unloads all nodes from the Dialogue.
        /// </summary>
        public void UnloadAll() {
            Program = null;
        }

        internal String GetByteCode() {
            return Program.DumpCode (library);
        }

        /// <summary>
        /// Gets a value indicating whether a specified node exists in the
        /// Program.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>`true` if a node named `nodeName` exists in the Program, `false` otherwise.</returns>
        public bool NodeExists(string nodeName) {
            if (Program == null) {
                LogErrorMessage ("Tried to call NodeExists, but no nodes " +
                                 "have been compiled!");
                return false;
            }
            if (Program.Nodes == null || Program.Nodes.Count == 0) {
                LogDebugMessage ("Called NodeExists, but there are zero nodes. " +
                                 "This may be an error.");
                return false;
            }
            return Program.Nodes.ContainsKey(nodeName);
        }


        public void Analyse(Analysis.Context context) {
            context.AddProgramToAnalysis (this.Program);
        }

        /// <summary>
        /// Expands all [format functions]({{|ref
        /// "syntax.md#format-functions"|}}) in a given string, using
        /// pluralisation rules specified by the given locale. 
        /// </summary>
        /// <param name="input">The string to process.</param>
        /// <param name="localeCode">The locale code, as an IETF BCP-47
        /// language tag, to use when determining the plural categories of
        /// numbers.</param>
        /// <returns>The original string, with any format functions
        /// replaced with their evaluated versions.</returns>
        /// <throws cref="ArgumentException">Thrown when the string
        /// contains a `plural` or `ordinal` format function, but the
        /// specified value cannot be parsed as a number.</throws>
        public static string ExpandFormatFunctions(string input, string localeCode)
        {
            ParseFormatFunctions(input, out var lineWithReplacements, out var formatFunctions);

            for (int i = 0; i < formatFunctions.Length; i++)
            {
                ParsedFormatFunction function = formatFunctions[i];

                // Apply the "select" format function
                if (function.functionName == "select")
                {
                    if (function.data.TryGetValue(function.value, out string replacement) == false)
                    {
                        replacement = $"<no replacement for {function.value}>";
                    }

                    // Insert the value if needed
                    replacement = replacement.Replace(FormatFunctionValuePlaceholder, function.value);

                    lineWithReplacements = lineWithReplacements.Replace("{" + i + "}", replacement);
                }
                else
                {
                    // Apply the "plural" or "ordinal" format function

                    if (double.TryParse(function.value, out var value) == false)
                    {
                        throw new ArgumentException($"Error while pluralising line '{input}': '{function.value}' is not a number");
                    }

                    CLDRPlurals.PluralCase pluralCase;

                    switch (function.functionName)
                    {
                        case "plural":
                            pluralCase = CLDRPlurals.NumberPlurals.GetCardinalPluralCase(localeCode, value);
                            break;
                        case "ordinal":
                            pluralCase = CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(localeCode, value);
                            break;
                        default:
                            throw new ArgumentException($"Unknown formatting function '{function.functionName}' in line '{input}'");
                    }

                    if (function.data.TryGetValue(pluralCase.ToString().ToLowerInvariant(), out string replacement) == false)
                    {
                        replacement = $"<no replacement for {function.value}>";
                    }

                    // Insert the value if needed
                    replacement = replacement.Replace(FormatFunctionValuePlaceholder, function.value);

                    lineWithReplacements = lineWithReplacements.Replace("{" + i + "}", replacement);

                }
            }
            return lineWithReplacements;
        }

        internal static void ParseFormatFunctions(string input, out string lineWithReplacements, out ParsedFormatFunction[] parsedFunctions)
        {
            var stringReader = new StringReader(input);

            var stringBuilder = new System.Text.StringBuilder();

            var returnedFunctions = new List<ParsedFormatFunction>();

            int next;

            // Read the entirety of the line
            while ((next = stringReader.Read()) != -1)
            {
                char c = (char)next;

                if (c != '[')
                {
                    // plain text!
                    stringBuilder.Append(c);
                }
                else
                {
                    // the start of a format function!

                    ParsedFormatFunction function = new ParsedFormatFunction();


                    // Structure of a format function:
                    // [ name "value" key1="value1" key2="value2" ]

                    // Read the name
                    function.functionName = ExpectID();

                    // Ensure that only valid function names are used
                    switch (function.functionName) {
                        case "select":
                        break;
                        case "plural":
                        break;
                        case "ordinal":
                        break;
                        default:
                        throw new ArgumentException($"Invalid formatting function {function.functionName} in line \"{input}\"");
                    }

                    function.value = ExpectString();

                    function.data = new Dictionary<string, string>();

                    // parse and read the data for this format function
                    while (true)
                    {
                        ConsumeWhitespace();

                        var peek = stringReader.Peek();
                        if ((char)peek == ']')
                        {
                            // we're done adding parameters
                            break;
                        }

                        // this is a key-value pair
                        var key = ExpectID();
                        ExpectCharacter('=');
                        var value = ExpectString();

                        if (function.data.ContainsKey(key))
                        {
                            throw new ArgumentException($"Duplicate value '{key}' in format function inside line \"{input}\"");
                        }

                        function.data.Add(key, value);

                    }

                    // We now expect the end of this format function
                    ExpectCharacter(']');

                    // reached the end of this function; add it to the
                    // list
                    returnedFunctions.Add(function);

                    // and add a placeholder for this function's value
                    stringBuilder.Append("{" + (returnedFunctions.Count - 1) + "}");                    

                    // Local functions used in parsing

                    // id = [_\w][\w0-9_]*
                    string ExpectID()
                    {
                        ConsumeWhitespace();
                        var idStringBuilder = new StringBuilder();

                        // Read the first character, which must be a letter
                        int tempNext = stringReader.Read();
                        AssertNotEndOfInput(tempNext);
                        char nextChar = (char)tempNext;

                        if (char.IsLetter(nextChar) || nextChar == '_')
                        {
                            idStringBuilder.Append((char)tempNext);
                        }
                        else
                        {
                            throw new ArgumentException($"Expected an identifier inside a format function in line \"{input}\"");
                        }

                        // Read zero or more letters, numbers, or underscores
                        while (true)
                        {
                            tempNext = stringReader.Peek();
                            if (tempNext == -1)
                            {
                                break;
                            }
                            nextChar = (char)tempNext;
                            if (char.IsLetterOrDigit(nextChar) || (char)tempNext == '_')
                            {
                                idStringBuilder.Append((char)tempNext);
                                stringReader.Read(); // consume it
                            }
                            else
                            {
                                // no more
                                break;
                            }
                        }
                        return idStringBuilder.ToString();
                    }

                    // string = " (\"|\\|^["])* "
                    string ExpectString()
                    {
                        ConsumeWhitespace();

                        var stringStringBuilder = new StringBuilder();

                        int tempNext = stringReader.Read();
                        AssertNotEndOfInput(tempNext);

                        char nextChar = (char)tempNext;
                        if (nextChar != '"')
                        {
                            throw new ArgumentException($"Expected a string inside a format function in line {input}");
                        }

                        while (true)
                        {
                            tempNext = stringReader.Read();
                            AssertNotEndOfInput(tempNext);
                            nextChar = (char)tempNext;                            

                            if (nextChar == '"')
                            {
                                // end of string - consume it but don't
                                // append to the final collection
                                break;
                            }
                            else if (nextChar == '\\')
                            {
                                // an escaped quote or backslash
                                int nextNext = stringReader.Read();
                                AssertNotEndOfInput(nextNext);
                                int nextNextChar = (char)nextNext;
                                if (nextNextChar == '\\' || nextNextChar == '"' || nextNextChar == '%')
                                {
                                    stringStringBuilder.Append(nextNextChar);
                                } 
                            } else if (nextChar == '%') {
                                stringStringBuilder.Append(FormatFunctionValuePlaceholder);
                            }
                            else
                            {
                                stringStringBuilder.Append(nextChar);
                            }

                        }

                        return stringStringBuilder.ToString();
                    }

                    // Consume a character, and throw an exception if it
                    // isn't the one we expect.
                    void ExpectCharacter(char character)
                    {
                        ConsumeWhitespace();

                        int tempNext = stringReader.Read();
                        AssertNotEndOfInput(tempNext);
                        if ((char)tempNext != character)
                        {
                            throw new ArgumentException($"Expected a {character} inside a format function in line \"{input}\"");
                        }
                    }

                    // Throw an exception if value represents the end of
                    // input.
                    void AssertNotEndOfInput(int value)
                    {
                        if (value == -1)
                        {
                            throw new ArgumentException($"Unexpected end of line inside a format function in line \"{input}");
                        }
                    }

                    // Read and discard all whitespace until we hit
                    // something that isn't whitespace.
                    void ConsumeWhitespace(bool allowEndOfLine = false)
                    {
                        while (true)
                        {
                            var tempNext = stringReader.Peek();
                            if (tempNext == -1 && allowEndOfLine == false)
                            {
                                throw new ArgumentException($"Unexpected end of line inside a format function in line \"{input}");
                            }

                            if (char.IsWhiteSpace((char)tempNext) == true)
                            {
                                // consume it and continue
                                stringReader.Read();
                            }
                            else
                            {
                                // no more whitespace ahead; don't
                                // consume it, but instead stop eating
                                // whitespace
                                return;
                            }
                        }
                    }
                }
            }

            lineWithReplacements = stringBuilder.ToString();
            parsedFunctions = returnedFunctions.ToArray();
        }

        /// The standard, built-in library of functions and operators.
        private class StandardLibrary : Library {

            public StandardLibrary() {

                #region Operators

                this.RegisterFunction(TokenType.Add.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] + parameters[1];
                });

                this.RegisterFunction(TokenType.Minus.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] - parameters[1];
                });

                this.RegisterFunction(TokenType.UnaryMinus.ToString(), 1, delegate(Value[] parameters) {
                    return -parameters[0];
                });

                this.RegisterFunction(TokenType.Divide.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] / parameters[1];
                });

                this.RegisterFunction(TokenType.Multiply.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] * parameters[1];
                });

                this.RegisterFunction(TokenType.Modulo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] % parameters[1];
                });

                this.RegisterFunction(TokenType.EqualTo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].Equals( parameters[1] );
                });

                this.RegisterFunction(TokenType.NotEqualTo.ToString(), 2, delegate(Value[] parameters) {

                    // Return the logical negative of the == operator's result
                    var equalTo = this.GetFunction(TokenType.EqualTo.ToString());

                    return !equalTo.Invoke(parameters).AsBool;
                });

                this.RegisterFunction(TokenType.GreaterThan.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] > parameters[1];
                });

                this.RegisterFunction(TokenType.GreaterThanOrEqualTo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] >= parameters[1];
                });

                this.RegisterFunction(TokenType.LessThan.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] < parameters[1];
                });

                this.RegisterFunction(TokenType.LessThanOrEqualTo.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0] <= parameters[1];
                });

                this.RegisterFunction(TokenType.And.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].AsBool && parameters[1].AsBool;
                });

                this.RegisterFunction(TokenType.Or.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].AsBool || parameters[1].AsBool;
                });

                this.RegisterFunction(TokenType.Xor.ToString(), 2, delegate(Value[] parameters) {
                    return parameters[0].AsBool ^ parameters[1].AsBool;
                });

                this.RegisterFunction(TokenType.Not.ToString(), 1, delegate(Value[] parameters) {
                    return !parameters[0].AsBool;
                });

                #endregion Operators
			}
		}

    }
}
