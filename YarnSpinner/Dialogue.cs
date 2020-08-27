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
using Yarn.Markup;

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
    /// 2. Use <see cref="Dialogue.ExpandSubstitutions"/> to replace all
    /// substitutions in the user-facing text.
    ///
    /// 3. Use <see cref="Dialogue.ParseMarkup"/>
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
        bool TryGetValue<T>(string variableName, out T result);

        T GetValue<T>(string variableName);

        /// <summary>
        /// Removes all variables from storage.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// A simple concrete subclass of <see cref="BaseVariableStorage"/> that
    /// keeps all variables in memory.
    /// </summary>
    public class MemoryVariableStore : VariableStorage
    {

        private Dictionary<string, object> variables = new Dictionary<string, object>();

        /// <inheritdoc/>
        public bool TryGetValue<T>(string variableName, out T result)
        {
            if (variables.TryGetValue(variableName, out var foundValue)) {
                if (typeof(T).IsAssignableFrom(foundValue.GetType())) {
                    result = (T)foundValue;
                    return true;
                } else {
                    throw new ArgumentException($"Variable {variableName} is present, but is of type {foundValue.GetType()}, not {typeof(T)}");
                }
            }
            result = default;
            return false;
        }

        public T GetValue<T>(string variableName)
        {
            var foundValue = variables[variableName];

            if (typeof(T).IsAssignableFrom(foundValue.GetType()))
            {
                return (T)foundValue;
            }
            else
            {
                throw new ArgumentException($"Variable {variableName} is present, but is of type {foundValue.GetType()}, not {typeof(T)}");
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            variables.Clear();
        }

        public void SetValue(string variableName, string stringValue)
        {
            variables[variableName] = stringValue;
        }

        public void SetValue(string variableName, float floatValue)
        {
            variables[variableName] = floatValue;
        }

        public void SetValue(string variableName, bool boolValue)
        {
            variables[variableName] = boolValue;
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
        /// Represents the method that is called when the Dialogue delivers
        /// a <see cref="Line"/>.
        /// </summary>
        /// <param name="line">The <see cref="Line"/> that has been
        /// delivered.</param>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeStartHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate void LineHandler(Line line);

        /// <summary>
        /// Represents the method that is called when the Dialogue delivers
        /// an <see cref="OptionSet"/>.
        /// </summary>
        /// <param name="options">The <see cref="OptionSet"/> that has been
        /// delivered.</param>
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
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="NodeStartHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate void CommandHandler(Command command);

        /// <summary>
        /// Represents the method that is called when the Dialogue reaches
        /// the end of a node.
        /// </summary>
        /// <param name="completedNodeName">The name of the node.</param>
        /// <remarks>
        /// This method may be called multiple times over the course of
        /// code execution. A node being complete does not necessarily
        /// represent the end of the conversation.
        /// </remarks>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeStartHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate void NodeCompleteHandler(string completedNodeName);

        /// <summary>
        /// Represents the method that is called when the Dialogue begins
        /// executing a node.
        /// </summary>
        /// <param name="startedNodeName">The name of the node.</param>
        /// <seealso cref="LineHandler"/>
        /// <seealso cref="OptionsHandler"/>
        /// <seealso cref="CommandHandler"/>
        /// <seealso cref="NodeCompleteHandler"/>
        /// <seealso cref="DialogueCompleteHandler"/>
        public delegate void NodeStartHandler(string startedNodeName);

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
        /// Represents the method that is called when the dialogue
        /// anticipates that it will deliver lines.
        /// </summary>
        /// <remarks>
        /// This method should begin preparing to run the lines. For
        /// example, if a game delivers dialogue via voice-over, the
        /// appropriate audio files should be loaded.
        ///
        /// This method serves to provide a hint to the game that a line
        /// _may_ be run. Not every line indicated in <paramref
        /// ref="lineIDs"/> may end up actually running.
        ///
        /// This method may be called any number of times during a dialogue
        /// session.
        /// </remarks>
        /// <param name="lineIDs">The collection of line IDs that may be
        /// delivered at some point soon.</param>
        public delegate void PrepareForLinesHandler(IEnumerable<string> lineIDs);

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

        /// <summary>
        /// Gets or sets the <see cref="PrepareForLinesHandler"/> that is
        /// called when the dialogue anticipates delivering some lines.
        /// </summary>
        /// <value></value>
        public PrepareForLinesHandler prepareForLinesHandler
        {
            get => vm.prepareForLinesHandler;
            set => vm.prepareForLinesHandler = value;
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
        ///
        /// If <see cref="prepareForLinesHandler"/> has been set, it may be
        /// called when this method is invoked, as the Dialogue determines
        /// which lines may be delivered during the <paramref
        /// name="startNode"/> node's execution.
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
        /// * The <see cref="lineHandler"/> or <see cref="commandHandler"/>
        /// is called. After calling either of these handlers, the Dialogue
        /// will wait until <see cref="Continue"/> is called. Continue may
        /// be called from inside the <see cref="lineHandler"/> or <see
        /// cref="commandHandler"/>, or may be called at any future time. *
        /// The <see cref="optionsHandler"/> is called. When this occurs,
        /// the Dialogue is waiting for the user to specify which of the
        /// options has been selected, and <see
        /// cref="SetSelectedOption(int)"/> must be called before <see
        /// cref="Continue"/> is called again.) * The Program reaches its
        /// end. When this occurs, <see cref="SetNode(string)"/> must be
        /// called before <see cref="Continue"/> is called again. * An
        /// error occurs while executing the Program.
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
        /// `plural` or `ordinal` markers replaced with the
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
        /// Replaces all substitution markers in a text with the given
        /// substitution list.
        /// </summary>
        /// <remarks>
        /// This method replaces substitution markers - for example, `{0}`
        /// - with the corresponding entry in <paramref
        /// name="substitutions"/>. If <paramref name="text"/> contains a
        /// substitution marker whose index is not present in <paramref
        /// name="substitutions"/>, it is ignored.
        /// </remarks>
        /// <param name="text">The text containing substitution
        /// markers.</param>
        /// <param name="substitutions">The list of substitutions.</param>
        /// <returns><paramref name="text"/>, with the content from
        /// <paramref name="substitutions"/> inserted.</returns>
        public static string ExpandSubstitutions(string text, IList<string> substitutions)
        {
            for (int i = 0; i < substitutions.Count; i++) {
                string substitution = substitutions[i];
                text = text.Replace("{" + i + "}", substitution);
            }

            return text;
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
        /// contains a `plural` or `ordinal` marker, but the
        /// specified value cannot be parsed as a number.</throws>
        string IAttributeMarkerProcessor.ReplacementTextForMarker(MarkupAttributeMarker marker)
        {

            if (marker.TryGetProperty("value", out var valueProp) == false)
            {
                throw new KeyNotFoundException("Expected a property \"value\"");
            }

            var value = valueProp.ToString();

            // Apply the "select" marker
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

                this.RegisterFunction(TokenType.Add.ToString(), delegate(Value a, Value b) {
                    return a + b;
                });

                this.RegisterFunction(TokenType.Minus.ToString(), delegate(Value a, Value b) {
                    return a - b;
                });

                this.RegisterFunction(TokenType.UnaryMinus.ToString(), delegate(Value a) {
                    return -a;
                });

                this.RegisterFunction(TokenType.Divide.ToString(), delegate(Value a, Value b) {
                    return a / b;
                });

                this.RegisterFunction(TokenType.Multiply.ToString(), delegate(Value a, Value b) {
                    return a * b;
                });

                this.RegisterFunction(TokenType.Modulo.ToString(), delegate(Value a, Value b) {
                    return a % b;
                });

                this.RegisterFunction(TokenType.EqualTo.ToString(), delegate(Value a, Value b) {
                    return a.Equals(b);
                });

                this.RegisterFunction(TokenType.NotEqualTo.ToString(), delegate(Value a, Value b) {

                    // Return the logical negative of the == operator's result
                    return !a.Equals(b);
                });

                this.RegisterFunction(TokenType.GreaterThan.ToString(), delegate(Value a, Value b) {
                    return a > b;
                });

                this.RegisterFunction(TokenType.GreaterThanOrEqualTo.ToString(), delegate(Value a, Value b) {
                    return a >= b;
                });

                this.RegisterFunction(TokenType.LessThan.ToString(), delegate(Value a, Value b) {
                    return a < b;
                });

                this.RegisterFunction(TokenType.LessThanOrEqualTo.ToString(), delegate(Value a, Value b) {
                    return a <= b;
                });

                this.RegisterFunction(TokenType.And.ToString(), delegate(Value a, Value b) {
                    return a.ConvertTo<bool>() && b.ConvertTo<bool>();
                });

                this.RegisterFunction(TokenType.Or.ToString(), delegate(Value a, Value b) {
                    return a.ConvertTo<bool>() || b.ConvertTo<bool>();
                });

                this.RegisterFunction(TokenType.Xor.ToString(), delegate(Value a, Value b) {
                    return a.ConvertTo<bool>() ^ b.ConvertTo<bool>();
                });

                this.RegisterFunction(TokenType.Not.ToString(), delegate(Value a) {
                    return !a.ConvertTo<bool>();
                });

                this.RegisterFunction("string", delegate(Value v) {
                    return v.ConvertTo<string>();
                });

                this.RegisterFunction("number", delegate(Value v) {
                    return v.ConvertTo<float>();
                });

                this.RegisterFunction("bool", delegate(Value v) {
                    return v.ConvertTo<bool>();
                });
                

                #endregion Operators
			}
		}

    }
}
