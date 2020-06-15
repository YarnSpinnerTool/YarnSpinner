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
using System.Text;
using System.Text.RegularExpressions;
using Yarn.MarkupParsing;

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
    /// 3. Use <see cref="Dialogue.ParseMarkup(string)"/>
    /// to parse all markup in the line.
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
        /// The values that should be inserted into the user-facing text
        /// before delivery.
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
            internal Option(Line line, int id, string destinationNode)
            {
                Line = line;
                ID = id;
                DestinationNode = destinationNode;
            }

            /// <summary>
            /// Gets the <see cref="Line"/> that should be presented to the
            /// user for this option.
            /// </summary>
            /// <remarks>
            /// See the documentation for the <see cref="Yarn.Line"/> class
            /// for information on how to prepare a line before presenting
            /// it to the user. 
            /// </remarks>
            public Line Line {get; private set;}

            /// <summary>
            /// Gets the identifying number for this option.
            /// </summary>
            /// <remarks>
            /// When the user selects this option, this value should be
            /// used as the parameter for <see
            /// cref="Dialogue.SetSelectedOption(int)"/>.
            /// </remarks>
            public int ID {get; private set;}

            /// <summary>
            /// Gets the name of the node that will be run if this option
            /// is selected.
            /// </summary>
            /// <remarks>
            /// The value of this property not be valid if this is a
            /// shortcut option.
            /// </remarks>
            public string DestinationNode { get; private set; }
        }
        
        /// <summary>
        /// Gets the <see cref="Option"/>s that should be presented to the
        /// user.
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
    /// Represents a method that receives diagnostic messages and error
    /// information from a <see cref="Dialogue"/>.
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
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="value">The value to store.</param>
        void SetValue(string variableName, Value value);

        // some convenience setters

        /// <summary>
        /// Stores a <see cref="string"/> as a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="stringValue">The string to store.</param>
        void SetValue(string variableName, string stringValue);

        /// <summary>
        /// Stores a <see cref="float"/> as a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="floatValue">The number to store.</param>
        void SetValue(string variableName, float floatValue);

        /// <summary>
        /// Stores a <see cref="bool"/> as a <see cref="Value"/>.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
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
    public class Dialogue : IAttributeMarkerProcessor {

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
            /// Indicates that the <see cref="Dialogue"/> should suspend
            /// execution.
            /// </summary>
            PauseExecution,

            /// <summary>
            /// Indicates that the <see cref="Dialogue"/> should continue
            /// execution.
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
        /// <seealso cref="NodeStartHandler"/>
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
        /// <seealso cref="NodeStartHandler"/>
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
        /// <seealso cref="NodeStartHandler"/>
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
        /// <seealso cref="NodeStartHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate HandlerExecutionType NodeCompleteHandler(string completedNodeName);

        /// <summary>
        /// Represents the method that is called when the Dialogue begins
        /// executing a node.
        /// </summary>
        /// <param name="startedNodeName">The name of the node.</param>
        /// <returns>Whether the <see cref="Dialogue"/> should suspend
        /// execution after this method has been called.</returns>
        /// <seealso cref="HandlerExecutionType"/>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate HandlerExecutionType NodeStartHandler(string startedNodeName);

        /// <summary>
        /// Represents the method that is called when the dialogue has
        /// reached its end, and no more code remains to be run.
        /// </summary>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeStartHandler"/>
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
        /// Gets or sets the <see cref="Dialogue"/>'s locale, as an IETF
        /// BCP 47 code.
        /// </summary>
        /// <remarks>
        /// This code is used to determine how the `plural` and `ordinal`
        /// markers determine the plural class of numbers.
        ///
        /// For example, the code "en-US" represents the English language
        /// as used in the United States.
        /// </remarks>
        public string LanguageCode { get; set; }

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
        /// Gets or sets the <see cref="NodeStartHandler"/> that is called
        /// when a node is started.
        /// </summary>
        public NodeStartHandler nodeStartHandler
        {
            get => vm.nodeStartHandler;
            set => vm.nodeStartHandler = value;
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
        /// <param name="variableStorage">The <see cref="VariableStorage"/>
        /// that this Dialogue should use.</param>
        public Dialogue(Yarn.VariableStorage variableStorage)
        {
            this.variableStorage = variableStorage ?? throw new ArgumentNullException(nameof(variableStorage));
            library = new Library();

            this.vm = new VirtualMachine(this);

            library.ImportLibrary(new StandardLibrary());

            lineParser = new LineParser();

            lineParser.RegisterMarkerProcessor("select", this);
            lineParser.RegisterMarkerProcessor("plural", this);
            lineParser.RegisterMarkerProcessor("ordinal", this);
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
        /// Prepares the <see cref="Dialogue"/> that the user intends to
        /// start running a node.
        /// </summary>
        /// <param name="startNode">The name of the node that will be run.
        /// The node have been loaded by calling <see
        /// cref="SetProgram(Program)"/> or <see
        /// cref="AddProgram(Program)"/>.</param>
        /// <remarks>
        /// After this method is called, you call <see cref="Continue"/> to
        /// start executing it.
        /// </remarks>
        /// <throws cref="DialogueException">Thrown when no node named
        /// `startNode` has been loaded.</throws>
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
        /// This method repeatedly executes instructions until one of the
        /// following conditions is encountered:
        /// 
        /// * The <see cref="lineHandler"/>, <see cref="commandHandler"/>,
        /// or <see cref="nodeCompleteHandler"/> return <see
        /// cref="HandlerExecutionType.PauseExecution"/>.
        /// * The <see cref="optionsHandler"/> is called. When this occurs,
        /// the Dialogue is waiting for the user to specify which of the
        /// options has been selected, and <see
        /// cref="SetSelectedOption(int)"/> must be called before <see
        /// cref="Continue"/> is called again.)
        /// * The Program reaches its end. When this occurs, <see
        /// cref="SetNode(string)"/> must be called before <see
        /// cref="Continue"/> is called again.
        /// * An error occurs while executing the Program.
        ///
        /// This method has no effect if it is called while the <see
        /// cref="Dialogue"/> is currently in the process of executing
        /// instructions.
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

        private readonly LineParser lineParser;

        /// <summary>
        /// Parses a line of text, and produces a <see
        /// cref="MarkupParseResult"/>
        /// containing the results.
        /// </summary>
        /// <remarks>
        /// The <see cref="MarkupParseResult"/>'s <see
        /// cref="MarkupParseResult.Text"/> will have any `select`,
        /// `plural` or `ordinal` [format function]({{|ref
        /// "syntax.md#format-functions"|}}) markers replaced with the
        /// appropriate text, following this <see cref="Dialogue"/>'s <see
        /// cref="LanguageCode"/>.
        /// </remarks>
        /// <param name="line">The line of text to parse.</param>
        /// <returns>The results of parsing the markup.</returns>
        public MarkupParseResult ParseMarkup(string line)
        {
            return this.lineParser.ParseMarkup(line);
        }

        /// <summary>
        /// A regex that matches any `%` as long as it's not preceded by a `\`.
        /// </summary>
        private static readonly Regex ValuePlaceholderRegex = new Regex(@"(?<!\\)%");

        /// <summary></summary>
        /// <param name="marker">The marker to generate replacement text for.</param>
        /// <returns>The replacement text for the marker.</returns>
        /// <throws cref="InvalidOperationException"></throws>
        /// <throws cref="KeyNotFoundException"></throws>
        /// <throws cref="ArgumentException">Thrown when the string
        /// contains a `plural` or `ordinal` format function, but the
        /// specified value cannot be parsed as a number.</throws>
        string IAttributeMarkerProcessor.ReplacementTextForMarker(MarkupAttributeMarker marker)
        {

            if (marker.TryGetProperty("value", out var valueProp) == false)
            {
                throw new KeyNotFoundException("Expected a property \"value\"");
            }

            var value = valueProp.ToString();

            // Apply the "select" format function
            if (marker.Name == "select")
            {
                if (!marker.TryGetProperty(value, out var replacementProp))
                {
                    throw new KeyNotFoundException($"error: no replacement for {value}");
                }
                
                string replacement = replacementProp.ToString();
                replacement = ValuePlaceholderRegex.Replace(replacement, value);
                return replacement;                
            }

            // If it's not "select", then it's "plural" or "ordinal"

            // First, ensure that we have a locale code set
            if (this.LanguageCode == null) 
            {
                throw new InvalidOperationException("Dialogue locale code is not set. 'plural' and 'ordinal' markers cannot be called unless one is set.");
            }

            // Attempt to parse the value as a double, so we can determine
            // its plural class
            if (double.TryParse(value, out var doubleValue) == false)
            {
                throw new ArgumentException($"Error while pluralising line: '{value}' is not a number");
            }

            CLDRPlurals.PluralCase pluralCase;

            switch (marker.Name)
            {
                case "plural":
                    pluralCase = CLDRPlurals.NumberPlurals.GetCardinalPluralCase(this.LanguageCode, doubleValue);
                    break;
                case "ordinal":
                    pluralCase = CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(this.LanguageCode, doubleValue);
                    break;
                default:
                    throw new InvalidOperationException($"Invalid marker name {marker.Name}");
            }

            string pluralCaseName = pluralCase.ToString().ToLowerInvariant();

            // Now that we know the plural case, we can select the appropriate replacement text for it
            if (!marker.TryGetProperty(pluralCaseName, out var replacementValue))
            {
                throw new KeyNotFoundException($"error: no replacement for {value}'s plural case of {pluralCaseName}");
            }
            
            string input = replacementValue.ToString();
            return ValuePlaceholderRegex.Replace(input, value);
        
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
