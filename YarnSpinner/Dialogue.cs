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

namespace Yarn
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.RegularExpressions;
    using Yarn.Markup;

    /// <summary>
    /// A line of dialogue, sent from the <see cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// <para>When the game receives a <see cref="Line"/>, it should do the
    /// following things to prepare the line for presentation to the user.
    /// </para>
    /// <list type="number">
    /// <item>Use the value in the <see cref="ID"/> field to look up the
    /// appropriate user-facing text in the string table. </item>
    ///
    /// <item>Use <see cref="Dialogue.ExpandSubstitutions"/> to replace all
    /// substitutions in the user-facing text.</item>
    ///
    /// <item>Use <see cref="Dialogue.ParseMarkup"/> to parse all markup in the
    /// line.</item>
    /// </list>
    ///
    /// <para>You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.</para>
    /// </remarks>
    /// <seealso cref="Dialogue.LineHandler"/>
    #pragma warning disable CA1815
    public struct Line
    {
        internal Line(string stringID) : this()
        {
            this.ID = stringID;
            this.Substitutions = Array.Empty<string>();
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
    #pragma warning restore CA1815

    /// <summary>
    /// A set of <see cref="OptionSet.Option"/>s, sent from the <see
    /// cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.OptionsHandler"/>
    #pragma warning disable CA1815
    public struct OptionSet
    {
        internal OptionSet(Option[] options)
        {
            Options = options;
        }

        #pragma warning disable CA1716
        /// <summary>
        /// An option to be presented to the user.
        /// </summary>
        public struct Option
        {
            internal Option(Line line, int id, int destinationInstruction, bool isAvailable)
            {
                Line = line;
                ID = id;
                DestinationInstruction = destinationInstruction;
                IsAvailable = isAvailable;
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
            public Line Line { get; private set; }

            /// <summary>
            /// Gets the identifying number for this option.
            /// </summary>
            /// <remarks>
            /// When the user selects this option, this value should be
            /// used as the parameter for <see
            /// cref="Dialogue.SetSelectedOption(int)"/>.
            /// </remarks>
            public int ID { get; private set; }

            /// <summary>
            /// Gets the name of the node that will be run if this option
            /// is selected.
            /// </summary>
            /// <remarks>
            /// The value of this property not be valid if this is a
            /// shortcut option.
            /// </remarks>
            internal int DestinationInstruction { get; set; }

            /// <summary>
            /// Gets a value indicating whether the player should be
            /// permitted to select this option.
            /// </summary>
            /// <remarks>
            /// <para>
            /// If this value is <see langword="false"/>, this option had a
            /// line condition on it that failed. The option will still be
            /// delivered to the game, but, depending on the needs of the
            /// game, the game may decide to not allow the player to select
            /// it, or not offer it to the player at all.
            /// </para>
            /// <para>
            /// This is intended for situations where games wish to show
            /// options that the player _could_ have taken, if some other
            /// condition had been met (e.g. having enough "charisma"
            /// points).
            /// </para>
            /// </remarks>
            public bool IsAvailable { get; private set; }
        }
        #pragma warning restore CA1716

        /// <summary>
        /// Gets the <see cref="Option"/>s that should be presented to the
        /// user.
        /// </summary>
        /// <seealso cref="Option"/>
        public Option[] Options { get; private set; }
    }
    #pragma warning restore CA1815

    /// <summary>
    /// A command, sent from the <see cref="Dialogue"/> to the game.
    /// </summary>
    /// <remarks>
    /// You do not create instances of this struct yourself. They are
    /// created by the <see cref="Dialogue"/> during program execution.
    /// </remarks>
    /// <seealso cref="Dialogue.CommandHandler"/>    
    #pragma warning disable CA1815
    public struct Command
    {
        internal Command(string text)
        {
            Text = text;
        }

        /// <summary>
        /// Gets the text of the command.
        /// </summary>
        public string Text { get; private set; }
    }
    #pragma warning restore CA1815

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

    /// <summary>
    /// Represents different kinds of variables that can be fetched from a <see
    /// cref="Dialogue"/> using <see cref="IVariableAccess.TryGetValue{T}(string, out T)"/>.
    /// </summary>
    public enum VariableKind {
        /// <summary>
        /// The kind of the variable cannot be determined. It may not be known
        /// to the system.
        /// </summary>
        Unknown,
        /// <summary>
        /// The variable's value is stored in memory, and may be persisted to
        /// disk.
        /// </summary>
        Stored,
        /// <summary>
        /// The variable's value is computed at run-time, and is not persisted
        /// to disk. 
        /// </summary>
        Smart
    }

    /// <summary>Provides a mechanism for retrieving values.</summary>
    public interface IVariableAccess {

        /// <summary>
        /// Given a variable name, attempts to fetch a value for the variable,
        /// either from storage, initial values found in <see cref="Program"/>,
        /// or by evaluating a smart variable found in <see cref="Program"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value to return. The fetched
        /// value will be converted to this type, if possible.</typeparam>
        /// <param name="variableName">The name of the variable.</param>
        /// <param name="result">If this method returns <see langword="true"/>,
        /// this parameter will contain the fetched value.</param>
        /// <returns><see langword="true"/> if a value could be fetched; <see
        /// langword="false"/> otherwise.</returns>
        bool TryGetValue<T>(string variableName, out T result);

        /// <summary>
        /// Gets the kind of variable named <paramref name="name"/>.
        /// </summary>
        /// <param name="name">The name of the variable.</param>
        /// <returns>A <see cref="VariableKind"/> enum representing the kind of
        /// the variable named <paramref name="name"/>.</returns>
        VariableKind GetVariableKind(string name);

        /// <summary>
        /// Gets or sets the Yarn <see cref="Program"/> that stores information
        /// about the initial values of variables, and is able to produce values
        /// for smart variables.
        /// </summary>
        Program Program { get; set; }

        /// <summary>
        /// Gets or sets the object to use when evaluating smart variables.
        /// </summary>
        ISmartVariableEvaluator SmartVariableEvaluator { get; set; }
    }

    /// <summary>Provides a mechanism for storing values.</summary>
    public interface IVariableStorage : IVariableAccess
    {
        /// <summary>
        /// Stores a <see cref="string"/> in this VariableStorage.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="stringValue">The string to store.</param>
        void SetValue(string variableName, string stringValue);

        /// <summary>
        /// Stores a <see cref="float"/> in this VariableStorage.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="floatValue">The number to store.</param>
        void SetValue(string variableName, float floatValue);

        /// <summary>
        /// Stores a <see cref="bool"/> in this VariableStorage.
        /// </summary>
        /// <param name="variableName">The name to associate with this
        /// variable.</param>
        /// <param name="boolValue">The boolean value to store.</param>
        void SetValue(string variableName, bool boolValue);

        /// <summary>
        /// Removes all variables from storage.
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// A simple concrete implementation of <see cref="IVariableStorage"/>
    /// that keeps all variables in memory.
    /// </summary>
    public class MemoryVariableStore : IVariableStorage
    {
        private Dictionary<string, object> variables = new Dictionary<string, object>();

        private static bool TryGetAsType<T>(Dictionary<string, object> dictionary, string key, out T result)
        {
            if (dictionary.TryGetValue(key, out var objectResult) == true
                && typeof(T).IsAssignableFrom(objectResult.GetType()))
            {
                result = (T)objectResult;
                return true;
            }

            result = default;
            return false;
        }

        /// <inheritdoc/>
        public Program Program { get; set; }

        /// <inheritdoc/>
        public ISmartVariableEvaluator SmartVariableEvaluator { get; set; }
    
        public virtual bool TryGetValue<T>(string variableName, out T result) {
            switch (GetVariableKind(variableName))
            {
                case VariableKind.Stored:
                    // This is a stored value. First, attempt to fetch it from the
                    // variable storage.

                    // Try to get the value from the dictionary, and check to see that it's the 
                    if (TryGetAsType(variables, variableName, out result)) {
                        // We successfully fetched it from storage.
                        return true;
                    } else {
                        // We didn't fetch it from storage. Fall back to the
                        // program's initial value storage.
                        return Program.TryGetInitialValue(variableName, out result);
                    }
                case VariableKind.Smart:
                    // The variable is a smart variable. Ask our smart variable
                    // evaluator.
                    return this.SmartVariableEvaluator.TryGetSmartVariable(variableName, out result);
                case VariableKind.Unknown:
                default:
                    // The variable is not known.
                    result = default;
                    return false;
            }
        }

        /// <inheritdoc/>
        public void Clear()
        {
            this.variables.Clear();
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, string stringValue)
        {
            this.variables[variableName] = stringValue;
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, float floatValue)
        {
            this.variables[variableName] = floatValue;
        }

        /// <inheritdoc/>
        public virtual void SetValue(string variableName, bool boolValue)
        {
            this.variables[variableName] = boolValue;
        }

        public VariableKind GetVariableKind(string name)
        {
            // Does this variable exist in our stored values?
            if (this.variables.ContainsKey(name)) {
                return VariableKind.Stored;
            }
            if (this.Program == null) {
                // We don't have a Program, so we can't ask it for other
                // information.
                return VariableKind.Unknown;
            }
            // Ask our Program about it. It will be able to tell if the variable
            // is stored, smart, or unknown.
            return this.Program.GetVariableKind(name);
        }
    }

    /// <summary>
    /// Represents the method that is called when the Dialogue delivers a <see
    /// cref="Line"/>.
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
    /// Represents the method that is called when the Dialogue delivers an <see
    /// cref="OptionSet"/>.
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
    /// Represents the method that is called when the Dialogue delivers a <see
    /// cref="Command"/>.
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
    /// Represents the method that is called when the Dialogue reaches the end
    /// of a node.
    /// </summary>
    /// <param name="completedNodeName">The name of the node.</param>
    /// <remarks>
    /// This method may be called multiple times over the course of code
    /// execution. A node being complete does not necessarily represent the end
    /// of the conversation.
    /// </remarks>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void NodeCompleteHandler(string completedNodeName);

    /// <summary>
    /// Represents the method that is called when the Dialogue begins executing
    /// a node.
    /// </summary>
    /// <param name="startedNodeName">The name of the node.</param>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    /// <seealso cref="DialogueCompleteHandler"/>
    public delegate void NodeStartHandler(string startedNodeName);

    /// <summary>
    /// Represents the method that is called when the dialogue has reached its
    /// end, and no more code remains to be run.
    /// </summary>
    /// <seealso cref="LineHandler"/>
    /// <seealso cref="OptionsHandler"/>
    /// <seealso cref="CommandHandler"/>
    /// <seealso cref="NodeStartHandler"/>
    /// <seealso cref="NodeCompleteHandler"/>
    public delegate void DialogueCompleteHandler();

    /// <summary>
    /// Represents the method that is called when the dialogue anticipates that
    /// it will deliver lines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method should begin preparing to run the lines. For example, if a
    /// game delivers dialogue via voice-over, the appropriate audio files
    /// should be loaded.
    /// </para>
    /// <para>
    /// This method serves to provide a hint to the game that a line _may_ be
    /// run. Not every line indicated in <paramref name="lineIDs"/> may end up
    /// actually running.
    /// </para>
    /// <para>
    /// This method may be called any number of times during a dialogue session.
    /// </para>
    /// </remarks>
    /// <param name="lineIDs">The collection of line IDs that may be delivered
    /// at some point soon.</param>
    public delegate void PrepareForLinesHandler(IEnumerable<string> lineIDs);

    /// <summary>
    /// Co-ordinates the execution of Yarn programs.
    /// </summary>
    public class Dialogue : IAttributeMarkerProcessor, ISmartVariableEvaluator
    {

        /// <summary>
        /// Gets or sets the object that provides access to storing and
        /// retrieving the values of variables.
        /// </summary>
        public IVariableStorage VariableStorage { get; set; }

        /// <summary>
        /// Invoked when the Dialogue needs to report debugging
        /// information.
        /// </summary>
        public Logger LogDebugMessage { get; set; }

        /// <summary>
        /// Invoked when the Dialogue needs to report an error.
        /// </summary>
        public Logger LogErrorMessage { get; set; }

        /// <summary>The node that execution will start from.</summary>
        public const string DefaultStartNodeName = "Start";

        private Program program;

        /// <summary>Gets or sets the compiled Yarn program.</summary>
        internal Program Program
        {
            get => program;
            set
            {
                program = value;

                vm.Program = value;
                vm.ResetState();
                
                smartVariableVM.Program = value;
                smartVariableVM.ResetState();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the Dialogue is currently executing
        /// Yarn instructions.
        /// </summary>
        public bool IsActive => vm.CurrentExecutionState != VirtualMachine.ExecutionState.Stopped;

        /// <summary>
        /// Gets or sets the <see cref="Yarn.LineHandler"/> that is called when
        /// a line is ready to be shown to the user.
        /// </summary>
        public LineHandler LineHandler
        {
            get => vm.LineHandler;
            set => vm.LineHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Dialogue"/>'s locale, as an IETF BCP 47
        /// code.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This code is used to determine how the <c>plural</c> and <c>ordinal</c>
        /// markers determine the plural class of numbers.
        /// </para>
        /// <para>
        /// For example, the code "en-US" represents the English language as
        /// used in the United States.
        /// </para>
        /// </remarks>
        public string LanguageCode { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.OptionsHandler"/> that is called
        /// when a set of options are ready to be shown to the user.
        /// </summary>
        /// <remarks>
        /// The Options Handler delivers an <see cref="OptionSet"/> to the game.
        /// Before <see cref="Continue"/> can be called to resume execution,
        /// <see cref="SetSelectedOption"/> must be called to indicate which
        /// <see cref="OptionSet.Option"/> was selected by the user. If <see
        /// cref="SetSelectedOption"/> is not called, an exception is thrown.
        /// </remarks>
        public OptionsHandler OptionsHandler
        {
            get => vm.OptionsHandler;
            set => vm.OptionsHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.CommandHandler"/> that is called
        /// when a command is to be delivered to the game.
        /// </summary>
        public CommandHandler CommandHandler
        {
            get => vm.CommandHandler;
            set => vm.CommandHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.NodeStartHandler"/> that is called
        /// when a node is started.
        /// </summary>
        public NodeStartHandler NodeStartHandler
        {
            get => vm.NodeStartHandler;
            set => vm.NodeStartHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.NodeCompleteHandler"/> that is
        /// called when a node is complete.
        /// </summary>
        public NodeCompleteHandler NodeCompleteHandler
        {
            get => vm.NodeCompleteHandler;
            set => vm.NodeCompleteHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="Yarn.DialogueCompleteHandler"/> that is
        /// called when the dialogue reaches its end.
        /// </summary>
        public DialogueCompleteHandler DialogueCompleteHandler
        {
            get => vm.DialogueCompleteHandler;
            set => vm.DialogueCompleteHandler = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="PrepareForLinesHandler"/> that is called
        /// when the dialogue anticipates delivering some lines.
        /// </summary>
        /// <value></value>
        public PrepareForLinesHandler PrepareForLinesHandler
        {
            get => vm.PrepareForLinesHandler;
            set => vm.PrepareForLinesHandler = value;
        }

        /// <summary>
        /// The virtual machine to use when running dialogue.
        /// </summary>
        private VirtualMachine vm;

        /// <summary>
        /// The virtual machine to use when evaluating smart variables.
        /// </summary>
        /// <remarks>
        /// This is kept separate from the main VM in order to prevent
        /// evaluating smart variables from modifying the evaluation state of
        /// dialogue.
        /// </remarks>
        private VirtualMachine smartVariableVM;

        /// <summary>
        /// Gets the <see cref="Yarn.Library"/> that this Dialogue uses to
        /// locate functions.
        /// </summary>
        /// <remarks>
        /// When the Dialogue is constructed, the Library is initialized with
        /// the built-in operators like <c>+</c>, <c>-</c>, and so on.
        /// </remarks>
        public Library Library { get; internal set; }

        /// <summary>
        /// Gets or sets the content saliency strategy used by this <see cref="Dialogue"/>.
        /// </summary>
        /// <remarks>
        /// A content saliency strategy is a class that implements <see
        /// cref="Yarn.Saliency.IContentSaliencyStrategy"/> and selects the most
        /// appropriate content in a line group, or any other situation where
        /// content saliency is relevant.
        /// </remarks>
        public Saliency.IContentSaliencyStrategy ContentSaliencyStrategy { 
            get => this.vm.ContentSaliencyStrategy; 
            set => this.vm.ContentSaliencyStrategy = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dialogue"/> class.
        /// </summary>
        /// <param name="variableStorage">The <see
        /// cref="Yarn.IVariableStorage"/> that this Dialogue should
        /// use.</param>
        public Dialogue(Yarn.IVariableStorage variableStorage)
        {
            Library = new Library();
            
            this.VariableStorage = variableStorage ?? throw new ArgumentNullException(nameof(variableStorage));
            this.VariableStorage.SmartVariableEvaluator = this;
            this.VariableStorage.Program = this.Program;

            this.vm = new VirtualMachine(this.Library, this.VariableStorage);
            this.smartVariableVM = new VirtualMachine(this.Library, this.VariableStorage);

            Library.ImportLibrary(new StandardLibrary());

            Library.RegisterFunction("visited", delegate(string node){
                return IsNodeVisited(node);
            });
            Library.RegisterFunction("visited_count", delegate(string node){
                return GetNodeVisitCount(node);
            });

            lineParser = new LineParser();

            lineParser.RegisterMarkerProcessor("select", this);
            lineParser.RegisterMarkerProcessor("plural", this);
            lineParser.RegisterMarkerProcessor("ordinal", this);
        }

        /// <summary>
        /// Loads all nodes from the provided <see cref="Yarn.Program"/>.
        /// </summary>
        /// <remarks>
        /// This method replaces any existing nodes have been loaded.
        /// </remarks>
        /// <param name="program">The <see cref="Yarn.Program"/> to use.</param>
        public void SetProgram(Program program)
        {
            this.Program = program;
            this.VariableStorage.Program = program;
        }

        /// <summary>
        /// Loads a compiled <see cref="Yarn.Program"/> from a file.
        /// </summary>
        /// <param name="fileName">The path of the file to load.</param>
        /// <remarks>
        /// <para>
        /// This method replaces the current value of <see cref="Program"/> with
        /// the result of loading the file.
        /// </para>
        /// <para>
        /// This method does not compile Yarn source. To compile Yarn source
        /// code into a <see cref="Yarn.Program"/>, Refer to the Yarn compiler.
        /// </para>
        /// </remarks>
        internal void LoadProgram(string fileName)
        {
            var bytes = File.ReadAllBytes(fileName);

            this.Program = Program.Parser.ParseFrom(bytes);
        }

        /// <summary>
        /// Prepares the <see cref="Dialogue"/> that the user intends to start
        /// running a node.
        /// </summary>
        /// <param name="startNode">The name of the node that will be run. The
        /// node have been loaded by calling <see cref="SetProgram(Program)"/>
        /// or <see cref="AddProgram(Program)"/>.</param>
        /// <remarks>
        /// <para>
        /// After this method is called, you call <see cref="Continue"/> to
        /// start executing it.
        /// </para>
        /// <para>
        /// If <see cref="PrepareForLinesHandler"/> has been set, it may be
        /// called when this method is invoked, as the Dialogue determines which
        /// lines may be delivered during the <paramref name="startNode"/>
        /// node's execution.
        /// </para>
        /// </remarks>
        /// <throws cref="DialogueException">Thrown when no node named
        /// <c>startNode</c> has been loaded.</throws>
        public void SetNode(string startNode = DefaultStartNodeName)
        {
            this.vm.SetNode(startNode, clearState: true);
        }

        /// <summary>
        /// Signals to the <see cref="Dialogue"/> that the user has selected a
        /// specified <see cref="OptionSet.Option"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// After the Dialogue delivers an <see cref="OptionSet"/>, this method
        /// must be called before <see cref="Continue"/> is called.
        /// </para>
        /// <para>
        /// The ID number that should be passed as the parameter to this method
        /// should be the <see cref="OptionSet.Option.ID"/> field in the <see
        /// cref="OptionSet.Option"/> that represents the user's selection.
        /// </para>
        /// </remarks>
        /// <param name="selectedOptionID">The ID number of the Option that the
        /// user selected.</param>
        /// <throws cref="DialogueException">Thrown when the Dialogue is not
        /// expecting an option to be selected.</throws> <throws
        /// cref="ArgumentOutOfRangeException">Thrown when <c>selectedOptionID</c> is
        /// not a valid option ID.</throws>
        /// <seealso cref="Yarn.OptionsHandler"/>
        /// <seealso cref="OptionSet"/>
        /// <seealso cref="Continue"/>
        public void SetSelectedOption(int selectedOptionID)
        {
            this.vm.SetSelectedOption(selectedOptionID);
        }

        /// <summary>
        /// Starts, or continues, execution of the current Program.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method repeatedly executes instructions until one of the
        /// following conditions is encountered:
        /// </para>
        /// <list type="bullet">
        /// <item>The <see cref="LineHandler"/> or <see cref="CommandHandler"/>
        /// is called. After calling either of these handlers, the Dialogue will
        /// wait until <see cref="Continue"/> is called. Continue may be called
        /// from inside the <see cref="LineHandler"/> or <see
        /// cref="CommandHandler"/>, or may be called at any future time.</item>
        ///
        /// <item>The <see cref="OptionsHandler"/> is called. When this occurs,
        /// the Dialogue is waiting for the user to specify which of the options
        /// has been selected, and <see cref="SetSelectedOption(int)"/> must be
        /// called before <see cref="Continue"/> is called again.)</item>
        ///
        /// <item>The Program reaches its end. When this occurs, <see
        /// cref="SetNode(string)"/> must be called before <see
        /// cref="Continue"/> is called again.</item>
        ///
        /// <item>An error occurs while executing the Program.</item>
        /// </list>
        ///
        /// <para>This method has no effect if it is called while the <see
        /// cref="Dialogue"/> is currently in the process of executing
        /// instructions.</para>
        /// </remarks>
        /// <seealso cref="Yarn.LineHandler"/>
        /// <seealso cref="Yarn.OptionsHandler"/>
        /// <seealso cref="Yarn.CommandHandler"/>
        /// <seealso cref="Yarn.NodeCompleteHandler"/>
        /// <seealso cref="Yarn.DialogueCompleteHandler"/>
        public void Continue()
        {
            if (this.vm.CurrentExecutionState == VirtualMachine.ExecutionState.Running)
            {
                // Cannot 'continue' an already running VM.
                return;
            }

            this.vm.Continue();
        }

        /// <summary>
        /// Immediately stops the <see cref="Dialogue"/>.
        /// </summary>
        public void Stop()
        {
            if (this.vm != null)
            {
                this.vm.Stop();
            }
        }

        /// <summary>
        /// Gets the names of the nodes in the currently loaded Program.
        /// </summary>
        public IEnumerable<string> NodeNames
        {
            get
            {
                return this.Program.Nodes.Keys;
            }
        }

        /// <summary>
        /// Gets the name of the node that this Dialogue is currently executing.
        /// </summary>
        /// <remarks>If <see cref="Continue"/> has never been called, this value
        /// will be <see langword="null"/>.</remarks>
        public string CurrentNode
        {
            get
            {
                if (this.vm == null)
                {
                    return null;
                }
                else
                {
                    return this.vm.CurrentNodeName;
                }
            }
        }

        /// <summary>
        /// Returns the string ID that contains the original, uncompiled source
        /// text for a node.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The string ID.</returns>
        /// <remarks>
        /// <para>
        /// A node's source text will only be present in the string table if its
        /// <c>tags</c> header contains <c>rawText</c>.
        /// </para>
        /// <para>
        /// Because the <see cref="Dialogue"/> class is designed to be unaware
        /// of the contents of the string table, this method does not test to
        /// see if the string table contains an entry with the line ID. You will
        /// need to test for that yourself.
        /// </para>
        /// </remarks>
        public string GetStringIDForNode(string nodeName)
        {
            if (this.Program.Nodes.Count == 0)
            {
                this.LogErrorMessage?.Invoke("No nodes are loaded!");
                return null;
            }
            else if (this.Program.Nodes.ContainsKey(nodeName))
            {
                return "line:" + nodeName;
            }
            else
            {
                this.LogErrorMessage?.Invoke("No node named " + nodeName);
                return null;
            }
        }

        /// <summary>
        /// Returns the tags for the node <paramref name="nodeName"/>.
        /// </summary>
        /// <remarks>
        /// The tags for a node are defined by setting the <c>tags</c> header in
        /// the node's source code. This header must be a space-separated list.
        /// </remarks>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns>The node's tags, or <see langword="null"/> if the node is
        /// not present in the Program.</returns>
        public IEnumerable<string> GetTagsForNode(string nodeName)
        {
            if (this.Program.Nodes.Count == 0)
            {
                this.LogErrorMessage?.Invoke("No nodes are loaded!");
                return null;
            }
            else if (this.Program.Nodes.ContainsKey(nodeName))
            {
                return this.Program.GetTagsForNode(nodeName);
            }
            else
            {
                this.LogErrorMessage?.Invoke("No node named " + nodeName);
                return null;
            }
        }

        /// <summary>
        /// Unloads all nodes from the Dialogue.
        /// </summary>
        public void UnloadAll()
        {
            Program = null;
        }

        /// <summary>
        /// Gets a value indicating whether a specified node exists in the
        /// Program.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        /// <returns><see langword="true"/> if a node named <c>nodeName</c>
        /// exists in the Program, <see langword="false"/>
        /// otherwise.</returns>
        public bool NodeExists(string nodeName)
        {
            if (this.Program == null)
            {
                this.LogErrorMessage?.Invoke("Tried to call NodeExists, but no program has been loaded!");
                return false;
            }

            if (this.Program.Nodes == null || this.Program.Nodes.Count == 0)
            {
                // No nodes? Then this node doesn't exist.
                return false;
            }

            return this.Program.Nodes.ContainsKey(nodeName);
        }

        /// <summary>
        /// Begins analysis of the <see cref="Program"/> by the <paramref name="context"/>
        /// </summary>
        /// <param name="context">The Context that performs the analysis</param>
        public void Analyse(Analysis.Context context)
        {
            if (context == null)
            {
                // can't perform analysis on nothing
                return;
            }
            context.AddProgramToAnalysis(this.Program);
        }

        private readonly LineParser lineParser;

        /// <summary>
        /// Parses a line of text, and produces a <see
        /// cref="MarkupParseResult"/> containing the results.
        /// </summary>
        /// <remarks>
        /// The <see cref="MarkupParseResult"/>'s <see
        /// cref="MarkupParseResult.Text"/> will have any <c>select</c>,
        /// <c>plural</c> or <c>ordinal</c> markers replaced with the appropriate
        /// text, following this <see cref="Dialogue"/>'s <see
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
        /// This method replaces substitution markers - for example, <c>{0}</c>
        /// - with the corresponding entry in <paramref name="substitutions"/>.
        /// If <paramref name="text"/> contains a substitution marker whose
        /// index is not present in <paramref name="substitutions"/>, it is
        /// ignored.
        /// </remarks>
        /// <param name="text">The text containing substitution markers.</param>
        /// <param name="substitutions">The list of substitutions.</param>
        /// <returns><paramref name="text"/>, with the content from <paramref
        /// name="substitutions"/> inserted.</returns>
        public static string ExpandSubstitutions(string text, IList<string> substitutions)
        {
            if (substitutions == null)
            {
                // if we have no substitutions we want to just return the text as is
                return text;
            }
            if (text == null)
            {
                // we somehow have substitutions to apply but no text for them to be applied into?
                throw new ArgumentNullException($"{nameof(text)} is null. Cannot apply substitutions to an empty string");
            }

            for (int i = 0; i < substitutions.Count; i++)
            {
                string substitution = substitutions[i];
                text = text.Replace("{" + i + "}", substitution);
            }

            return text;
        }

        /// <summary>
        /// A regex that matches any <c>%</c> as long as it's not preceded by a
        /// <c>\</c>.
        /// </summary>
        private static readonly Regex ValuePlaceholderRegex = new Regex(@"(?<!\\)%");

        /// <summary>Returns the text that should be used to replace the
        /// contents of <paramref name="marker"/>.</summary>
        /// <param name="marker">The marker to generate replacement text
        /// for.</param>
        /// <returns>The replacement text for the marker.</returns>
        /// <throws cref="InvalidOperationException"></throws> <throws
        /// cref="KeyNotFoundException"></throws> <throws
        /// cref="ArgumentException">Thrown when the string contains a
        /// <c>plural</c> or <c>ordinal</c> marker, but the specified value cannot be
        /// parsed as a number.</throws>
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
            if (double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var doubleValue) == false)
            {
                throw new ArgumentException($"Error while pluralising line: '{value}' is not a number");
            }

            // CLDRPlurals only works with 'neutral' locale names (i.e. "en"),
            // not 'specific' locale names. We need to check to see if
            // this.LanguageCode is the name of a 'specific' locale name. If is,
            // we'll fetch its parent, which will be 'neutral', and use that.
            string languageCode;
            try
            {
                var culture = new System.Globalization.CultureInfo(this.LanguageCode);
                if (culture.IsNeutralCulture)
                {
                    languageCode = culture.Name;
                }
                else
                {
                    culture = culture.Parent;
                    if (culture != null) {
                        languageCode = culture.Name;
                    } else {
                        languageCode = this.LanguageCode;
                    }
                }
            }
            catch (System.Globalization.CultureNotFoundException)
            {
                // this.LanguageCode doesn't represent a known culture. Fall
                // back to using what the user provided.
                languageCode = this.LanguageCode;
            }

            CLDRPlurals.PluralCase pluralCase;

            switch (marker.Name)
            {
                case "plural":
                    pluralCase = CLDRPlurals.NumberPlurals.GetCardinalPluralCase(languageCode, doubleValue);
                    break;
                case "ordinal":
                    pluralCase = CLDRPlurals.NumberPlurals.GetOrdinalPluralCase(languageCode, doubleValue);
                    break;
                default:
                    throw new InvalidOperationException($"Invalid marker name {marker.Name}");
            }

            string pluralCaseName = pluralCase.ToString().ToUpperInvariant();

            // Now that we know the plural case, we can select the
            // appropriate replacement text for it
            if (!marker.TryGetProperty(pluralCaseName, out var replacementValue))
            {
                throw new KeyNotFoundException($"error: no replacement for {value}'s plural case of {pluralCaseName}");
            }

            string input = replacementValue.ToString();
            return ValuePlaceholderRegex.Replace(input, value);

        }

        private bool IsNodeVisited(string nodeName)
        {
            float count = 0;
            if (VariableStorage.TryGetValue<float>(Library.GenerateUniqueVisitedVariableForNode(nodeName), out count))
            {
                return count > 0;
            }
            return false;
        }
        private float GetNodeVisitCount(string nodeName)
        {
            float count = 0;
            VariableStorage.TryGetValue<float>(Library.GenerateUniqueVisitedVariableForNode(nodeName), out count);
            return count;
        }

        /// <inheritdoc />
        public bool TryGetSmartVariable<T>(string name, out T result)
        {
            return SmartVariableEvaluationVirtualMachine.TryGetSmartVariable(
                name,
                this.VariableStorage, 
                this.Library, 
                out result);
        }

        // The standard, built-in library of functions and operators.
        internal class StandardLibrary : Library
        {
            /// <summary>
            /// The internal random number generator used by functions like
            /// 'random' and 'dice'.
            /// </summary>
            private static readonly System.Random Random = new Random();

            public StandardLibrary()
            {
                #region Operators

                // Register the in-built conversion functions
                this.RegisterFunction("string", delegate(object v)
                {
                    return Convert.ToString(v);
                });

                this.RegisterFunction("number", delegate(object v)
                {
                    return Convert.ToSingle(v);
                });

                this.RegisterFunction("format_invariant", delegate (float v)
                {
                    return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                });

                this.RegisterFunction("bool", delegate(object v)
                {
                    return Convert.ToBoolean(v);
                });

                // Register the built-in types.
                this.RegisterMethods((TypeBase)Types.Number);
                this.RegisterMethods((TypeBase)Types.String);
                this.RegisterMethods((TypeBase)Types.Boolean);

                // NOTE: This is part of a workaround for supporting 'EqualTo'
                // in Enums. See note in TypeUtil.GetCanonicalNameForMethod.
                this.RegisterMethods(new EnumType("Enum", null, null));

                // Register the built-in utility functions

#pragma warning disable CA5394 // System.Random is cryptographically insecure
                this.RegisterFunction<float>("random", () =>
                {
                    return (float)Random.NextDouble();
                });

                this.RegisterFunction<float, float, float>("random_range", (float min, float max) =>
                {
                    return Random.Next((int)max - (int)min + 1) + min;
                });

                this.RegisterFunction<int, int>("dice", (int sides) =>
                {
                    return Random.Next(sides + 1);
                });
#pragma warning restore CA5394 // System.Random is cryptographically insecure

                this.RegisterFunction<float, int>("round", (float num) =>
                {
                    return (int)Math.Round(num);
                });

                this.RegisterFunction<float, int, float>("round_places", (float num, int places) =>
                {
                    return (float)Math.Round(num, places);
                });

                this.RegisterFunction<float, int>("floor", (float num) =>
                {
                    return (int)Math.Floor(num);
                });

                this.RegisterFunction<float, int>("ceil", (float num) =>
                {
                    return (int)Math.Ceiling(num);
                });

                this.RegisterFunction<float, int>("inc", (float value) =>
                {
                    if (Decimal(value) == 0)
                    {
                        return (int)(value + 1);
                    }
                    else
                    {
                        return (int)Math.Ceiling(value);
                    }
                });

                this.RegisterFunction<float, int>("dec", (float value) =>
                {
                    if (Decimal(value) == 0)
                    {
                        return (int)value - 1;
                    }
                    else
                    {
                        return (int)Math.Floor(value);
                    }
                });

                this.RegisterFunction<float, float>("decimal", Decimal);
                this.RegisterFunction<float, int>("int", Integer);

                this.RegisterFunction("format", delegate (string formatString, object argument)
                {
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture, formatString, argument);
                });

                #endregion Operators
            }

            private static float Decimal(float value) {
                return value - Integer(value);

            }
            private static int Integer(float value) {
                return (int)Math.Truncate(value);
            }
        }
    }
}
